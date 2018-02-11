using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.Azure.Documents.Linq;

namespace Planetsino.Models
{
    public static class DbHelper
    {
        public static readonly string DatabaseId; 
        public static readonly int InitialThroughput;
        public static readonly int MaxConnectionLimit;
        public static ConsistencyLevel ConsistencyLevel;

        public static readonly string ServerName;
        public static readonly string ServerSuffix;
        public static readonly DbClientInfo[] Clients;
        public static readonly DbClientInfo PrimaryClient;
        private static readonly string[] PreferredLocations;
        public static double RequestCharge;

        private static readonly char[] delimiter = new char[] { ',' };

        static DbHelper()
        {
            // Init basic settings
            DatabaseId = ConfigurationManager.AppSettings["DatabaseId"];
            InitialThroughput = int.Parse(ConfigurationManager.AppSettings["InitialThroughput"]);
            MaxConnectionLimit = int.Parse(ConfigurationManager.AppSettings["MaxConnectionLimit"]);
            ConsistencyLevel = (ConsistencyLevel)Enum.Parse(typeof(ConsistencyLevel), ConfigurationManager.AppSettings["ConsistencyLevel"]);

            var endpointUrls = ConfigurationManager.AppSettings["EndpointURLs"].Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
            var authKeys = ConfigurationManager.AppSettings["AuthKeys"].Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
            
            if (endpointUrls.Length != authKeys.Length || endpointUrls.Length == 0)
            {
                throw new Exception("Invalid configuration of EndpointURLs and AuthKeys");
            }

            // Server specific settings
            ServerName = Environment.GetEnvironmentVariable("APPSETTING_WEBSITE_SITE_NAME") ?? "local"; // The name of the app service
            ServerSuffix = ServerName.Contains("-") ? $"-{ServerName.Split('-')[1]}" : ""; // Will be -eastus, -westeu or similar

            PreferredLocations = (ConfigurationManager.AppSettings["PreferredLocations" + ServerSuffix] ?? "").Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

            // Create connection poliy (same for all endpoints/clients)
            var connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = (ConnectionMode)Enum.Parse(typeof(ConnectionMode), ConfigurationManager.AppSettings["ConnectionMode"]),
                ConnectionProtocol = (Protocol)Enum.Parse(typeof(Protocol), ConfigurationManager.AppSettings["ConnectionProtocol"]),
                EnableEndpointDiscovery = true,
                MaxConnectionLimit = MaxConnectionLimit,
                RetryOptions = new RetryOptions { MaxRetryAttemptsOnThrottledRequests = 10, MaxRetryWaitTimeInSeconds = 30 }
            };

            foreach (var location in PreferredLocations)
                connectionPolicy.PreferredLocations.Add(location);

            // Create clients
            Clients = new DbClientInfo[endpointUrls.Length];
            for (var i = 0; i < Clients.Length; i++)
            {
                var client = new DbClientInfo();
                client.Name = new Uri(endpointUrls[i]).Host.Split('.').First();
                client.DocumentClient = new DocumentClient(new Uri(endpointUrls[i]), authKeys[i], connectionPolicy, ConsistencyLevel);
                client.DocumentClient.OpenAsync(); // Preload routing tables
                client.IsPrimaryClient = ServerSuffix.Length != 0 && endpointUrls[i].Contains(ServerSuffix);
                Clients[i] = client;
            }

            // Detect primary client
            if (Clients.Where(c => c.IsPrimaryClient).Count() >= 1)
                PrimaryClient = Clients.Where(c => c.IsPrimaryClient).First();
            else
                PrimaryClient = Clients.First();

            ResetRequestCharge();
        }

        public static async Task CreateDatabases()
        {
            // Create databases if not existing already
            foreach (var client in Clients)
                await CreateDatabase(client.DocumentClient);
        }

        public static async Task CreateDatabase(DocumentClient client)
        {
            await client.CreateDatabaseIfNotExistsAsync(new Database { Id = DatabaseId });
        }

        public static async Task CreateCollections(string collectionId, string partitionKey)
        {
            // Create collections if not existing already
            foreach (var client in Clients)
                await CreateCollection(client.DocumentClient, collectionId, partitionKey);
        }

        public static async Task CreateCollection(DocumentClient client, string collectionId, string partitionKey)
        {
            var myCollection = new DocumentCollection();
            myCollection.Id = collectionId;
            myCollection.PartitionKey.Paths.Add(partitionKey);

            await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseId),
                myCollection,
                new RequestOptions { OfferThroughput = InitialThroughput });
        }

        public static async Task DeleteCollections(string collectionId)
        {
            foreach (var client in Clients)
                await DeleteCollection(client.DocumentClient, collectionId);
        }

        public static async Task DeleteCollection(DocumentClient client, string collectionId)
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DatabaseId, collectionId);

            await client.DeleteDocumentCollectionAsync(uri);
        }

        public static async Task DeleteDatabases()
        {
            foreach (var client in Clients)
                await DeleteDatabase(client.DocumentClient);
        }

        public static async Task DeleteDatabase(DocumentClient client)
        {
            var uri = UriFactory.CreateDatabaseUri(DatabaseId);

            await client.DeleteDatabaseAsync(uri);
        }

        public static async Task Create(IDocument document, string collectionId)
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DatabaseId, collectionId);
            if (document.ClientName == null)
                document.ClientName = PrimaryClient.Name;
            var response = await GetDocumentClientByName(document.ClientName).CreateDocumentAsync(uri, document);
            RequestCharge += response.RequestCharge;
        }

        public static async Task Upsert(IDocument document, string collectionId)
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DatabaseId, collectionId);
            if (document.ClientName == null)
                document.ClientName = PrimaryClient.Name;
            var response = await GetDocumentClientByName(document.ClientName).UpsertDocumentAsync(uri, document);
            RequestCharge += response.RequestCharge;
        }

        public static async Task Replace(IDocument document, string key, string collectionId)
        {
            var uri = UriFactory.CreateDocumentUri(DatabaseId, collectionId, key);
            if (document.ClientName == null)
                document.ClientName = PrimaryClient.Name;
            var response = await GetDocumentClientByName(document.ClientName).ReplaceDocumentAsync(uri, document);
            RequestCharge += response.RequestCharge;
        }

        public static async Task<DocumentResponse<T>> Get<T>(string clientName, string key, object partitionKey, string collectionId) where T : IDocument
        {
            if (string.IsNullOrEmpty(clientName))
                return await Get<T>(key, partitionKey, collectionId);

            var uri = UriFactory.CreateDocumentUri(DatabaseId, collectionId, key);

            var response = await GetDocumentClientByName(clientName).ReadDocumentAsync<T>(uri, new RequestOptions { PartitionKey = new PartitionKey(partitionKey) });
            response.Document.ClientName = clientName;
            RequestCharge += response.RequestCharge;

            return response;
        }

        public static async Task<DocumentResponse<T>> Get<T>(string key, object partitionKey, string collectionId) where T : IDocument
        {
            var uri = UriFactory.CreateDocumentUri(DatabaseId, collectionId, key);

            var tasks = Clients.Select(c => Task.Run( async () =>
                {
                    try
                    {
                        var result = await c.DocumentClient.ReadDocumentAsync<T>(uri, new RequestOptions { PartitionKey = new PartitionKey(partitionKey) });
                        result.Document.ClientName = c.Name;
                        return result;
                    }
                    catch
                    {
                        return null;
                    }
                })).ToArray();

            var responses = await Task.WhenAll(tasks);
            var response = responses.Where(r => r != null).Single();

            return response;
        }

        public static async Task<T[]> Query<T>(string top, string filter, string collectionId) where T : IDocument
        {
            var tasks = Clients.Select(client => Query<T>(client, top, filter, collectionId)).ToArray();

            var items = await Task.WhenAll(tasks);
            var results = items.SelectMany(i => i).ToArray();

            return results;
        }

        public static async Task<T[]> Query<T>(DbClientInfo client, string top, string filter, string collectionId) where T : IDocument
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DatabaseId, collectionId);
            if (!string.IsNullOrEmpty(filter))
                filter = "WHERE " + filter;
            else
                filter = filter ?? "";
            top = top ?? "";
            var sql = new SqlQuerySpec($"SELECT {top} * FROM {collectionId} AS c {filter}");

            var query = client.DocumentClient.CreateDocumentQuery<T>(uri, sql, new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery();

            var results = new List<T>();
            while (query.HasMoreResults)
            {
                var items = await query.ExecuteNextAsync<T>();
                results.AddRange(items.AsEnumerable());
                RequestCharge += items.RequestCharge;
            }

            foreach (var item in results)
                item.ClientName = client.Name;

            return results.ToArray();
        }

        public static async Task<T> ExecStoredProcedure<T>(string clientName, string procedureName, string collectionId, string partitionKey, params dynamic[] parameters)
        {
            var uri = UriFactory.CreateStoredProcedureUri(DatabaseId, collectionId, procedureName);

            var response = await GetDocumentClientByName(clientName).ExecuteStoredProcedureAsync<T>(uri, new RequestOptions { PartitionKey = new PartitionKey(partitionKey) }, parameters);
            RequestCharge += response.RequestCharge;
            return response;
        }

        public static async Task CreateStoredProcedure(string clientName, string procedureName, string collectionId, string body)
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DatabaseId, collectionId);
            var proc = new StoredProcedure { Id = procedureName, Body = body };

            try
            {
                var response = await GetDocumentClientByName(clientName).ReadStoredProcedureAsync(UriFactory.CreateStoredProcedureUri(DatabaseId, collectionId, procedureName));
                RequestCharge += response.RequestCharge;
            }
            catch
            {
                var response = await GetDocumentClientByName(clientName).CreateStoredProcedureAsync(uri, proc);
                RequestCharge += response.RequestCharge;
            }
        }

        public static void ResetRequestCharge()
        {
            RequestCharge = 0.0d;
        }

        public static DocumentClient GetDocumentClientByName(string clientName)
        {
            return Clients.Where(c => c.Name == clientName).Single().DocumentClient;
        }

        public static string Diagnostics()
        {
            var results = $"Server name: {ServerName} <br/>";
            results += $"Server suffix: {ServerSuffix} <br/>";
            results += $"Total RequestCharge: {RequestCharge:f2} <br/>";
            results += $"Primary client: {PrimaryClient.Name} <br/>";
            results += $"PreferredLocations: {string.Join(",", PreferredLocations)} <br/>";

            foreach (var client in Clients)
            {
                results += $"<br/>Client name: {client.Name} <br/>";

                var documentClient = client.DocumentClient;
                results += $"ServiceEndpoint: {documentClient.ServiceEndpoint} <br/>";
                results += $"ReadEndpoint: {documentClient.ReadEndpoint} <br/>";
                results += $"WriteEndpoint: {documentClient.WriteEndpoint} <br/>";
                results += $"ConsistencyLevel: {documentClient.ConsistencyLevel} <br/>";
                results += $"ConnectionMode: {documentClient.ConnectionPolicy.ConnectionMode} <br/>";
                results += $"ConnectionProtocol: {documentClient.ConnectionPolicy.ConnectionProtocol} <br/>";
                results += $"MaxConnectionLimit: {documentClient.ConnectionPolicy.MaxConnectionLimit} <br/>";
                results += $"PreferredLocations: {string.Join(", ", documentClient.ConnectionPolicy.PreferredLocations)} <br/>";
            }

            return results;
        }
    }
}
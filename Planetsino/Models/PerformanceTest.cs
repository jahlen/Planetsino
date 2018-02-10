using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Planetsino.Models
{
    public class PerformanceTest
    {
        public struct Results
        {
            public string Name;
            public long ElapsedMilliseconds;
            public double RUCost;
            public int NumberOfOperations;

            public long OperationPerSecond => ElapsedMilliseconds != 0 ? NumberOfOperations * 1000 / ElapsedMilliseconds : 0;
            public double RUsPerSecond => ElapsedMilliseconds != 0 ? RUCost * 1000 / ElapsedMilliseconds : 0;
            public double RUsPerDocument => NumberOfOperations != 0 ? RUCost / NumberOfOperations : 0;
        };

        // Input parameters for the test
        public int NumberOfWritesPrimary { get; set; }
        public int NumberOfWritesSecondary { get; set; }
        public int NumberOfQueryResultsPrimary { get; set; }
        public int NumberOfQueryResultsSecondary { get; set; }
        public int NumberOfRandomReadsPrimary { get; set; }
        public int NumberOfRandomReadsSecondary { get; set; }
        public int NumberOfUpsertsPrimary { get; set; }
        public int NumberOfUpsertsSecondary { get; set; }
        public int Parallelism { get; set; }

        // The clients used (could be the same if there is only one client available)
        public DbClientInfo PrimaryClient;
        public DbClientInfo SecondaryClient;

        // Test results
        public Results WritesPrimary;
        public Results WritesSecondary;
        public Results QueryResultsPrimary;
        public Results QueryResultsSecondary;
        public Results RandomReadsPrimary;
        public Results RandomReadsSecondary;
        public Results UpsertsPrimary;
        public Results UpsertsSecondary;

        public Results[] AllResults => new[] { WritesPrimary, WritesSecondary, QueryResultsPrimary, QueryResultsSecondary, RandomReadsPrimary, RandomReadsSecondary, UpsertsPrimary, UpsertsSecondary };
        
        private int counter;
        private Player[] playersPrimary;
        private Player[] playersSecondary;

        public async Task RunTests()
        {
            if (Parallelism < 1)
                Parallelism = 1;
            if (NumberOfQueryResultsPrimary < 1)
                NumberOfQueryResultsPrimary = 1;
            if (NumberOfQueryResultsSecondary < 1)
                NumberOfQueryResultsSecondary = 1;

            PrimaryClient = DbHelper.PrimaryClient;
            if (DbHelper.Clients.Length == 1)
            {
                // If there is only one client available, use also as secondary
                SecondaryClient = PrimaryClient;
            }
            else
            {
                // Make sure we don't take the primary client
                SecondaryClient = DbHelper.Clients.Where(c => c != PrimaryClient).Last();
            }

            WritesPrimary = await RunCreateTest("WritesPrimary", PrimaryClient, NumberOfWritesPrimary);
            WritesSecondary = await RunCreateTest("WritesSecondary", SecondaryClient, NumberOfWritesPrimary);

            QueryResultsPrimary = await RunTest("QueryResultsPrimary", async () => playersPrimary = await DbHelper.Query<Player>(PrimaryClient, $"TOP {NumberOfQueryResultsPrimary}", null, Player.CollectionId), NumberOfQueryResultsPrimary);
            QueryResultsSecondary = await RunTest("QueryResultsSecondary", async () => playersSecondary = await DbHelper.Query<Player>(SecondaryClient, $"TOP {NumberOfQueryResultsSecondary}", null, Player.CollectionId), NumberOfQueryResultsSecondary);

            RandomReadsPrimary = await RunRandomReadTest("RandomReadsPrimary", PrimaryClient, NumberOfRandomReadsPrimary, playersPrimary);
            RandomReadsSecondary = await RunRandomReadTest("RandomReadsSecondary", SecondaryClient, NumberOfRandomReadsSecondary, playersSecondary);

            UpsertsPrimary = await RunUpsertTest("UpsertsPrimary", PrimaryClient, NumberOfUpsertsPrimary, playersPrimary);
            UpsertsSecondary = await RunUpsertTest("UpsertsSecondary", SecondaryClient, NumberOfUpsertsSecondary, playersSecondary);
        }

        private async Task<Results> RunTest(string testName, Func<Task> func, int count)
        {
            var stopWatch = Stopwatch.StartNew();
            var prevRequestCharge = DbHelper.RequestCharge;
            await func();
            var results = new Results { ElapsedMilliseconds = stopWatch.ElapsedMilliseconds, RUCost = DbHelper.RequestCharge - prevRequestCharge, Name = testName, NumberOfOperations = count };
            return results;
        }

        private async Task<Results> RunCreateTest(string testName, DbClientInfo client, int count)
        {
            var stopWatch = Stopwatch.StartNew();
            var prevRequestCharge = DbHelper.RequestCharge;
            counter = 0;
            var tasks = Enumerable.Range(0, Parallelism).Select(i => Task.Run(Create)).ToArray();
            await Task.WhenAll(tasks);
            var results = new Results { ElapsedMilliseconds = stopWatch.ElapsedMilliseconds, RUCost = DbHelper.RequestCharge - prevRequestCharge, Name = testName, NumberOfOperations = count };
            return results;

            async Task Create()
            {
                while (true)
                {
                    var i = System.Threading.Interlocked.Increment(ref counter);
                    if (i > count)
                        return;
                    var player = Player.New();
                    player.ClientName = client.Name;
                    await DbHelper.Create(player, Player.CollectionId);
                }
            }
        }

        private async Task<Results> RunRandomReadTest(string testName, DbClientInfo client, int count, Player[] players)
        {
            var stopWatch = Stopwatch.StartNew();
            var prevRequestCharge = DbHelper.RequestCharge;
            counter = 0;
            var tasks = Enumerable.Range(0, Parallelism).Select(i => Task.Run(Read)).ToArray();
            await Task.WhenAll(tasks);
            var results = new Results { ElapsedMilliseconds = stopWatch.ElapsedMilliseconds, RUCost = DbHelper.RequestCharge - prevRequestCharge, Name = testName, NumberOfOperations = count };
            return results;

            async Task Read()
            {
                while (true)
                {
                    var i = System.Threading.Interlocked.Increment(ref counter);
                    if (i > count)
                        return;
                    var j = new Random(i).Next(players.Length);
                    var player = await Player.Load(players[j].PlayerGuid, players[j].ClientName);
                }
            }
        }

        private async Task<Results> RunUpsertTest(string testName, DbClientInfo client, int count, Player[] players)
        {
            var stopWatch = Stopwatch.StartNew();
            var prevRequestCharge = DbHelper.RequestCharge;
            counter = 0;
            var tasks = Enumerable.Range(0, Parallelism).Select(i => Task.Run(Upsert)).ToArray();
            await Task.WhenAll(tasks);
            var results = new Results { ElapsedMilliseconds = stopWatch.ElapsedMilliseconds, RUCost = DbHelper.RequestCharge - prevRequestCharge, Name = testName, NumberOfOperations = count };
            return results;

            async Task Upsert()
            {
                while (true)
                {
                    var i = System.Threading.Interlocked.Increment(ref counter);
                    if (i > count)
                        return;
                    var j = new Random(i).Next(players.Length);
                    players[j].Balance += 5;
                    await players[j].Upsert();
                }
            }
        }
    }
}
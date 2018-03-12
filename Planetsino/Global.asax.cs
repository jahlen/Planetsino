using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Planetsino.Models;
using System.IO;

namespace Planetsino
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public static TaskCompletionSource<Exception> DatabaseReady = new TaskCompletionSource<Exception>();

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            Task.Run(InitDatabase);

            // Make sure database and collection is created before continuing
            var dbCompletedInitialization = DatabaseReady.Task.Wait(TimeSpan.FromSeconds(60));
            if (!dbCompletedInitialization)
                throw new Exception("Database initialization timed out");

            // Check if an exception occured during initialization
            if (DatabaseReady.Task.Result != null)
                throw DatabaseReady.Task.Result;
        }

        private async Task InitDatabase()
        {
            try
            {
                await DbHelper.CreateDatabases();
                await DbHelper.CreateCollections(Player.CollectionId, Player.PartitionKey);

                var path = HttpRuntime.AppDomainAppPath; // Server.MapPath("~");
                var procedureBody = File.ReadAllText(Path.Combine(path, "Procedures/AdjustBalance.js"));
                await DbHelper.CreateStoredProcedure(DbHelper.PrimaryClient.Name, "adjustBalance", Player.CollectionId, procedureBody);

                DatabaseReady.SetResult(null);
            }
            catch (Exception ex)
            {
                DatabaseReady.SetResult(ex);
            }
        }
    }
}

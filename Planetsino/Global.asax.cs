using System;
using System.Collections.Generic;
using System.Linq;
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
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            InitDatabase();
        }

        private async void InitDatabase()
        {
            await DbHelper.CreateDatabases();
            await DbHelper.CreateCollections(Player.CollectionId, Player.PartitionKey);

            var path = HttpRuntime.AppDomainAppPath; // Server.MapPath("~");
            var procedureBody = File.ReadAllText(Path.Combine(path, "Procedures/AdjustBalance.js"));
            await DbHelper.CreateStoredProcedure(DbHelper.PrimaryClient.Name, "adjustBalance", Player.CollectionId, procedureBody);
        }
    }
}

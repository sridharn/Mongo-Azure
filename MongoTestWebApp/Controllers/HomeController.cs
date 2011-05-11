using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using Microsoft.WindowsAzure.ServiceRuntime;

using MongoDB.MongoHelper;

namespace MongoTestWebApp.Controllers
{
    [HandleError]
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewData["Message"] = "Welcome to the MongoDB Azure Web App";

            return View();
        }

        public ActionResult About()
        {
            ViewData["Status"] = GetStatusMessage();
            return View();
        }

        private string GetStatusMessage()
        {
            var mongodEndpoint = 
                RoleEnvironment.Roles[MongoHelper.MongoRoleName].Instances[0].InstanceEndpoints[MongoHelper.MongodPortKey].IPEndpoint;
            var status = "Role Not Started";
            if (mongodEndpoint != null)
            {
                status = MongoHelper.GetStatusMessage(mongodEndpoint.Address.ToString(), mongodEndpoint.Port);
            }
            return status;
        }
    }
}

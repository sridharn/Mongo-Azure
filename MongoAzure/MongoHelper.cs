namespace MongoDB.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Text;

    using MongoDB.Bson;
    using MongoDB.Driver;
    using MongoDB.Driver.Builders;
    using MongoDB.Bson.Serialization.Attributes;

    using Microsoft.WindowsAzure.ServiceRuntime;

    public class MongoHelper
    {

        private const string MongoAzureSystemDatabase = "mongoazure";
        private const string MongoAzureSystemTable = "system";

        public const string MongodPortKey = "MongodPort";
        public const string MongoRoleName = "MongoWorkerRole";

        private static string GetMongoConnectionString(string host, int port)
        {
            var connectionString = new StringBuilder();
            connectionString.Append("mongodb://");
            connectionString.Append(string.Format("{0}:{1}", host, port));
            return connectionString.ToString();
        }

        private static IPEndPoint GetMongoPort()
        {
            // need to figure out how to check if the instance is actually up
            var roleInstances =
                RoleEnvironment.Roles[MongoHelper.MongoRoleName].Instances;
            IPEndPoint mongodEndpoint = null;
            foreach (var instance in roleInstances)
            {
                mongodEndpoint = instance.InstanceEndpoints[MongoHelper.MongodPortKey].IPEndpoint;
                var isEndpointValid = CheckEndpoint(mongodEndpoint);
                if (isEndpointValid)
                {
                    return mongodEndpoint;
                }
            }

            throw new ApplicationException("Could not connect to mongo");
        }

        private static bool CheckEndpoint(IPEndPoint mongodEndpoint)
        {
            var valid = false;
            var server = MongoServer.Create(GetMongoConnectionString(
                mongodEndpoint.Address.ToString(),
                mongodEndpoint.Port));

            if (server.State == MongoServerState.Disconnected)
            {
                try
                {
                    server.Connect(TimeSpan.FromSeconds(2));
                    valid = true;
                }
                catch 
                {
                    valid = false;
                }
            }
            else
            {
                valid = true;
            }
            return valid;
        }

        public static string GetMongoConnectionString()
        {
            var mongodEndpoint = GetMongoPort();
            return GetMongoConnectionString(mongodEndpoint.Address.ToString(), mongodEndpoint.Port);
        }

        public static string GetLocalMongoConnectionString()
        {
            var mongodEndpoint = GetMongoPort();
            return GetMongoConnectionString("127.0.0.1", mongodEndpoint.Port);
        }

        public static MongoServer GetMongoServer()
        {
            var server = MongoServer.Create(GetMongoConnectionString());

            if (server.State == MongoServerState.Disconnected)
            {
                try
                {
                    server.Connect(TimeSpan.FromSeconds(2));
                }
                catch (Exception e)
                {
                    throw new ApplicationException("Could not connect to mongo: " + e.Message);
                }
            }
            return server;
        }

        public static MongoServer GetLocalMongoServer()
        {
            var server = MongoServer.Create(GetLocalMongoConnectionString());

            if (server.State == MongoServerState.Disconnected)
            {
                try
                {
                    server.Connect(TimeSpan.FromSeconds(2));
                }
                catch (Exception e)
                {
                    throw new ApplicationException("Could not connect to mongo: " + e.Message);
                }
            }
            return server;
        }

        public static void ShutdownMongo()
        {
            try
            {
                var server = GetLocalMongoServer();
                // server.Shutdown();
                server.RunAdminCommand("shutdown");
            }
            catch 
            {
                // ignore exceptions since this is only called during shutdown
            }
        }

    }
}

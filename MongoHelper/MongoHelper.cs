namespace MongoDB.MongoHelper
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    using Microsoft.WindowsAzure.ServiceRuntime;

    using MongoDB.Driver;

    public class MongoHelper
    {

        public const int MongoPort = 27017;

        public static string GetMongoConnectionString()
        {
            var connectionString = new StringBuilder();
            connectionString.Append("mongodb://");
            var endpoints = RoleEnvironment.Roles["Mongo"]
                .Instances.Select(instance => instance.InstanceEndpoints["MongoPort"])
                .ToList();
            var mongoNode = endpoints.First();
            connectionString.Append(string.Format("{0}:{1}", mongoNode.IPEndpoint.Address,
                                                      mongoNode.IPEndpoint.Port));
            return connectionString.ToString();
        }

        public static MongoServer GetMongoServer()
        {
            var server = MongoServer.Create(GetMongoConnectionString());

            if (server == null)
            {
                var errorMessage = "Unable to connect to mongo: Mongo server is null";
                Trace.TraceError(errorMessage);
                throw new ApplicationException(errorMessage);
            }
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

    }
}

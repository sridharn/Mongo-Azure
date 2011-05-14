namespace MongoDB.MongoHelper
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    using MongoDB.Bson;
    using MongoDB.Driver;
    using MongoDB.Driver.Builders;
    using MongoDB.Bson.Serialization.Attributes;

    public class MongoHelper
    {

        private const string MongoAzureSystemDatabase = "mongoazure";
        private const string MongoAzureSystemTable = "system";

        public const string MongodPortKey = "MongodPort";
        public const string MongoRoleName = "MongoWorkerRole";

        public static string GetMongoConnectionString(string host, int port)
        {
            var connectionString = new StringBuilder();
            connectionString.Append("mongodb://");
            connectionString.Append(string.Format("{0}:{1}", host, port));
            return connectionString.ToString();
        }

        public static MongoServer GetMongoServer(string host, int port)
        {
            var server = MongoServer.Create(GetMongoConnectionString(host, port));

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

        public static void MarkStart(string mongoHost, int mongoPort)
        {
            var server = GetMongoServer(mongoHost, mongoPort);
            var startEntry = new MongoStartEntry()
            {
                Host = mongoHost,
                Port = mongoPort,
                StartTime = DateTime.UtcNow
            };
            var azureDb = server.GetDatabase(MongoAzureSystemDatabase);
            var azureTable = azureDb.GetCollection<MongoStartEntry>(MongoAzureSystemTable);
            azureTable.Insert(startEntry);
        }

        [BsonIgnoreExtraElements]
        public class MongoStartEntry
        {
            public string Host { get; set; }
            public int Port { get; set; }
            public DateTime StartTime { get; set; }
            public override string ToString()
            {
                return string.Format("Host={0} Port={1} Start={2}",
                    Host,
                    Port,
                    StartTime.ToString());
            }
        }

        public static void ShutdownMongo(string host, int port)
        {
            try
            {
                var server = GetMongoServer(host, port);
                server.RunAdminCommand("shutdownServer");
            }
            catch 
            {
                // ignore exceptions since this is only called during shutdown
            }
        }

        public static string GetStatusMessage(string mongoHost, int mongoPort)
        {
            var server = GetMongoServer(mongoHost, mongoPort);
            var azureDb = server.GetDatabase(MongoAzureSystemDatabase);
            var azureTable = azureDb.GetCollection<MongoStartEntry>(MongoAzureSystemTable);
            var sortBy = SortBy.Descending("_id");
            var cursor = azureTable.Find(Query.Null).SetSortOrder(sortBy).SetLimit(1);
            var status = cursor.First<MongoStartEntry>();
            return status.ToString();
        }
    }
}

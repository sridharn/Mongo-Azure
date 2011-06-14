﻿/* Copyright 2010-2011 10gen Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

namespace MongoDB.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    using MongoDB.Bson;
    using MongoDB.Driver;
    using MongoDB.Driver.Builders;
    using MongoDB.Bson.Serialization.Attributes;

    using Microsoft.WindowsAzure.ServiceRuntime;

    public class MongoHelper
    {

        public const string MongodPortKey = "MongodPort";
        public const string MongoRoleName = "MongoWorkerRole";

        private static bool CheckEndpoint(IPEndPoint mongodEndpoint)
        {
            var valid = false;
            using (var s = new Socket(mongodEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    s.Connect(mongodEndpoint);
                    if (s.Connected)
                    {
                        valid = true;
                        s.Disconnect(true);
                    }
                    else
                    {
                        valid = false;
                    }
                }
                catch
                {
                    valid = false;
                }
            }
            return valid;
        }

        public static IPEndPoint GetMongoConnectionString()
        {
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

        public static MongoServer GetMongoServer()
        {
            var mongoEndpoint = GetMongoConnectionString();
            var connectionString = new StringBuilder();
            connectionString.Append("mongodb://");
            connectionString.Append(string.Format("{0}:{1}", 
                mongoEndpoint.Address.ToString(), 
                mongoEndpoint.Port));
            var server = MongoServer.Create(connectionString.ToString());

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

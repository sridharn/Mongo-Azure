/* Copyright 2010-2011 10gen Inc.
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

namespace MongoDB.MongoWorkerRole {

    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.StorageClient;

    using System;
    using System.Linq;

    public class NodeInformationServiceContext : TableServiceContext {

        public NodeInformationServiceContext(string baseAddress, StorageCredentials credentials)
            : base(baseAddress, credentials) {
        }

        public IQueryable<NodeInformation> NodeInfos {
            get {
                return this.CreateQuery<NodeInformation>("NodeInformation");
            }
        }

        public void AddNodeInfo(string name, int id, string ip, int port ) {
            this.AddObject("NodeInfos", new NodeInformation {
                InstanceName = name,
                InstanceId = id,
                InstanceIp = ip,
                InstancePort = port
            });
            this.SaveChanges();
        }

    }

}

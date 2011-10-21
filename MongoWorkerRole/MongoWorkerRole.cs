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
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading;

    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Diagnostics;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using Microsoft.WindowsAzure.StorageClient;

    using MongoDB.Azure;
    using MongoDB.Driver;

    public class MongoWorkerRole : RoleEntryPoint {

        #region constant settings
        // Do not modify
        private const string MongodDataBlobContainerName = "mongoddatadrive";
        private const string MongodDataBlobName = "mongoddblob.vhd";
        private const string MongodLogBlobContainerName = "mongodlogdrive";
        private const string MongodLogBlobName = "mongodlblob.vhd";

        private const string MongoCloudDataDir = "MongoDBDataDir";
        private const string MongoCloudLogDir = "MongoDBLogDir";
        private const string MongoLocalDataDir = "MongoDBLocalDataDir";
        private const string MongoLocalLogDir = "MongoDBLocalLogDir";
        private const string IPTableStore = "TableStore";

        private const string MongoTraceDir = "MongoTraceDir";

        private const string MongoBinaryFolder = @"approot\MongoExe";
        private const string MongoLogFileName = "mongod.log";
        private const string MongodCommandLine = "--dbpath {0} --port {1} --logpath {2} --nohttpinterface --logappend ";

        private const string TraceLogFileDir = "TraceLogFileDir";
        private const string MongodDataBlobCacheDir = "MongodDataBlobCacheDir";
        private const string MongodLogBlobCacheDir = "MongodLogBlobCacheDir";
        private const string DiagnosticsConnectionString = "DiagnosticsConnectionString";

        private const string TraceLogFile = "MongoWorkerTrace.log";

        // The following settings are configurable
        private const int MaxDBDriveSize = 5 * 1024; // in MB
        private const int MaxLogDriveSize = 1024; // in MB
        private const int MountSleep = 30 * 1000; // 30 seconds;

        private readonly TimeSpan DiagnosticTransferInterval = TimeSpan.FromMinutes(30);
        private readonly TimeSpan PerfCounterTransferInterval = TimeSpan.FromMinutes(15);
        // end of configurable section

        #endregion constant settings

        private string mongoDataDriveLetter = null;
        private CloudDrive mongoDataDrive = null;
        private CloudDrive mongoLogDrive = null;
        private string mongoHost;
        private int mongoPort;
        private Process mongodProcess = null;
        private TextWriter traceWriter = null;

        public override void Run() {
            TraceInformation("MongoWorkerRole run method called");
            try {
                mongodProcess.WaitForExit();
            } catch (Exception e) {
                TraceWarning("exception when waiting on mongod process");
                TraceWarning(e.Message);
                TraceWarning(e.StackTrace);
            }
            TraceWarning("MongoWorkerRole run method exiting since mongod process wait completed");
        }

        public override bool OnStart() {
            InitializeDiagnostics();

            TraceInformation("MongoWorkerRole onstart called");
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;
            CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) => {
                configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));
            });

            LogNodeInfo();
            TraceInformation(string.Format("Obtained host={0}, port={1}", mongoHost, mongoPort));

            StartMongoD();
            return base.OnStart();
        }

        public override void OnStop() {
            TraceInformation("MongoWorkerRole onstop called");
            try {
                // should we instead call Process.stop?
                TraceInformation("Shutdown called on mongod");
                if ((mongodProcess != null) &&
                    !(mongodProcess.HasExited)) {
                    ShutdownMongo();
                }
                TraceInformation("Shutdown completed on mongod");
            } catch (Exception e) {
                //Ignore exceptions caught on unmount
                TraceWarning("Exception in onstop - mongo shutdown");
                TraceWarning(e.Message);
                TraceWarning(e.StackTrace);
            }
            try {
                TraceInformation("Unmount called on data drive");
                if (mongoDataDrive != null) {
                    mongoDataDrive.Unmount();
                }
                TraceInformation("Unmount completed on data drive");
            } catch (Exception e) {
                //Ignore exceptions caught on unmount
                TraceWarning("Exception in onstop - unmount of data drive");
                TraceWarning(e.Message);
                TraceWarning(e.StackTrace);
            }
            try {
                TraceInformation("Unmount called on log drive");
                if (mongoLogDrive != null) {
                    mongoLogDrive.Unmount();
                }
                TraceInformation("Unmount completed on log drive");
            } catch (Exception e) {
                //Ignore exceptions caught on unmount
                TraceWarning("Exception in onstop - unmount of log drive");
                TraceWarning(e.Message);
                TraceWarning(e.StackTrace);
            }
            TraceInformation("Calling diagnostics shutdown");
            ShutdownDiagnostics();
            base.OnStop();
        }

        private void LogNodeInfo() {
            SetHostAndPort();
            // StoreNodeInfo();
       
        }

        private int ParseNodeInstanceId() {
            string instanceId = RoleEnvironment.CurrentRoleInstance.Id;
            int instanceIndex = 0;
            if (RoleEnvironment.IsEmulated) {
                int.TryParse(instanceId.Substring(instanceId.LastIndexOf("_") + 1), out instanceIndex);
            } else {
                int.TryParse(instanceId.Substring(instanceId.LastIndexOf(".") + 1), out instanceIndex);
            }
            return instanceIndex;
        }

        private void StoreNodeInfo() {
            var account = CloudStorageAccount.FromConfigurationSetting(IPTableStore);
            try {
                CloudTableClient.CreateTablesFromModel(typeof(NodeInformationServiceContext),
                               account.TableEndpoint.AbsoluteUri, account.Credentials);
            } catch { }

            var context = new NodeInformationServiceContext(account.TableEndpoint.ToString(), account.Credentials);
            context.AddNodeInfo(RoleEnvironment.CurrentRoleInstance.Id, ParseNodeInstanceId(), mongoHost, mongoPort);
        }

        private void StartMongoD() {
            var mongoAppRoot = Path.Combine(
                Environment.GetEnvironmentVariable("RoleRoot") + @"\",
                MongoBinaryFolder);
            var mongodPath = Path.Combine(mongoAppRoot, @"mongod.exe");

            var blobPath = GetMongoDataDirectory();

            var logFile = GetLogFile();

            var cmdline = String.Format(MongodCommandLine,
                blobPath,
                mongoPort,
                logFile);
            TraceInformation(string.Format("Launching mongod as {0} {1}", mongodPath, cmdline));

            // launch mongo
            try {
                mongodProcess = new Process() {
                    StartInfo = new ProcessStartInfo(mongodPath, cmdline) {
                        UseShellExecute = false,
                        WorkingDirectory = mongoAppRoot,
                        CreateNoWindow = false
                    }
                };
                mongodProcess.Start();
            } catch (Exception e) {

                TraceError("Can't start Mongo: " + e.Message);
                throw new ApplicationException("Can't start mongo: " + e.Message); // throwing an exception here causes the VM to recycle
            }
        }

        private string GetMongoDataDirectory() {
            TraceInformation("Getting db path");
            mongoDataDriveLetter = GetMountedPathFromBlob(
                MongoLocalDataDir,
                MongoCloudDataDir,
                MongodDataBlobContainerName,
                MongodDataBlobName,
                MaxDBDriveSize,
                true,
                false,
                out mongoDataDrive
                );
            TraceInformation(string.Format("Obtained data drive as {0}", mongoDataDriveLetter));
            var dir = Directory.CreateDirectory(Path.Combine(mongoDataDriveLetter, @"data"));
            TraceInformation(string.Format("Data directory is {0}", dir.FullName));
            return dir.FullName;
        }

        private string GetLogFile() {
            TraceInformation("Getting log file base path");
            var path = GetMountedPathFromBlob(
                MongoLocalLogDir,
                MongoCloudLogDir,
                MongodLogBlobContainerName,
                MongodLogBlobName,
                MaxLogDriveSize,
                false,
                true,
                out mongoLogDrive
                );
            TraceInformation(string.Format("Obtained log root directory as {0}", path));
            var dir = Directory.CreateDirectory(Path.Combine(path, @"log"));
            var logfile = Path.Combine(dir.FullName + @"\", MongoLogFileName);
            return logfile;
        }

        private void SetHostAndPort() {
            var endPoint = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints[MongoHelper.MongodPortKey].IPEndpoint;
            mongoHost = endPoint.Address.ToString();
            mongoPort = endPoint.Port;
        }

        private string GetMountedPathFromBlob(
            string localCachePath,
            string cloudDir,
            string containerName,
            string blobName,
            int driveSize,
            bool waitOnMount,
            bool fallback,
            out CloudDrive mongoDrive) {
            TraceInformation(string.Format("In mounting cloud drive for dir {0}", cloudDir));

            CloudStorageAccount storageAccount = null;

            if (fallback) {
                try {
                    storageAccount = CloudStorageAccount.FromConfigurationSetting(cloudDir);
                } catch {
                    // case for fallback to data dir for log also
                    TraceInformation(string.Format("{0} is not found. using backup", cloudDir));
                    mongoDrive = mongoDataDrive;
                    return mongoDataDriveLetter;
                }
            } else {
                storageAccount = CloudStorageAccount.FromConfigurationSetting(cloudDir);
            }
            var blobClient = storageAccount.CreateCloudBlobClient();

            TraceInformation("Get container");
            // this should be the name of your replset
            var driveContainer = blobClient.GetContainerReference(containerName);

            // create blob container (it has to exist before creating the cloud drive)
            try {
                driveContainer.CreateIfNotExist();
            } catch (Exception e) {
                TraceInformation("Exception when creating container");
                TraceInformation(e.Message);
                TraceInformation(e.StackTrace);
            }

            var mongoBlobUri = blobClient.GetContainerReference(containerName).GetPageBlobReference(blobName).Uri.ToString();
            TraceInformation(string.Format("Blob uri obtained {0}", mongoBlobUri));

            // create the cloud drive
            mongoDrive = storageAccount.CreateCloudDrive(mongoBlobUri);
            try {
                mongoDrive.Create(driveSize);
            } catch (Exception e) {
                // exception is thrown if all is well but the drive already exists
                TraceInformation("Exception when creating cloud drive. safe to ignore");
                TraceInformation(e.Message);
                TraceInformation(e.StackTrace);

            }

            TraceInformation("Initialize cache");
            var localStorage = RoleEnvironment.GetLocalResource(localCachePath);

            CloudDrive.InitializeCache(localStorage.RootPath.TrimEnd('\\'),
                localStorage.MaximumSizeInMegabytes);

            // mount the drive and get the root path of the drive it's mounted as
            if (!waitOnMount) {
                try {
                    TraceInformation(string.Format("Trying to mount blob as azure drive on {0}",
                        RoleEnvironment.CurrentRoleInstance.Id));
                    var driveLetter = mongoDrive.Mount(localStorage.MaximumSizeInMegabytes,
                        DriveMountOptions.None);
                    TraceInformation(string.Format("Write lock acquired on azure drive, mounted as {0}, on role instance",
                        driveLetter, RoleEnvironment.CurrentRoleInstance.Id));
                    return driveLetter;
                } catch (Exception e) {
                    TraceWarning("could not acquire blob lock.");
                    TraceWarning(e.Message);
                    TraceWarning(e.StackTrace);
                    throw;
                }
            } else {
                string driveLetter;
                TraceInformation(string.Format("Trying to mount blob as azure drive on {0}",
                    RoleEnvironment.CurrentRoleInstance.Id));
                while (true) {
                    try {
                        driveLetter = mongoDrive.Mount(localStorage.MaximumSizeInMegabytes,
                            DriveMountOptions.None);
                        TraceInformation(string.Format("Write lock acquired on azure drive, mounted as {0}, on role instance",
                            driveLetter, RoleEnvironment.CurrentRoleInstance.Id));
                        return driveLetter;
                    } catch { }
                    Thread.Sleep(MountSleep);
                }
            }
        }

        private void InitializeDiagnostics() {
            var diagObj = DiagnosticMonitor.GetDefaultInitialConfiguration();
            diagObj.Logs.ScheduledTransferPeriod = DiagnosticTransferInterval;
            AddPerfCounters(diagObj);
            diagObj.PerformanceCounters.ScheduledTransferPeriod = PerfCounterTransferInterval;
            diagObj.Logs.ScheduledTransferLogLevelFilter = LogLevel.Verbose;
            DiagnosticMonitor.Start(DiagnosticsConnectionString, diagObj);

            var localStorage = RoleEnvironment.GetLocalResource(MongoTraceDir);
            var fileName = Path.Combine(localStorage.RootPath, TraceLogFile);
            traceWriter = new StreamWriter(fileName);
            TraceInformation(string.Format("Local log file is {0}", fileName));
        }

        private void AddPerfCounters(DiagnosticMonitorConfiguration diagObj) {
            AddPerfCounter(diagObj, @"\LogicalDisk(*)\% Disk Read Time", 30);
            AddPerfCounter(diagObj, @"\LogicalDisk(*)\% Disk Write Time", 30);
            AddPerfCounter(diagObj, @"\LogicalDisk(*)\% Free Space", 30);
            AddPerfCounter(diagObj, @"\LogicalDisk(*)\Disk Read Bytes/sec", 30);
            AddPerfCounter(diagObj, @"\LogicalDisk(*)\Disk Write Bytes/sec", 30);
            AddPerfCounter(diagObj, @"\Memory\Available MBytes", 30);
            AddPerfCounter(diagObj, @"\Network Interface(*)\Bytes Received/sec", 30);
            AddPerfCounter(diagObj, @"\Network Interface(*)\Bytes Sent/sec", 30);
            AddPerfCounter(diagObj, @"\Processor(*)\% Processor Time", 30);
            AddPerfCounter(diagObj, @"\PhysicalDisk(*)\% Disk Read Time", 30);
            AddPerfCounter(diagObj, @"\PhysicalDisk(*)\% Disk Write Time", 30);
        }

        private static void AddPerfCounter(DiagnosticMonitorConfiguration config, string name, double seconds) {
            var perfmon = new PerformanceCounterConfiguration();
            perfmon.CounterSpecifier = name;
            perfmon.SampleRate = System.TimeSpan.FromSeconds(seconds);
            config.PerformanceCounters.DataSources.Add(perfmon);
        }

        private void ShutdownDiagnostics() {
            if (traceWriter != null) {
                try {
                    traceWriter.Close();
                } catch {
                    // ignore exceptions on close.
                }
            }
        }

        public static void ShutdownMongo() {
            var mongoEndpoint = MongoHelper.GetMongoEndPoint();
            var server = MongoServer.Create(
                string.Format("mongod://{0}:{1}",
                    mongoEndpoint.Address.ToString(),
                    mongoEndpoint.Port));
            server.Shutdown();
        }

        #region Trace wrappers

        private void TraceInformation(string message) {
            Trace.TraceInformation(message);
            WriteTraceMessage(message, "INFORMATION");
        }

        private void TraceWarning(string message) {
            Trace.TraceWarning(message);
            WriteTraceMessage(message, "WARNING");
        }

        private void TraceError(string message) {
            Trace.TraceError(message);
            WriteTraceMessage(message, "ERRROR");
        }

        private void WriteTraceMessage(string message, string type) {
            if (traceWriter != null) {
                try {
                    var messageString = string.Format("{0}-{1}-{2}", DateTime.UtcNow.ToString(), type, message);
                    traceWriter.WriteLine(messageString);
                    traceWriter.Flush();
                } catch {
                    // ignore trace message errors
                }
            }
        }

        #endregion Trace wrappers

    }
}

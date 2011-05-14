namespace MongoDB.MongoWorkerRole
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading;

    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using Microsoft.WindowsAzure.StorageClient;

    using MongoDB.MongoHelper;
    using Microsoft.WindowsAzure.Diagnostics;

    public class MongoWorkerRole : RoleEntryPoint
    {

        #region constant settings

        private const string MongodDataBlobContainerName = "mongoddatadrive";
        private const string MongodDataBlobName = "mongoddblob.vhd";
        private const string MongodLogBlobContainerName = "mongodlogdrive";
        private const string MongodLogBlobName = "mongodlblob.vhd";

        private const string MongoCloudDataDir = "MongoDBDataDir";
        private const string MongoCloudLogDir = "MongoDBLogDir";
        private const string MongoLocalDataDir = "MongoDBLocalDataDir";
        private const string MongoLocalLogDir = "MongoDBLocalLogDir";

        private const string MongoTraceDir = "MongoTraceDir";
        
        private const string MongoBinaryFolder = @"approot\MongoExe";
        private const string MongoLogFileName = "mongod.log";
        private const string MongodCommandLine = "--dbpath {0} --port {1} --logpath {2} --journal --nohttpinterface ";
        private const int MaxRetryCount = 5;
        private const int SleepBetweenRetry = 30 * 1000; // 30 seconds

        private readonly TimeSpan DiagnosticTransferInterval = TimeSpan.FromSeconds(3);

        private const string TraceLogFileDir = "TraceLogFileDir";
        private const string MongodDataBlobCacheDir = "MongodDataBlobCacheDir";
        private const string MongodLogBlobCacheDir = "MongodLogBlobCacheDir";

        private const string TraceLogFile = "MongoWorkerTrace.log";

        #endregion constant settings

        private CloudDrive mongoDataDrive = null;
        private CloudDrive mongoLogDrive = null;
        private string mongoHost;
        private int mongoPort;
        private Process mongodProcess = null;
        private TextWriter traceWriter = null;

        public override void Run()
        {
            TraceInformation("MongoWorkerRole entry point called");
            try
            {
                int retryCount = 0;
                mongodProcess.WaitForExit();
                // if here mongod has exited try restart.
                while (retryCount < MaxRetryCount)
                {
                    // Thread.Sleep(SleepBetweenRetry);
                    Thread.Sleep(5);
                    retryCount++;
                }
            }
            catch
            {
                // Thread.Sleep(30 * 60 * 1000);
                Thread.Sleep(5);
            }
        }

        public override bool OnStart()
        {
            InitializeDiagnostics();

            TraceInformation("MongoWorkerRole onstart called");
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;
            CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) =>
            {
                configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));
            });

            SetHostAndPort();
            TraceInformation(string.Format("Obtained host={0}, port={1}", mongoHost, mongoPort));

            try
            {
                // this try block should not be on the final code
                StartMongoD();

                try
                {
                    MongoHelper.MarkStart(mongoHost, mongoPort);
                }
                catch (Exception e)
                {
                    TraceWarning("In run mongod connect exception");
                    TraceWarning(e.Message);
                    TraceWarning(e.StackTrace);
                }
            }
            catch (Exception e)
            {
                TraceWarning("StartMongoD exception");
                TraceWarning(e.Message);
                TraceWarning(e.StackTrace);
            }
            return base.OnStart();
        }

        public override void OnStop()
        {
            TraceInformation("MongoWorkerRole onstop called");
            try
            {
                // should we instead call Process.stop?
                MongoHelper.ShutdownMongo(mongoHost, mongoPort);
                // sleep for 15 seconds to allow for shutdown before unmount
                Thread.Sleep(15000);
            }
            catch (Exception e)
            {
                //Ignore exceptions caught on unmount
                TraceWarning("Exception in onstop - mongo shutdown");
                TraceWarning(e.Message);
                TraceWarning(e.StackTrace);
            }
            try
            {
                mongoDataDrive.Unmount();
            }
            catch (Exception e)
            {
                //Ignore exceptions caught on unmount
                TraceWarning("Exception in onstop - unmount of data drive");
                TraceWarning(e.Message);
                TraceWarning(e.StackTrace);
            }
            try
            {
                mongoLogDrive.Unmount();
            }
            catch (Exception e)
            {
                //Ignore exceptions caught on unmount
                TraceWarning("Exception in onstop - unmount of log drive");
                TraceWarning(e.Message);
                TraceWarning(e.StackTrace);
            }
            ShutdownDiagnostics();
            base.OnStop();
        }

        private void StartMongoD()
        {
            var mongoAppRoot = Path.Combine(
                Environment.GetEnvironmentVariable("RoleRoot"),
                MongoBinaryFolder);

            var blobPath = GetDBPath();

            var logFile = GetLogFile();

            var cmdline = String.Format(MongodCommandLine,
                blobPath,
                mongoPort,
                logFile);
            TraceInformation(string.Format("Launching mongo with cmdline {0}", cmdline));

            // launch mongo
            try
            {
                mongodProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo(Path.Combine(mongoAppRoot, @"mongod.exe"), cmdline)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = mongoAppRoot,
                        CreateNoWindow = false
                    }
                };
                mongodProcess.Start();
            }
            catch (Exception e)
            {

                TraceError("Can't start Mongo: " + e.Message);
                throw new Exception("Can't start mongo: " + e.Message); // throwing an exception here causes the VM to recycle
            }
        }

        private string GetDBPath()
        {
            TraceInformation("Getting db path");
            var path = GetMountedPathFromBlob(
                MongoLocalDataDir,
                MongoCloudDataDir,
                MongodDataBlobContainerName,
                MongodDataBlobName,
                out mongoDataDrive
                );
            TraceInformation(string.Format("Obtained data path as {0}", path));
            return path;
        }

        private string GetLogFile()
        {
            TraceInformation("Getting log file base path");
            var path = GetMountedPathFromBlob(
                MongoLocalLogDir,
                MongoCloudLogDir,
                MongodLogBlobContainerName,
                MongodLogBlobName,
                out mongoLogDrive
                );
            var logfile = Path.Combine(path, MongoLogFileName);
            TraceInformation(string.Format("Obtained log file as {0}", logfile));
            return logfile;
        }

        private void SetHostAndPort()
        {
            var endPoint = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints[MongoHelper.MongodPortKey].IPEndpoint;
            mongoHost = endPoint.Address.ToString();
            mongoPort = endPoint.Port;
        }

        private string GetMountedPathFromBlob(
            string localCachePath,
            string cloudDir,
            string containerName,
            string blobName,
            out CloudDrive mongoDrive)
        {
            var localStorage = RoleEnvironment.GetLocalResource(localCachePath);

            TraceInformation("Initialize cache");
            CloudDrive.InitializeCache(localStorage.RootPath.TrimEnd('\\'),
                localStorage.MaximumSizeInMegabytes);

            var storageAccount = CloudStorageAccount.FromConfigurationSetting(cloudDir);
            var blobClient = storageAccount.CreateCloudBlobClient();

            TraceInformation("Get container");
            // this should be the name of your replset
            var driveContainer = blobClient.GetContainerReference(containerName);

            // create blob container (it has to exist before creating the cloud drive)
            try
            {
                driveContainer.CreateIfNotExist();
            }
            catch (Exception e)
            {
                TraceInformation("Exception when creating container");
                TraceInformation(e.Message);
                TraceInformation(e.StackTrace);
            }

            var mongoBlobUri = blobClient.GetContainerReference(containerName).GetPageBlobReference(blobName).Uri.ToString();
            TraceInformation(string.Format("Blob uri obtained {0}", mongoBlobUri));

            // create the cloud drive
            mongoDrive = storageAccount.CreateCloudDrive(mongoBlobUri);
            try
            {
                mongoDrive.Create(localStorage.MaximumSizeInMegabytes);
            }
            catch (Exception e)
            {
                // exception is thrown if all is well but the drive already exists
                TraceInformation("Exception when creating cloud drive. safe to ignore");
                TraceInformation(e.Message);
                TraceInformation(e.StackTrace);

            }

            // mount the drive and get the root path of the drive it's mounted as
            try
            {
                var driveLetter = mongoDrive.Mount(localStorage.MaximumSizeInMegabytes,
                    DriveMountOptions.None);
                //                    DriveMountOptions.Force);
                TraceInformation(string.Format("Write lock acquired on azure drive, mounted as {0}",
                    driveLetter));
                return driveLetter;
                // return Path.Combine(driveLetter, @"\");
                // return localStorage.RootPath;
            }
            catch (Exception e)
            {
                TraceWarning("could not acquire blob lock.");
                TraceWarning(e.Message);
                TraceWarning(e.StackTrace);
                throw;
            }

        }

        private void InitializeDiagnostics()
        {
            var diagObj = DiagnosticMonitor.GetDefaultInitialConfiguration();
            diagObj.Logs.ScheduledTransferPeriod = DiagnosticTransferInterval;
            diagObj.Logs.ScheduledTransferLogLevelFilter = LogLevel.Verbose;
            DiagnosticMonitor.Start("DiagnosticsConnectionString", diagObj);

            var localStorage = RoleEnvironment.GetLocalResource(MongoTraceDir);
            var fileName = Path.Combine(localStorage.RootPath, TraceLogFile);
            traceWriter = new StreamWriter(fileName);
        }

        private void ShutdownDiagnostics()
        {
            if (traceWriter != null)
            {
                try
                {
                    traceWriter.Close();
                }
                catch
                {
                    // ignore exceptions on close.
                }
            }
        }

        #region Trace wrappers

        private void TraceInformation(string message)
        {
            Trace.TraceInformation(message);
            WriteTraceMessage(message, "INFORMATION");
        }

        private void TraceWarning(string message)
        {
            Trace.TraceWarning(message);
            WriteTraceMessage(message, "WARNING");
        }

        private void TraceError(string message)
        {
            Trace.TraceError(message);
            WriteTraceMessage(message, "ERRROR");
        }

        private void WriteTraceMessage(string message, string type)
        {
            if (traceWriter != null)
            {
                try
                {
                    var messageString = string.Format("{0}-{1}-{2}", DateTime.UtcNow.ToString(), type, message);
                    traceWriter.WriteLine(messageString);
                    traceWriter.Flush();
                }
                catch
                {
                    // ignore trace messages
                }
            }
        }

        #endregion Trace wrappers

    }
}

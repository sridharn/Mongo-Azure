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

    public class MongoWorkerRole : RoleEntryPoint
    {

        private const string MongoBlobContainerName = "mongodrive";
        private const string MongoBlobName = "mongoblob33.vhd";
        private const string MongoCloudDataDir = "MongoDBDataDir";
        private const string MongoLocalDataDir = "MongoDBLocalDataDir";
        private const string MongoLocalLogDir = "MongoDBLocalLogDir";
        private const string MongoBinaryFolder = @"approot\MongoExe";
        private const string MongoLogFileName = "mongod.log";
        private const string MongodCommandLine = "--dbpath {0} --port {1} --journal --nohttpinterface ";


        private static CloudDrive mongoDrive = null;
        private static string mongoHost;
        private static int mongoPort;

        public override void Run()
        {
            Trace.TraceInformation("MongoWorkerRole entry point called");

            while (true)
            {
                Thread.Sleep(10000);
                Trace.TraceInformation("Working");
            }
        }

        public override bool OnStart()
        {
            Trace.TraceInformation("MongoWorkerRole onstart called");
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;
            CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) =>
            {
                configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));
            });
            SetHostAndPort();
            Trace.TraceInformation(string.Format("Obtained host={0}, port={1}", mongoHost, mongoPort));
            StartMongoD();

            try
            {
                MongoHelper.MarkStart(mongoHost, mongoPort);
            }
            catch (Exception e)
            {
                Trace.TraceWarning("In run mongod connect exception");
                Trace.TraceWarning(e.Message);
                Trace.TraceWarning(e.StackTrace);
            }
            return base.OnStart();
        }

        public override void OnStop()
        {
            Trace.TraceInformation("MongoWorkerRole onstop called");
            try
            {
                MongoHelper.ShutdownMongo(mongoHost, mongoPort);
                // sleep for 15 seconds to allow for shutdown before unmount
                Thread.Sleep(15000);
            }
            catch (Exception e)
            {
                //Ignore exceptions caught on unmount
                Trace.TraceWarning("Exception in onstop - mongo shutdown");
                Trace.TraceWarning(e.Message);
                Trace.TraceWarning(e.StackTrace);
            }
            try
            {
                mongoDrive.Unmount();
            }
            catch (Exception e)
            {
                //Ignore exceptions caught on unmount
                Trace.TraceWarning("Exception in onstop - unmount");
                Trace.TraceWarning(e.Message);
                Trace.TraceWarning(e.StackTrace);
            }
            base.OnStop();
        }

        private void StartMongoD()
        {
            var mongoAppRoot = Path.Combine(
                Environment.GetEnvironmentVariable("RoleRoot"),
                MongoBinaryFolder);

            var blobPath = GetBlobPath();

            var cmdline = String.Format(MongodCommandLine,
                blobPath,
                mongoPort);
            Process mongodProcess;
            Trace.TraceInformation(string.Format("Launching mongo with cmdline {0}", cmdline));
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

                Trace.TraceError("Can't start Mongo: " + e.Message);
                throw new Exception("Can't start mongo: " + e.Message); // throwing an exception here causes the VM to recycle
            }
        }

        private string GetBlobPath()
        {
            var instanceName = RoleEnvironment.CurrentRoleInstance.Id;
            var localStorage =  RoleEnvironment.GetLocalResource(MongoLocalDataDir);

            Trace.TraceInformation("Initialize cache");
            CloudDrive.InitializeCache(localStorage.RootPath.TrimEnd('\\'), 
                localStorage.MaximumSizeInMegabytes);

            var storageAccount = CloudStorageAccount.FromConfigurationSetting(MongoCloudDataDir);
            var blobClient = storageAccount.CreateCloudBlobClient();

            Trace.TraceInformation("Get container");
            // this should be the name of your replset
            var driveContainer = blobClient.GetContainerReference(MongoBlobContainerName);

            // create blob container (it has to exist before creating the cloud drive)
            try 
            { 
                driveContainer.CreateIfNotExist(); 
            }
            catch (Exception e)
            {
                Trace.TraceInformation("Exception when creating container");
                Trace.TraceInformation(e.Message);
                Trace.TraceInformation(e.StackTrace);
            }

            var mongoBlobUri = blobClient.GetContainerReference(MongoBlobContainerName).GetPageBlobReference(MongoBlobName).Uri.ToString();
            Trace.TraceInformation(string.Format("Blob uri obtained {0}", mongoBlobUri));

            // create the cloud drive
            mongoDrive = storageAccount.CreateCloudDrive(mongoBlobUri);
            try
            {
                mongoDrive.Create(localStorage.MaximumSizeInMegabytes);
            }
            catch (Exception e)
            {
                // exception is thrown if all is well but the drive already exists
                Trace.TraceInformation("Exception when creating cloud drive. safe to ignore");
                Trace.TraceInformation(e.Message);
                Trace.TraceInformation(e.StackTrace);

            }

            // mount the drive and get the root path of the drive it's mounted as
            try
            {
                var driveLetter = mongoDrive.Mount(localStorage.MaximumSizeInMegabytes,
                    DriveMountOptions.Force);
                Trace.TraceInformation("Write lock acquired on azure drive, mounted as {0}", 
                    driveLetter);
                return driveLetter;
                // return localStorage.RootPath;
            }
            catch (Exception e)
            {
                Trace.TraceWarning("could not acquire blob lock.");
                Trace.TraceWarning(e.Message);
                Trace.TraceWarning(e.StackTrace);
                throw;
            }
    
        }

        //private string GetMongoLogFile()
        //{
        //    var logDiwr = RoleEnvironment.GetLocalResource(MongoLocalLogDir);
        //    var logfile = Path.Combine(logDir.RootPath, MongoLogFileName);
        //    var file = new FileStream(logfile, FileMode.Append);
        //    return logfile;
        //}

        private static void SetHostAndPort()
        {
            var endPoint = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints[MongoHelper.MongodPortKey].IPEndpoint;
            mongoHost = endPoint.Address.ToString();
            mongoPort = endPoint.Port;
        }
    }
}

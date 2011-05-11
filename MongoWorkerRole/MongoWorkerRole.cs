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

        private static CloudDrive MongoDrive = null;
        private static string mongoHost;
        private static int mongoPort;

        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.WriteLine("MongoWorkerRole entry point called", "Information");

            while (true)
            {
                Thread.Sleep(10000);
                Trace.WriteLine("Working", "Information");
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;
            CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) =>
            {
                configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));
            });
            SetHostAndPort();
            StartMongo();
            MongoHelper.MarkStart(mongoHost, mongoPort);

            return base.OnStart();
        }

        public override void OnStop()
        {
            MongoHelper.ShutdownMongo(mongoHost, mongoPort);
            // sleep for 15 seconds to allow for shutdown before unmount
            Thread.Sleep(15000);
            try
            {
                MongoDrive.Unmount();
            }
            catch 
            { 
                //Ignore exceptions caught on unmount
            }
            base.OnStop();
        }

        private void StartMongo()
        {
            var mongoAppRoot = Path.Combine(
                Environment.GetEnvironmentVariable("RoleRoot"),
                MongoBinaryFolder);

            var blobPath = GetBlobPath();
            var logFile = GetMongoLogFile();

            var cmdline = String.Format(MongodCommandLine,
                blobPath,
                mongoPort);
            Process mongodProcess;

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
            CloudDrive.InitializeCache(localStorage.RootPath.TrimEnd('\\'), 
                localStorage.MaximumSizeInMegabytes);

            var storageAccount = CloudStorageAccount.FromConfigurationSetting(MongoCloudDataDir);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var driveContainer = blobClient.GetContainerReference(MongoBlobContainerName);

            // create blob container (it has to exist before creating the cloud drive)
            try { driveContainer.CreateIfNotExist(); }
            catch { }

            // get the url to the vhd page blob we'll be using
            // var vhdName = String.Format("{0}.vhd", instanceName);
            var mongoBlob = blobClient.GetContainerReference(MongoBlobContainerName).GetPageBlobReference(MongoBlobName).Uri.ToString();

            // create the cloud drive
            MongoDrive = storageAccount.CreateCloudDrive(mongoBlob);
            try
            {
                MongoDrive.Create(localStorage.MaximumSizeInMegabytes);
            }
            catch
            {
                // exception is thrown if all is well but the drive already exists
            }

            // mount the drive and get the root path of the drive it's mounted as
            try
            {
                var driveLetter = MongoDrive.Mount(localStorage.MaximumSizeInMegabytes,
                    DriveMountOptions.Force);
                Trace.TraceInformation("Write lock acquired on azure drive, mounted as {0}", 
                    driveLetter);
                return driveLetter;
                // return localStorage.RootPath;
            }
            catch 
            {

                Trace.TraceInformation("could not acquire blob lock.");
                return localStorage.RootPath;
            }
    
        }

        private string GetMongoLogFile()
        {
            var logDir = RoleEnvironment.GetLocalResource(MongoLocalLogDir);
            var logfile = Path.Combine(logDir.RootPath, MongoLogFileName);
            var file = new FileStream(logfile, FileMode.Append);
            return logfile;
        }


        private static void SetHostAndPort()
        {
            var endPoint = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints[MongoHelper.MongodPortKey].IPEndpoint;
            mongoHost = endPoint.Address.ToString();
            mongoPort = endPoint.Port;
        }
    }
}

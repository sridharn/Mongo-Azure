## MongoDB Azure Single Server Integration ## 
Welcome to MongoDB on Azure!. This is a prototype to have a single instance of MongoDB 1.8.1 working on Azure. If 2 instances of MongoWorkerRole are chosen the 2nd instance acts as a warm standby ready to takeover if the 1st is shutdown or dies.

## Components ## 
  * MongoAzure - DLL containing helper methods to access MongoDB in an Azure environment
  * MongoWorkerRole - An Azure worker role that launches mongod
  * MvcMovie  - An Azure web role that is a sample app that demonstrates connecting and working with Mongo on Azure
  * Mongo binaries  - MongoDB 1.8.1 and official 10gen C# driver 1.1 (prerelease)

## Building ##
### Prerequisites ###
  * .Net 4.0.
  * Windows Azure SDK 1.4 
  * Visual Studio 2010

### Build ###
  * Open MongoAzure.sln from Visual Studio 2010 and build

## Deploying, Running and Debugging ##

### In a development environment ###
The solution should be built and run in a development environment as is if using Visual Studio 2010 Ultimate Edition if adequate disk space is available. 

  * Since this uses Cloud Drive you cannot run from a development against Cloud storage
  * When running in the dev environment a trace file is written to C:\Users\<user>\AppData\Local\dftmp\s0\deployment(<deployment number>)\res\deployment(<deployment number>).MongoWorker.MongoWorkerRole.0\directory\MongoTraceDir
  * The actual mongod log file is at C:\Users\<user>\AppData\Local\dftmp\wadd\devstoreaccount1\mongodlogdrive\mongodlblob.vhd. Note - On a development environment the port is not honored and hence you need to look at the long file to know which port mongo is listening on. This drive is also mounted and can be accessed from Windows explorer while the app is running.
  * The actual mongod data files are at C:\Users\<user>\AppData\Local\dftmp\wadd\devstoreaccount1\mongoddatadrive\mongoddblob.vhd. This drive is also mounted and can be accessed from windows explorer while the app is running. 

### In the Azure environment ###
* Change connection setting from UseDevelopment storage to actual storage account credentials
* Ensure that the connection mode is https for DiagnosticStorage and http for the data and log directories
* Ensure instance size for MongoWorkerRole is at least Medium
* Set the number of MongoWorkerRole instances to 2 if Warm standby is needed 

## Maintainers
* Sridhar Nanjundeswaran       sridhar@10gen.com

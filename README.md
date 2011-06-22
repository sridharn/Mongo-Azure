## MongoDB Azure Single Server Integration ## 
Welcome to MongoDB on Azure!. This is a prototype to have a single instance of MongoDB 1.8.1 working on Azure. If 2 instances of MongoWorkerRole are chosen the 2nd instance acts as a warm standby ready to takeover if the 1st is shutdown or dies.

## Components ## 
  * MongoAzure - DLL containing helper methods to access MongoDB in an Azure environment
  * MongoWorkerRole - An Azure worker role that launches mongod
  * MvcMovie  - An Azure web role that is a sample app that demonstrates connecting and working with Mongo on Azure
  * Mongo binaries  - MongoDB 1.8.1 and official 10gen C# driver 1.1

## Building ##
### Prerequisites ###
  * .Net 4.0.
  * Windows Azure SDK 1.4 
  * Visual Studio 2010

### Build ###
  * Open MongoAzure.sln from Visual Studio 2010 and build

## Doc page
  * http://www.mongodb.org/display/DOCS/MongoDB+on+Azure 

## Maintainers
* Sridhar Nanjundeswaran       sridhar@10gen.com

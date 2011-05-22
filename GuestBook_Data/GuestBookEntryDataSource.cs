// ----------------------------------------------------------------------------------
// Microsoft Developer & Platform Evangelism
// 
// Copyright (c) Microsoft Corporation. All rights reserved.
// 
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
// The example companies, organizations, products, domain names,
// e-mail addresses, logos, people, places, and events depicted
// herein are fictitious.  No association with any real company,
// organization, product, domain name, email address, logo, person,
// places, or events is intended or should be inferred.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;

using MongoDB.Azure;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace GuestBook_Data
{
    public class GuestBookEntryDataSource
    {
        public GuestBookEntryDataSource()
        {
        }

        public IEnumerable<GuestBookEntry> Select()
        {
            var collection = GetEntriesCollection();
            var query = Query.EQ("PartitionKey", DateTime.UtcNow.ToString("MMddyyyy"));
            var cursor = collection.Find(query);
            return cursor;
        }

        public void AddGuestBookEntry(GuestBookEntry newItem)
        {
            var collection = GetEntriesCollection();
            collection.Insert(newItem);
        }

        public void UpdateImageThumbnail(string partitionKey, string rowKey, string thumbUrl)
        {
            var collection = GetEntriesCollection();
            var query = Query.And(
                Query.EQ("PartitionKey", partitionKey),
                Query.EQ("RowKey", rowKey)
            );
            var entry = collection.FindOne(query);
            entry.ThumbnailUrl = thumbUrl;
            collection.Save(entry);
        }

        private MongoCollection<GuestBookEntry> GetEntriesCollection() {
            // var server = MongoServer.Create("mongodb://localhost/?safe=true");
            var server = MongoHelper.GetMongoServer();
            var database = server["guestbook"];
            var entries = database.GetCollection<GuestBookEntry>("entries");
            return entries;
        }
    }
}

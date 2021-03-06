﻿//----------------------------------------------------------------------- 
// PDS WITSMLstudio Store, 2017.1
//
// Copyright 2017 Petrotechnical Data Systems
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//   
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Energistics.Datatypes;
using LinqToQuerystring;
using MongoDB.Driver;
using PDS.WITSMLstudio.Framework;
using PDS.WITSMLstudio.Data.ChangeLogs;

namespace PDS.WITSMLstudio.Store.Data.Transactions
{
    /// <summary>
    /// Data adapter that encapsulates CRUD functionality for a <see cref="MongoDbTransaction"/>
    /// </summary>
    /// <seealso cref="Transactions.DbTransaction" />
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DbTransactionDataAdapter : MongoDbDataAdapter<DbTransaction>
    {
        private const string MongoDbTransaction = "dbTransaction";
        private const string TransactionIdField = "TransactionId";

        /// <summary>
        /// Initializes a new instance of the <see cref="DbTransactionDataAdapter" /> class.
        /// </summary>
        /// <param name="container">The composition container.</param>
        /// <param name="databaseProvider">The database provider.</param>
        [ImportingConstructor]
        public DbTransactionDataAdapter(IContainer container, IDatabaseProvider databaseProvider) : base(container, databaseProvider, MongoDbTransaction, ObjectTypes.Uri)
        {
            Logger.Debug("Creating instance.");
        }

        /// <summary>
        /// Gets a collection of data objects related to the specified URI.
        /// </summary>
        /// <param name="parentUri">The parent URI.</param>
        /// <returns>A collection of data objects.</returns>
        public override List<DbTransaction> GetAll(EtpUri? parentUri = null)
        {            
            var query = GetQuery().AsQueryable();
            var uri = parentUri?.Uri;

            if (!string.IsNullOrWhiteSpace(parentUri?.Query))
            {
                query = query.LinqToQuerystring(parentUri.Value.Query);
                uri = uri.Substring(0, uri.IndexOf('?'));
            }

            query = query.Where(x => x.Uri == uri);

            return query.ToList();
        }

        /// <summary>
        /// Inserts the entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        public void InsertEntities(List<DbTransaction> entities)
        {
            var collection = GetCollection();
            collection.InsertMany(entities);
        }

        /// <summary>
        /// Updates the entities.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="newTransactionId">The new transaction identifier.</param>
        public void UpdateEntities(string transactionId, string newTransactionId)
        {
            Logger.Debug($"Transferring transactions from Transaction ID: {transactionId} to {newTransactionId}");
            var filter = MongoDbUtility.BuildFilter<DbTransaction>(TransactionIdField, transactionId);
            var update = MongoDbUtility.BuildUpdate<DbTransaction>(null, TransactionIdField, newTransactionId);

            var collection = GetCollection();
            collection.UpdateMany(filter, update);
        }

        /// <summary>
        /// Deletes the transactions.
        /// </summary>
        /// <param name="transactionId">The tid.</param>
        public void DeleteTransactions(string transactionId)
        {
            Logger.Debug($"Deleting transactions for Transaction ID: {transactionId}");
            var filter = MongoDbUtility.BuildFilter<DbTransaction>(TransactionIdField, transactionId);

            var collection = GetCollection();
            collection.DeleteMany(filter);
        }

        /// <summary>
        /// Gets the entity filter for the specified URI.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="uri">The URI.</param>
        /// <param name="idPropertyName">Name of the identifier property.</param>
        /// <returns>The entity filter.</returns>
        protected override FilterDefinition<TObject> GetEntityFilter<TObject>(EtpUri uri, string idPropertyName)
        {
            return MongoDbUtility.BuildFilter<TObject>(idPropertyName, uri.ToString());
        }

        /// <summary>
        /// Audits the entity. Override this method to adjust the audit record
        /// before it is submitted to the database or to prevent the audit.
        /// </summary>
        /// <param name="entity">The changed entity.</param>
        /// <param name="auditHistory">The audit history.</param>
        /// <param name="exists">if set to <c>true</c> the entry exists.</param>
        protected override void AuditEntity(DbTransaction entity, DbAuditHistory auditHistory, bool exists)
        {
            // Excluding DbTransaction from audit history
        }
    }
}

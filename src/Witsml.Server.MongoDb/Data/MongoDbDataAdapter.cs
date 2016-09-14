﻿//----------------------------------------------------------------------- 
// PDS.Witsml.Server, 2016.1
//
// Copyright 2016 Petrotechnical Data Systems
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

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Energistics.DataAccess;
using Energistics.DataAccess.Validation;
using Energistics.DataAccess.WITSML200;
using Energistics.Datatypes;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using PDS.Framework;
using PDS.Witsml.Server.Configuration;
using PDS.Witsml.Server.Data.Transactions;

namespace PDS.Witsml.Server.Data
{
    /// <summary>
    /// MongoDb data adapter that encapsulates CRUD functionality for WITSML objects.
    /// </summary>
    /// <typeparam name="T">Type of the data object</typeparam>
    /// <seealso cref="Data.WitsmlDataAdapter{T}" />
    public abstract class MongoDbDataAdapter<T> : WitsmlDataAdapter<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MongoDbDataAdapter{T}" /> class.
        /// </summary>
        /// <param name="container">The composition container.</param>
        /// <param name="databaseProvider">The database provider.</param>
        /// <param name="dbCollectionName">The database collection name.</param>
        /// <param name="idPropertyName">The name of the identifier property.</param>
        /// <param name="namePropertyName">The name of the object name property</param>
        protected MongoDbDataAdapter(IContainer container, IDatabaseProvider databaseProvider, string dbCollectionName, string idPropertyName = ObjectTypes.Uid, string namePropertyName = ObjectTypes.NameProperty)
            : base(container)
        {
            DatabaseProvider = databaseProvider;
            DbCollectionName = dbCollectionName;
            IdPropertyName = idPropertyName;
            NamePropertyName = namePropertyName;
        }

        /// <summary>
        /// Gets the database provider used for accessing MongoDb.
        /// </summary>
        /// <value>The database provider.</value>
        protected IDatabaseProvider DatabaseProvider { get; private set; }

        /// <summary>
        /// Gets the database collection name for the data object.
        /// </summary>
        /// <value>The database collection name.</value>
        protected string DbCollectionName { get; private set; }

        /// <summary>
        /// Gets the name of the identifier property.
        /// </summary>
        /// <value>The name of the identifier property.</value>
        protected string IdPropertyName { get; private set; }

        /// <summary>
        /// Gets the name of the Name property.
        /// </summary>
        /// <value>The name of the Name property.</value>
        protected string NamePropertyName { get; private set; }

        /// <summary>
        /// Gets a data object by the specified UUID.
        /// </summary>
        /// <param name="uri">The data object URI.</param>
        /// <returns>The data object instance.</returns>
        public override T Get(EtpUri uri)
        {
            return GetEntity(uri);
        }

        /// <summary>
        /// Retrieves data objects from the data store using the specified parser.
        /// </summary>
        /// <param name="parser">The query template parser.</param>
        /// <param name="context">The response context.</param>
        /// <returns>
        /// A collection of data objects retrieved from the data store.
        /// </returns>
        public override List<T> Query(WitsmlQueryParser parser, ResponseContext context)
        {
            return QueryEntities(parser);
        }

        /// <summary>
        /// Adds a data object to the data store.
        /// </summary>
        /// <param name="parser">The input template parser.</param>
        /// <param name="dataObject">The data object to be added.</param>
        public override void Add(WitsmlQueryParser parser, T dataObject)
        {
            using (var transaction = DatabaseProvider.BeginTransaction())
            {
                InsertEntity(dataObject, transaction);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Updates a data object in the data store.
        /// </summary>
        /// <param name="parser">The input template parser.</param>
        /// <param name="dataObject">The data object to be updated.</param>
        public override void Update(WitsmlQueryParser parser, T dataObject)
        {
            var uri = GetUri(dataObject);
            using (var transaction = DatabaseProvider.BeginTransaction(uri))
            {
                UpdateEntity(parser, uri, transaction);
                ValidateUpdatedEntity(Functions.UpdateInStore, uri);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Replaces a data object in the data store.
        /// </summary>
        /// <param name="parser">The input template parser.</param>
        /// <param name="dataObject">The data object to be replaced.</param>
        public override void Replace(WitsmlQueryParser parser, T dataObject)
        {
            var uri = GetUri(dataObject);
            using (var transaction = DatabaseProvider.BeginTransaction(uri))
            {
                ReplaceEntity(dataObject, uri, transaction);
                ValidateUpdatedEntity(Functions.PutObject, uri);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Deletes or partially updates the specified object by uid.
        /// </summary>
        /// <param name="parser">The query parser that specifies the object.</param>
        public override void Delete(WitsmlQueryParser parser)
        {
            var uri = parser.GetUri<T>();

            if (parser.HasElements())
            {
                using (var transaction = DatabaseProvider.BeginTransaction(uri))
                {
                    PartialDeleteEntity(parser, uri, transaction);
                    transaction.Commit();
                }
            }
            else
            {
                Delete(uri);
            }
        }

        /// <summary>
        /// Deletes a data object by the specified identifier.
        /// </summary>
        /// <param name="uri">The data object URI.</param>
        public override void Delete(EtpUri uri)
        {
            using (var transaction = DatabaseProvider.BeginTransaction(uri))
            {
                DeleteEntity(uri, transaction);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Determines whether the entity exists in the data store.
        /// </summary>
        /// <param name="uri">The data object URI.</param>
        /// <returns>true if the entity exists; otherwise, false</returns>
        public override bool Exists(EtpUri uri)
        {
            return Exists<T>(uri, DbCollectionName);
        }

        /// <summary>
        /// Determines whether the entity exists in the data store.
        /// </summary>
        /// <param name="uri">The data object URI.</param>
        /// <param name="dbCollectionName">The name of the database collection.</param>
        /// <typeparam name="TObject">The data object type.</typeparam>
        /// <returns>true if the entity exists; otherwise, false</returns>
        protected bool Exists<TObject>(EtpUri uri, string dbCollectionName)
        {
            try
            {
                return GetEntity<TObject>(uri, dbCollectionName) != null;
            }
            catch (MongoException ex)
            {
                Logger.Error("Error querying " + dbCollectionName, ex);
                throw new WitsmlException(ErrorCodes.ErrorReadingFromDataStore, ex);
            }
        }

        /// <summary>
        /// Gets the default collection.
        /// </summary>
        /// <returns></returns>
        protected IMongoCollection<T> GetCollection()
        {
            return GetCollection<T>(DbCollectionName);
        }

        /// <summary>
        /// Gets the collection having the specified name.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="dbCollectionName">Name of the database collection.</param>
        /// <returns></returns>
        protected IMongoCollection<TObject> GetCollection<TObject>(string dbCollectionName)
        {
            var database = DatabaseProvider.GetDatabase();
            return database.GetCollection<TObject>(dbCollectionName);
        }

        /// <summary>
        /// Gets an <see cref="IQueryable{T}"/> instance for the default collection.
        /// </summary>
        /// <returns>An executable query.</returns>
        protected IMongoQueryable<T> GetQuery()
        {
            return GetQuery<T>(DbCollectionName);
        }

        /// <summary>
        /// Gets an <see cref="IQueryable{TObject}"/> instance for the specified collection.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="dbCollectionName">Name of the database collection.</param>
        /// <returns>An executable query.</returns>
        protected IMongoQueryable<TObject> GetQuery<TObject>(string dbCollectionName)
        {
            return GetCollection<TObject>(dbCollectionName).AsQueryable();
        }

        /// <summary>
        /// Gets an object from the data store by uid
        /// </summary>
        /// <param name="uri">The data object URI.</param>
        /// <returns>The object represented by the UID.</returns>
        protected T GetEntity(EtpUri uri)
        {
            return GetEntity<T>(uri, DbCollectionName);
        }

        /// <summary>
        /// Gets an object from the data store by uid
        /// </summary>
        /// <param name="uri">The data object URI.</param>
        /// <param name="dbCollectionName">The naame of the database collection.</param>
        /// <typeparam name="TObject">The data object type.</typeparam>
        /// <returns>The entity represented by the indentifier.</returns>
        protected TObject GetEntity<TObject>(EtpUri uri, string dbCollectionName)
        {
            try
            {
                Logger.DebugFormat("Querying {0} MongoDb collection; uid: {1}", dbCollectionName, uri.ObjectId);

                var filter = GetEntityFilter<TObject>(uri, IdPropertyName);

                return GetCollection<TObject>(dbCollectionName)
                    .Find(filter)
                    .FirstOrDefault();
            }
            catch (MongoException ex)
            {
                Logger.ErrorFormat("Error querying {0} MongoDb collection:{1}{2}", dbCollectionName, Environment.NewLine, ex);
                throw new WitsmlException(ErrorCodes.ErrorReadingFromDataStore, ex);
            }
        }

        /// <summary>
        /// Gets the entity filter for the specified URI.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="uri">The URI.</param>
        /// <param name="idPropertyName">Name of the identifier property.</param>
        /// <returns>The entity filter.</returns>
        protected virtual FilterDefinition<TObject> GetEntityFilter<TObject>(EtpUri uri, string idPropertyName)
        {
            return MongoDbUtility.GetEntityFilter<TObject>(uri, idPropertyName);
        }

        /// <summary>
        /// Gets the entities having the specified URIs.
        /// </summary>
        /// <param name="uris">The uris.</param>
        /// <returns>The query results.</returns>
        protected List<T> GetEntities(IEnumerable<EtpUri> uris)
        {
            return GetEntities<T>(uris, DbCollectionName);
        }

        /// <summary>
        /// Gets the entities having the supplied URIs found in the specified collection.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="uris">The uris.</param>
        /// <param name="dbCollectionName">Name of the database collection.</param>
        /// <returns>The query results.</returns>
        protected List<TObject> GetEntities<TObject>(IEnumerable<EtpUri> uris, string dbCollectionName)
        {
            var list = uris.ToList();

            Logger.DebugFormat("Querying {0} MongoDb collection by URIs: {1}{2}",
                dbCollectionName,
                Environment.NewLine,
                Logger.IsDebugEnabled ? string.Join(Environment.NewLine, list) : null);

            if (!list.Any())
            {
                return GetCollection<TObject>(dbCollectionName)
                    .Find("{}")
                    .ToList();
            }

            var filters = list.Select(x => MongoDbUtility.GetEntityFilter<TObject>(x, IdPropertyName));

            return GetCollection<TObject>(dbCollectionName)
                .Find(Builders<TObject>.Filter.Or(filters))
                .ToList();
        }

        /// <summary>
        /// Queries the data store with Mongo Bson filter and projection.
        /// </summary>
        /// <param name="parser">The parser.</param>
        /// <returns>The query results collection.</returns>
        /// <exception cref="WitsmlException"></exception>
        protected List<T> QueryEntities(WitsmlQueryParser parser)
        {
            try
            {
                if (OptionsIn.RequestObjectSelectionCapability.True.Equals(parser.RequestObjectSelectionCapability()))
                {
                    Logger.DebugFormat("Requesting {0} query template.", DbCollectionName);
                    var queryTemplate = CreateQueryTemplate();
                    return queryTemplate.AsList();
                }

                var returnElements = parser.ReturnElements();
                Logger.DebugFormat("Querying with return elements '{0}'", returnElements);

                var fields = GetProjectionPropertyNames(parser);
                var ignored = GetIgnoredElementNamesForQuery(parser);

                Logger.DebugFormat("Querying {0} MongoDb collection.", DbCollectionName);
                var query = new MongoDbQuery<T>(Container, GetCollection(), parser, fields, ignored);
                return query.Execute();
            }
            catch (MongoException ex)
            {
                Logger.ErrorFormat("Error querying {0} MongoDb collection: {1}", DbCollectionName, ex);
                throw new WitsmlException(ErrorCodes.ErrorReadingFromDataStore, ex);
            }
        }

        /// <summary>
        /// Inserts an object into the data store.
        /// </summary>
        /// <param name="entity">The object to be inserted.</param>
        /// <param name="transaction">The transaction.</param>
        protected void InsertEntity(T entity, MongoTransaction transaction = null)
        {
            InsertEntity(entity, DbCollectionName, GetUri(entity), transaction);
        }

        /// <summary>
        /// Inserts an object into the data store.
        /// </summary>
        /// <typeparam name="TObject">The data object type.</typeparam>
        /// <param name="entity">The object to be inserted.</param>
        /// <param name="dbCollectionName">The name of the database collection.</param>
        /// <param name="uri">The data object URI.</param>
        /// <param name="transaction">The transaction.</param>
        /// <exception cref="WitsmlException"></exception>
        protected void InsertEntity<TObject>(TObject entity, string dbCollectionName, EtpUri uri, MongoTransaction transaction = null)
        {
            try
            {
                Logger.DebugFormat("Inserting into {0} MongoDb collection.", dbCollectionName);

                var collection = GetCollection<TObject>(dbCollectionName);
                collection.InsertOne(entity);
                
                if (transaction != null)
                {
                    transaction.Attach(MongoDbAction.Add, dbCollectionName, null, uri);
                    transaction.Save();
                }               
            }
            catch (MongoException ex)
            {
                Logger.ErrorFormat("Error inserting into {0} MongoDb collection:{1}{2}", dbCollectionName, Environment.NewLine, ex);
                throw new WitsmlException(ErrorCodes.ErrorAddingToDataStore, ex);
            }
        }

        /// <summary>
        /// Updates an object in the data store.
        /// </summary>
        /// <param name="parser">The WITSML query parser.</param>
        /// <param name="uri">The data object URI.</param>
        /// <param name="transaction">The transaction.</param>
        protected void UpdateEntity(WitsmlQueryParser parser, EtpUri uri, MongoTransaction transaction = null)
        {
            UpdateEntity<T>(DbCollectionName, parser, uri, transaction);
        }

        /// <summary>
        /// Updates an object in the data store.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="dbCollectionName">The name of the database collection.</param>
        /// <param name="parser">The WITSML query parser.</param>
        /// <param name="uri">The data object URI.</param>
        /// <param name="transaction">The transaction.</param>
        /// <exception cref="WitsmlException"></exception>
        protected void UpdateEntity<TObject>(string dbCollectionName, WitsmlQueryParser parser, EtpUri uri, MongoTransaction transaction = null)
        {
            try
            {
                Logger.DebugFormat("Updating {0} MongoDb collection", dbCollectionName);

                var collection = GetCollection<TObject>(dbCollectionName);
                var current = GetEntity<TObject>(uri, dbCollectionName);
                var updates = MongoDbUtility.CreateUpdateFields<TObject>();
                var ignores = MongoDbUtility.CreateIgnoreFields<TObject>(GetIgnoredElementNamesForUpdate(parser));

                var update = new MongoDbUpdate<TObject>(Container, collection, parser, IdPropertyName, ignores);
                update.Update(current, uri, updates);

                if (transaction != null)
                {
                    transaction.Attach(MongoDbAction.Update, dbCollectionName, current.ToBsonDocument(), uri);
                    transaction.Save();
                }
            }
            catch (MongoException ex)
            {
                Logger.ErrorFormat("Error updating {0} MongoDb collection: {1}", dbCollectionName, ex);
                throw new WitsmlException(ErrorCodes.ErrorUpdatingInDataStore, ex);
            }
        }

        /// <summary>
        /// Replaces an object in the data store.
        /// </summary>
        /// <param name="entity">The object to be replaced.</param>
        /// <param name="uri">The data object URI.</param>
        /// <param name="transaction">The transaction.</param>
        protected void ReplaceEntity(T entity, EtpUri uri, MongoTransaction transaction = null)
        {
            ReplaceEntity(DbCollectionName, entity, uri, transaction);
        }

        /// <summary>
        /// Replaces an object in the data store.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="dbCollectionName">The name of the database collection.</param>
        /// <param name="entity">The object to be replaced.</param>
        /// <param name="uri">The data object URI.</param>
        /// <param name="transaction">The transaction.</param>
        /// <exception cref="WitsmlException"></exception>
        protected void ReplaceEntity<TObject>(string dbCollectionName, TObject entity, EtpUri uri, MongoTransaction transaction = null)
        {
            try
            {
                Logger.DebugFormat("Replacing {0} MongoDb collection", dbCollectionName);

                var collection = GetCollection<TObject>(dbCollectionName);
                var current = GetEntity<TObject>(uri, dbCollectionName);
                //var updates = MongoDbUtility.CreateUpdateFields<TObject>();
                //var ignores = MongoDbUtility.CreateIgnoreFields<TObject>(GetIgnoredElementNamesForUpdate(parser));

                //var update = new MongoDbUpdate<TObject>(Container, collection, parser, IdPropertyName, ignores);
                //update.Update(current, uri, updates);

                var filter = GetEntityFilter<TObject>(uri, IdPropertyName);
                collection.ReplaceOne(filter, entity);

                if (transaction != null)
                {
                    transaction.Attach(MongoDbAction.Update, dbCollectionName, current.ToBsonDocument(), uri);
                    transaction.Save();
                }
            }
            catch (MongoException ex)
            {
                Logger.ErrorFormat("Error replacing {0} MongoDb collection: {1}", dbCollectionName, ex);
                throw new WitsmlException(ErrorCodes.ErrorReplacingInDataStore, ex);
            }
        }

        /// <summary>
        /// Merges the entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="parser">The WITSML query parser.</param>
        /// <param name="transaction">The transaction.</param>
        protected void MergeEntity(T entity, WitsmlQueryParser parser, MongoTransaction transaction = null)
        {
            MergeEntity(DbCollectionName, entity, parser, transaction);
        }

        /// <summary>
        /// Merges the entity.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="dbCollectionName">Name of the database collection.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="parser">The WITSML query parser.</param>
        /// <param name="transaction">The transaction.</param>
        protected void MergeEntity<TObject>(string dbCollectionName, TObject entity, WitsmlQueryParser parser, MongoTransaction transaction = null)
        {
            try
            {
                Logger.DebugFormat($"Merging {dbCollectionName} MongoDb collection");

                var collection = GetCollection<TObject>(dbCollectionName);
                var merge = new MongoDbMerge<TObject>(Container, collection, parser, IdPropertyName);
                merge.Merge(entity);
            }
            catch (MongoException ex)
            {
                Logger.ErrorFormat("Error replacing {0} MongoDb collection: {1}", dbCollectionName, ex);
                throw new WitsmlException(ErrorCodes.ErrorReplacingInDataStore, ex);
            }
        }

        /// <summary>
        /// Deletes a data object by the specified identifier.
        /// </summary>
        /// <param name="uri">The data object URI.</param>
        /// <param name="transaction">The transaction.</param>
        /// <exception cref="WitsmlException"></exception>
        protected void DeleteEntity(EtpUri uri, MongoTransaction transaction = null)
        {
            DeleteEntity<T>(uri, DbCollectionName, transaction);
        }

        /// <summary>
        /// Deletes a data object by the specified identifier.
        /// </summary>
        /// <typeparam name="TObject">The type of data object.</typeparam>
        /// <param name="uri">The data object URI.</param>
        /// <param name="dbCollectionName">The name of the database collection.</param>
        /// <param name="transaction">The transaction.</param>
        /// <exception cref="WitsmlException"></exception>
        protected void DeleteEntity<TObject>(EtpUri uri, string dbCollectionName, MongoTransaction transaction = null)
        {
            try
            {
                Logger.DebugFormat("Deleting from {0} MongoDb collection", dbCollectionName);

                var collection = GetCollection<TObject>(dbCollectionName);
                var current = GetEntity<TObject>(uri, dbCollectionName);
                if (current == null)
                    return;

                if (transaction != null)
                {
                    //var document = MongoDbUtility.GetDocumentId(current);
                    transaction.Attach(MongoDbAction.Delete, dbCollectionName, null, uri);
                    transaction.Save();
                }
                else
                {
                    var filter = MongoDbUtility.GetEntityFilter<TObject>(uri, IdPropertyName);
                    collection.DeleteOne(filter);
                }
            }
            catch (MongoException ex)
            {
                Logger.ErrorFormat("Error deleting from {0} MongoDb collection: {1}", dbCollectionName, ex);
                throw new WitsmlException(ErrorCodes.ErrorDeletingFromDataStore, ex);
            }
        }

        /// <summary>
        /// Partials the delete entity.
        /// </summary>
        /// <param name="parser">The parser.</param>
        /// <param name="uri">The URI.</param>
        /// <param name="transaction">The transaction.</param>
        protected void PartialDeleteEntity(WitsmlQueryParser parser, EtpUri uri, MongoTransaction transaction = null)
        {
            PartialDeleteEntity<T>(DbCollectionName, parser, uri, transaction);
        }

        /// <summary>
        /// Partials the delete entity.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="dbCollectionName">Name of the database collection.</param>
        /// <param name="parser">The parser.</param>
        /// <param name="uri">The URI.</param>
        /// <param name="transaction">The transaction.</param>
        /// <exception cref="WitsmlException"></exception>
        protected void PartialDeleteEntity<TObject>(string dbCollectionName, WitsmlQueryParser parser, EtpUri uri, MongoTransaction transaction = null)
        {
            try
            {
                Logger.DebugFormat("Partial Deleting {0} MongoDb collection", dbCollectionName);

                var collection = GetCollection<TObject>(dbCollectionName);
                var current = GetEntity<TObject>(uri, dbCollectionName);
                var updates = MongoDbUtility.CreateUpdateFields<TObject>();
                var ignores = MongoDbUtility.CreateIgnoreFields<TObject>(GetIgnoredElementNamesForUpdate(parser));

                var partialDelete = new MongoDbDelete<TObject>(Container, collection, parser, IdPropertyName, ignores);
                partialDelete.PartialDelete(current, uri, updates);

                if (transaction != null)
                {
                    transaction.Attach(MongoDbAction.Update, dbCollectionName, current.ToBsonDocument(), uri);
                    transaction.Save();
                }
            }
            catch (MongoException ex)
            {
                Logger.ErrorFormat("Error partial deleting {0} MongoDb collection: {1}", dbCollectionName, ex);
                throw new WitsmlException(ErrorCodes.ErrorUpdatingInDataStore, ex);
            }
        }

        /// <summary>
        /// Gets a list of the property names to project during a query.
        /// </summary>
        /// <param name="parser">The WITSML parser.</param>
        /// <returns>A list of property names.</returns>
        protected override List<string> GetProjectionPropertyNames(WitsmlQueryParser parser)
        {
            if (OptionsIn.ReturnElements.IdOnly.Equals(parser.ReturnElements()))
            {
                if (typeof(IWellboreObject).IsAssignableFrom(typeof(T)))
                    return new List<string> { IdPropertyName, NamePropertyName, "UidWell", "NameWell", "UidWellbore", "NameWellbore" };

                if (typeof(IWellObject).IsAssignableFrom(typeof(T)))
                    return new List<string> { IdPropertyName, NamePropertyName, "UidWell", "NameWell" };

                return new List<string> { IdPropertyName, NamePropertyName };
            }

            return null;
        }

        /// <summary>
        /// Validates the updated entity.
        /// </summary>
        /// <param name="function">The WITSML API function.</param>
        /// <param name="uri">The URI.</param>
        protected void ValidateUpdatedEntity(Functions function, EtpUri uri)
        {
            IList<ValidationResult> results;

            var entity = GetEntity(uri);
            DataObjectValidator.TryValidate(entity, out results);
            WitsmlValidator.ValidateResults(function, results);
        }

        private static void ValidateResults(IList<ValidationResult> results)
        {
            if (!results.Any()) return;

            ErrorCodes errorCode;
            var witsmlValidationResult = results.OfType<WitsmlValidationResult>().FirstOrDefault();

            if (witsmlValidationResult != null)
            {
                throw new WitsmlException((ErrorCodes)witsmlValidationResult.ErrorCode);
            }

            if (Enum.TryParse(results.First().ErrorMessage, out errorCode))
            {
                throw new WitsmlException(errorCode);
            }

            throw new WitsmlException(ErrorCodes.InputTemplateNonConforming,
                string.Join("; ", results.Select(x => x.ErrorMessage)));
        }
    }
}

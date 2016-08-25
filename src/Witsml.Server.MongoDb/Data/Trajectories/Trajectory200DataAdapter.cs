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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Energistics.DataAccess.WITSML200;
using Energistics.Datatypes;
using PDS.Framework;

namespace PDS.Witsml.Server.Data.Trajectories
{
    /// <summary>
    /// Data adapter that encapsulates CRUD functionality for <see cref="Trajectory" />
    /// </summary>
    /// <seealso cref="PDS.Witsml.Server.Data.MongoDbDataAdapter{Trajectory}" />
    [Export(typeof(IWitsmlDataAdapter<Trajectory>))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class Trajectory200DataAdapter : MongoDbDataAdapter<Trajectory>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Trajectory200DataAdapter" /> class.
        /// </summary>
        /// <param name="container">The composition container.</param>
        /// <param name="databaseProvider">The database provider.</param>
        [ImportingConstructor]
        public Trajectory200DataAdapter(IContainer container, IDatabaseProvider databaseProvider) : base(container, databaseProvider, ObjectNames.Trajectory200, ObjectTypes.Uuid)
        {
            Logger.Debug("Instance created.");
        }

        /// <summary>
        /// Gets a collection of data objects related to the specified URI.
        /// </summary>
        /// <param name="parentUri">The parent URI.</param>
        /// <returns>A collection of data objects.</returns>
        public override List<Trajectory> GetAll(EtpUri? parentUri = null)
        {
            Logger.Debug("Fetching all Trajectorys.");

            return GetAllQuery(parentUri)
                .OrderBy(x => x.Citation.Title)
                .ToList();
        }

        /// <summary>
        /// Gets an <see cref="IQueryable{Trajectory}" /> instance to by used by the GetAll method.
        /// </summary>
        /// <param name="parentUri">The parent URI.</param>
        /// <returns>An executable query.</returns>
        protected override IQueryable<Trajectory> GetAllQuery(EtpUri? parentUri)
        {
            var query = GetQuery().AsQueryable();

            if (parentUri != null)
            {
                var uidWellbore = parentUri.Value.ObjectId;
                query = query.Where(x => x.Wellbore.Uuid == uidWellbore);
            }

            return query;
        }
    }
}

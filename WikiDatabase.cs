#region header

// ModernWiki - WikiDatabase.cs
// 
// Alistair J. R. Young
// Arkane Systems
// 
// Copyright Arkane Systems 2012-2013.  All rights reserved.
// 
// Created: 2013-09-29 1:27 PM

#endregion

using System;
using System.Collections.Generic;

using Microsoft.Isam.Esent.Interop;
using System.Reflection;
using System.IO;

namespace WikiDbTest
{
    public sealed class WikiDatabase : IDisposable
    {
        private bool disposed;

        /// <summary>
        ///     Non-reentrancy lock for all WikiDatabase public methods.
        /// </summary>
        private object nonReentrancyLock = new object ();

        #region Singleton

        /// <summary>
        ///     Lazy-initialization field for the singleton instance.
        /// </summary>
        private static readonly Lazy<WikiDatabase> Lazy = new Lazy<WikiDatabase> (() => new WikiDatabase ());

        /// <summary>
        ///     The singleton instance of the WikiDatabase.
        /// </summary>
        public static WikiDatabase Instance
        {
            get { return Lazy.Value; }
        }

        #endregion

        #region Constructor: create and/or open the database

        /// <summary>
        ///     Construct the WikiDatabase instance; create the database file, and make any internal
        ///     structures that aren't there already.
        /// </summary>
        private WikiDatabase ()
        {
            // Get the database folder.  Since this is a test app, we simply stick it in the same place
            // as the executing assembly.
            string workingPath = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);
            string databasePath = Path.Combine (workingPath, "wikis.edb");

            // Initialize ESENT.
            Api.JetCreateInstance2 (out this.instance, "wikiesent", "Wiki ESE", CreateInstanceGrbit.None);

            // Set parameters.
            // Setting circular log to 1 means ESENT will automatically delete unneeded logfiles. JetInit will
            // inspect the logfiles to see if the last shutdown was clean.  If it wasn't (e.g., the application
            // crashed) recovery will be run automatically, bringing the database to a consistent state.
            Api.JetSetSystemParameter (this.instance, JET_SESID.Nil, JET_param.CircularLog, 1, null);

            // Create any necessary file paths if they didn't already exist.
            Api.JetSetSystemParameter (this.instance, JET_SESID.Nil, JET_param.CreatePathIfNotExist, 1, null);

            // Enable index checking.
            Api.JetSetSystemParameter (this.instance, JET_SESID.Nil, JET_param.EnableIndexChecking, 1, null);

            // Set to small configuration; optimizes database engine for memory use.
            Api.JetSetSystemParameter (this.instance, JET_SESID.Nil, (JET_param) 129 /* JET_paramConfiguration */, 0, null);

            Api.JetInit (ref this.instance);
            Api.JetBeginSession(this.instance, out this.session, null, null);

            // Is there already an extant database file?
            if (File.Exists (databasePath))
            {
                // There is such a file, therefore we open/attach the database.
                Api.JetAttachDatabase2 (this.session, databasePath, 0, AttachDatabaseGrbit.None);
                Api.JetOpenDatabase (this.session, databasePath, null, out this.database, OpenDatabaseGrbit.None);

                // Open the wikis table.
                Api.JetOpenTable (this.session,
                                  this.database,
                                  "wikis",
                                  null,
                                  0,
                                  OpenTableGrbit.Updatable,
                                  out this.wikisTable);

                // Load the columnids.
                IDictionary<string, JET_COLUMNID> columnids = Api.GetColumnDictionary (this.session, this.wikisTable);

                this.wikis_id = columnids["id"];
                this.wikis_name = columnids["name"];
                this.wikis_description = columnids["description"];

                // Open the wikiPages table.
                Api.JetOpenTable (this.session,
                                  this.database,
                                  "wikiPages",
                                  null,
                                  0,
                                  OpenTableGrbit.Updatable | OpenTableGrbit.Sequential,
                                  out this.wikiPagesTable);

                // Load the columnids.
                columnids = Api.GetColumnDictionary (this.session, this.wikiPagesTable);

                this.wikipages_id = columnids["id"];
                this.wikipages_wikiName = columnids["wikiName"];
                this.wikipages_pageName = columnids["pageName"];
                this.wikipages_pageClass = columnids["pageClass"];
                this.wikipages_pageContents = columnids["pageContents"];
                this.wikipages_lastUpdated = columnids["lastUpdated"];

                // Open the backlinks table.
                Api.JetOpenTable (this.session,
                                  this.database,
                                  "backlinks",
                                  null,
                                  0,
                                  OpenTableGrbit.Updatable | OpenTableGrbit.Sequential,
                                  out this.backlinksTable);

                // Load the columnids.
                columnids = Api.GetColumnDictionary (this.session, this.backlinksTable);

                this.backlinks_id = columnids["id"];
                this.backlinks_pageId = columnids["pageId"];
                this.backlinks_wikiName = columnids["wikiName"];
                this.backlinks_pageName = columnids["pageName"];
            }
            else
            {
                // There is no such file, therefore we engage in database creation.
                // Create the database.
                Api.JetCreateDatabase(this.session, databasePath, null, out this.database, CreateDatabaseGrbit.None);
                Api.JetBeginTransaction (this.session);

                // Create the wikis table.
                Api.JetCreateTable(this.session, this.database, "wikis", 0, 100, out this.wikisTable);
                
                // Columns

                // id
                var coldef = new JET_COLUMNDEF
                                       {
                                           coltyp = JET_coltyp.Long,
                                           grbit = ColumndefGrbit.ColumnAutoincrement
                                       };
                Api.JetAddColumn(this.session, this.wikisTable, "id", coldef, null, 0, out this.wikis_id);

                // name
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Text,
                    grbit = ColumndefGrbit.ColumnNotNULL,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn (this.session, this.wikisTable, "name", coldef, null, 0, out this.wikis_name);

                // description
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Text,
                    grbit = ColumndefGrbit.ColumnNotNULL,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn (this.session, this.wikisTable, "description", coldef, null, 0, out this.wikis_description);

                // Indices.
                // name
                var indexcreate = new JET_INDEXCREATE
                                              {
                                                  szIndexName = "byName",
                                                  szKey = "+name\0",
                                                  cbKey = "+name\0".Length + 1,
                                                  grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexUnique | CreateIndexGrbit.IndexDisallowNull,
                                              };
                Api.JetCreateIndex2(this.session, this.wikisTable, new [] { indexcreate }, 1);

                // Create the wikiPages table.
                Api.JetCreateTable (this.session, this.database, "wikiPages", 0, 100, out this.wikiPagesTable);

                // Columns.
                // id
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Long,
                    grbit = ColumndefGrbit.ColumnAutoincrement
                };
                Api.JetAddColumn (this.session, this.wikiPagesTable, "id", coldef, null, 0, out this.wikipages_id);

                // wikiName
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Text,
                    grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn(this.session, this.wikiPagesTable, "wikiName", coldef, null, 0, out this.wikipages_wikiName);

                // pageName
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Text,
                    grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn(this.session, this.wikiPagesTable, "pageName", coldef, null, 0, out this.wikipages_pageName);

                // pageClass
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Long,
                    grbit = ColumndefGrbit.ColumnNotNULL,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn(this.session, this.wikiPagesTable, "pageClass", coldef, null, 0, out this.wikipages_pageClass);

                // pageContents
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.LongText,
                    grbit = ColumndefGrbit.ColumnMaybeNull,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn(this.session, this.wikiPagesTable, "pageContents", coldef, null, 0, out this.wikipages_pageContents);

                // lastUpdated
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.DateTime,
                    grbit = ColumndefGrbit.ColumnNotNULL,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn(this.session, this.wikiPagesTable, "lastUpdated", coldef, null, 0, out this.wikipages_lastUpdated);

                // Indices.
                // wikiName + pageName
                indexcreate = new JET_INDEXCREATE
                {
                    szIndexName = "byWikiAndPage",
                    szKey = "+wikiName\0+pageName\0",
                    cbKey = "+wikiName\0+pageName\0".Length + 1,
                    grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexUnique | CreateIndexGrbit.IndexDisallowNull
                };
                Api.JetCreateIndex2 (this.session, this.wikiPagesTable, new[] { indexcreate }, 1);

                // lastUpdated
                indexcreate = new JET_INDEXCREATE
                {
                    szIndexName = "byLastUpdated",
                    szKey = "-lastUpdated\0",
                    cbKey = "-lastUpdated\0".Length + 1,
                    grbit = CreateIndexGrbit.None
                };
                Api.JetCreateIndex2 (this.session, this.wikiPagesTable, new[] { indexcreate }, 1);

                // for full-test sear: pageContents
                indexcreate = new JET_INDEXCREATE
                {
                    szIndexName = "byContents",
                    szKey = "+pageContents\0",
                    cbKey = "+pageContents\0".Length + 1,
                    grbit = CreateIndexGrbit.None
                };
                Api.JetCreateIndex2 (this.session, this.wikiPagesTable, new[] { indexcreate }, 1);

                // Create the backlinks table.
                Api.JetCreateTable (this.session, this.database, "backlinks", 0, 100, out this.backlinksTable);

                // Columns.
                // id
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Long,
                    grbit = ColumndefGrbit.ColumnAutoincrement
                };
                Api.JetAddColumn (this.session, this.backlinksTable, "id", coldef, null, 0, out this.backlinks_id);

                // pageId
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Long
                };
                Api.JetAddColumn (this.session, this.backlinksTable, "pageId", coldef, null, 0, out this.backlinks_pageId);

                // wikiName
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Text,
                    grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn (this.session, this.backlinksTable, "wikiName", coldef, null, 0, out this.backlinks_wikiName);

                // pageName (multivalue)
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Text,
                    grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnMultiValued | ColumndefGrbit.ColumnTagged,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn (this.session, this.backlinksTable, "pageName", coldef, null, 0, out this.backlinks_pageName);

                // Indices.
                // pageId
                indexcreate = new JET_INDEXCREATE
                {
                    szIndexName = "byPageId",
                    szKey = "+pageId\0",
                    cbKey = "+pageId\0".Length + 1,
                    grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexUnique
                };
                Api.JetCreateIndex2 (this.session, this.backlinksTable, new[] { indexcreate }, 1);

                // wikiname and pagename
                indexcreate = new JET_INDEXCREATE
                {
                    szIndexName = "byWikiAndPage",
                    szKey = "+wikiName\0+pageName\0",
                    cbKey = "+wikiName\0+pageName\0".Length + 1,
                    grbit = CreateIndexGrbit.None
                };
                Api.JetCreateIndex2 (this.session, this.backlinksTable, new[] { indexcreate }, 1);

                Api.JetCommitTransaction(this.session, CommitTransactionGrbit.LazyFlush);
            }
        }

        #endregion

        private readonly JET_INSTANCE instance;
        private readonly JET_SESID session;
        private readonly JET_DBID database;

        private readonly JET_TABLEID wikisTable;
        private readonly JET_TABLEID wikiPagesTable;
        private readonly JET_TABLEID backlinksTable;

        private readonly JET_COLUMNID wikis_id;
        private readonly JET_COLUMNID wikis_name;
        private readonly JET_COLUMNID wikis_description;

        private readonly JET_COLUMNID wikipages_id;
        private readonly JET_COLUMNID wikipages_wikiName;
        private readonly JET_COLUMNID wikipages_pageName;
        private readonly JET_COLUMNID wikipages_pageClass;
        private readonly JET_COLUMNID wikipages_pageContents;
        private readonly JET_COLUMNID wikipages_lastUpdated;

        private readonly JET_COLUMNID backlinks_id;
        private readonly JET_COLUMNID backlinks_pageId;
        private readonly JET_COLUMNID backlinks_wikiName;
        private readonly JET_COLUMNID backlinks_pageName;

        #region IDisposable Members

        public void Dispose ()
        {
            if (!this.disposed)
            {
                // Run the cleanup routines.
                // Close the tables.
                Api.JetCloseTable(this.session, this.backlinksTable);
                Api.JetCloseTable(this.session, this.wikiPagesTable);
                Api.JetCloseTable(this.session, this.wikisTable);

                // Terminate ESENT. This performs a clean shutdown.
                Api.JetEndSession(this.session, EndSessionGrbit.None);
                Api.JetTerm (this.instance);

                // Suppress finalization.
                GC.SuppressFinalize (this);

                this.disposed = true;
            }
        }

        #endregion

        #region Destructor

        ~WikiDatabase ()
        {
            // Dispose us if we have not been disposed.
            if (!this.disposed)
                this.Dispose ();
        }

        #endregion
    }
}

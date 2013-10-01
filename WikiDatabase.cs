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
using System.Net.NetworkInformation;

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

            JET_TABLEID wikisTable;
            JET_TABLEID wikiPagesTable;

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
                                  out wikisTable);

                // Load the columnids.
                IDictionary<string, JET_COLUMNID> columnids = Api.GetColumnDictionary (this.session, wikisTable);

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
                                  out wikiPagesTable);

                // Load the columnids.
                columnids = Api.GetColumnDictionary (this.session, wikiPagesTable);

                this.wikipages_id = columnids["id"];
                this.wikipages_wiki = columnids["wiki"];
                this.wikipages_pageName = columnids["pageName"];
                this.wikipages_pageClass = columnids["pageClass"];
                this.wikipages_pageContents = columnids["pageContents"];
                this.wikipages_lastUpdated = columnids["lastUpdated"];
            }
            else
            {
                // There is no such file, therefore we engage in database creation.
                // Create the database.
                Api.JetCreateDatabase(this.session, databasePath, null, out this.database, CreateDatabaseGrbit.None);
                Api.JetBeginTransaction (this.session);

                // Create the wikis table.
                Api.JetCreateTable(this.session, this.database, "wikis", 0, 100, out wikisTable);
                
                // Columns

                // id
                var coldef = new JET_COLUMNDEF
                                       {
                                           coltyp = JET_coltyp.Long,
                                           grbit = ColumndefGrbit.ColumnAutoincrement
                                       };
                Api.JetAddColumn(this.session, wikisTable, "id", coldef, null, 0, out this.wikis_id);

                // name
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Text,
                    grbit = ColumndefGrbit.ColumnNotNULL,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn (this.session, wikisTable, "name", coldef, null, 0, out this.wikis_name);

                // description
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Text,
                    grbit = ColumndefGrbit.ColumnNotNULL,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn (this.session, wikisTable, "description", coldef, null, 0, out this.wikis_description);

                // Indices.
                // name
                var indexcreate = new JET_INDEXCREATE
                                              {
                                                  szIndexName = "byName",
                                                  szKey = "+name\0",
                                                  cbKey = "+name\0".Length + 1,
                                                  grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexUnique | CreateIndexGrbit.IndexDisallowNull,
                                              };
                Api.JetCreateIndex2(this.session, wikisTable, new [] { indexcreate }, 1);

                // Create the wikiPages table.
                Api.JetCreateTable (this.session, this.database, "wikiPages", 0, 100, out wikiPagesTable);

                // Columns.
                // id
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Long,
                    grbit = ColumndefGrbit.ColumnAutoincrement
                };
                Api.JetAddColumn (this.session, wikiPagesTable, "id", coldef, null, 0, out this.wikipages_id);

                // wiki
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Long,
                    grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed,
                };
                Api.JetAddColumn(this.session, wikiPagesTable, "wiki", coldef, null, 0, out this.wikipages_wiki);

                // pageName
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Text,
                    grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn(this.session, wikiPagesTable, "pageName", coldef, null, 0, out this.wikipages_pageName);

                // pageClass
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.Long,
                    grbit = ColumndefGrbit.ColumnNotNULL,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn(this.session, wikiPagesTable, "pageClass", coldef, null, 0, out this.wikipages_pageClass);

                // pageContents
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.LongText,
                    grbit = ColumndefGrbit.ColumnMaybeNull,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn(this.session, wikiPagesTable, "pageContents", coldef, null, 0, out this.wikipages_pageContents);

                // lastUpdated
                coldef = new JET_COLUMNDEF
                {
                    coltyp = JET_coltyp.DateTime,
                    grbit = ColumndefGrbit.ColumnNotNULL,
                    cp = JET_CP.Unicode
                };
                Api.JetAddColumn(this.session, wikiPagesTable, "lastUpdated", coldef, null, 0, out this.wikipages_lastUpdated);

                // Indices.
                // wiki + pageName
                indexcreate = new JET_INDEXCREATE
                {
                    szIndexName = "byWikiAndPage",
                    szKey = "+wiki\0+pageName\0",
                    cbKey = "+wiki\0+pageName\0".Length + 1,
                    grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexUnique | CreateIndexGrbit.IndexDisallowNull
                };
                Api.JetCreateIndex2 (this.session, wikiPagesTable, new[] { indexcreate }, 1);

                // lastUpdated
                indexcreate = new JET_INDEXCREATE
                {
                    szIndexName = "byLastUpdated",
                    szKey = "-lastUpdated\0",
                    cbKey = "-lastUpdated\0".Length + 1,
                    grbit = CreateIndexGrbit.None
                };
                Api.JetCreateIndex2 (this.session, wikiPagesTable, new[] { indexcreate }, 1);

                // for full-test sear: pageContents
                indexcreate = new JET_INDEXCREATE
                {
                    szIndexName = "byContents",
                    szKey = "+pageContents\0",
                    cbKey = "+pageContents\0".Length + 1,
                    grbit = CreateIndexGrbit.None
                };
                Api.JetCreateIndex2 (this.session, wikiPagesTable, new[] { indexcreate }, 1);

                Api.JetCommitTransaction(this.session, CommitTransactionGrbit.LazyFlush);
            }

            // Close the tables.
            Api.JetCloseTable (this.session, wikiPagesTable);
            Api.JetCloseTable (this.session, wikisTable);
        }

        #endregion

        private readonly JET_INSTANCE instance;
        private readonly JET_SESID session;
        private readonly JET_DBID database;

        #region Column id fields

        private readonly JET_COLUMNID wikis_id;
        private readonly JET_COLUMNID wikis_name;
        private readonly JET_COLUMNID wikis_description;

        private readonly JET_COLUMNID wikipages_id;
        private readonly JET_COLUMNID wikipages_wiki;
        private readonly JET_COLUMNID wikipages_pageName;
        private readonly JET_COLUMNID wikipages_pageClass;
        private readonly JET_COLUMNID wikipages_pageContents;
        private readonly JET_COLUMNID wikipages_lastUpdated;

        #endregion

        // WIKI FUNCTIONS

        // Enumerate the wikis currently in the database.
        public IEnumerable<Wiki> GetWikis ()
        {
            throw new NotImplementedException();
        }

        // Create a new wiki, which includes creating its first (front) page.
        public void CreateNewWiki (Wiki newWiki)
        {
            throw new NotImplementedException();
        }

        // Rename a wiki.
        public void RenameWiki (Wiki newNameAndDescription)
        {
            throw new NotImplementedException();
        }

        // Delete a new wiki, including all of its pages.
        public void DeleteWiki (int wikiId)
        {
            throw new NotImplementedException();
        }

        // WIKI PAGES FUNCTIONS

        // Enumerate all wiki pages (by title and class).
        public IEnumerable<WikiIndex> GetWikiIndex (int wikiId)
        {
            throw new NotImplementedException();
        }

        // Fetch a single wiki page.
        public WikiPage GetWikiPage (int wikiId, int id)
        {
            throw new NotImplementedException();
        }

        // Create a wiki page.
        public WikiPage CreateWikiPage (int wikiId, string title)
        {
            throw new NotImplementedException();
        }

        // Save a wiki page.
        public void SaveWikiPage (WikiPage page)
        {
            throw new NotImplementedException();
        }

        // Delete a wiki page.
        public void DeleteWikiPage (int wikiId, int id)
        {
            throw new NotImplementedException();
        }

        // Check if a wiki page exists.
        public bool CheckForPage (int wikiId, string title)
        {
            throw new NotImplementedException();
        }

        // Search titles locally.
        public IEnumerable<WikiIndex> SearchLocal (int wikiId, string search)
        {
            throw new NotImplementedException();
        }

        // Search titles globally.
        public IEnumerable<WikiIndex> SearchGlobal (string search)
        {
            throw new NotImplementedException();
        }

        // Search full text.
        public IEnumerable<WikiIndex> SearchFullText (int wikiId, string search)
        {
            throw new NotImplementedException();
        }

        // Dump all wiki pages (all data).
        public IEnumerable<WikiPage> DumpPages (int wikiId)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Members

        public void Dispose ()
        {
            if (!this.disposed)
            {
                // Run the cleanup routines.
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

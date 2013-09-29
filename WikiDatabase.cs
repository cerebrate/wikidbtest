using System;

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
using Microsoft.Isam.Esent;
using Microsoft.Isam.Esent.Interop;
using Microsoft.Isam.Esent.Interop.Windows8;

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
            get { return WikiDatabase.Lazy.Value; }
        }

        #endregion

        /// <summary>
        ///     Construct the WikiDatabase instance; create the database file, and make any internal
        ///     structures that aren't there already.
        /// </summary>
        private WikiDatabase ()
        { }

        #region IDisposable Members

        public void Dispose ()
        {
            if (!this.disposed)
            {
                // Run the cleanup routines.

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

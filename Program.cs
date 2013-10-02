using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiDbTest
{
    class Program
    {
        static void Main (string[] args)
        {
            // See the database opened or created.
            WikiDatabase wb = WikiDatabase.Instance;

            // Make some wikis.
            Console.WriteLine ("Adding three wikis...");
            wb.CreateNewWiki (new Wiki { Name = "foo", Description = "A wiki about foos." });
            wb.CreateNewWiki (new Wiki { Name = "bar", Description = "A wiki about bars." });
            wb.CreateNewWiki (new Wiki { Name = "baz", Description = "A wiki about bazzes." });

            // Enumerate the wikis.
            EnumerateWikis (wb);

            // Rename a wiki.
            Console.WriteLine ("Renaming foo wiki...");
            var fooWiki = wb.GetWikis ().Single(w => w.Name == "foo");
            fooWiki.Description = "A wiki about fooze.";
            wb.RenameWiki(fooWiki);

            // Enumerate the wikis.
            EnumerateWikis (wb);

            // Delete a wiki.
            Console.WriteLine ("Deleting bar wiki...");
            var barWiki = wb.GetWikis ().Single (w => w.Name == "bar");
            wb.DeleteWiki(barWiki.Id);

            // Enumerate the wikis.
            EnumerateWikis(wb);

            // Create some pages in foowiki.
            Console.WriteLine("Creating some test pages...");
            wb.CreateWikiPage (fooWiki.Id, "Monkey");
            wb.CreateWikiPage (fooWiki.Id, "Hat");
            wb.CreateWikiPage (fooWiki.Id, "Fish");

            // Create elsewhere
            var bazWiki = wb.GetWikis ().Single (w => w.Name == "baz");
            wb.CreateWikiPage (bazWiki.Id, "Fish");

            // Display page counts.
            int fooCount = wb.GetWikiPageCount (fooWiki.Id);
            int bazCount = wb.GetWikiPageCount (bazWiki.Id);

            Console.WriteLine(String.Format ("foo: {0}, bar: {1}", fooCount, bazCount));

            // Enumerate the pages.
            EnumerateWikiIndices (wb, fooWiki);

            // Enumerate the pages.
            EnumerateWikiIndicesByDate (wb, fooWiki);

            // Pause.
            Console.WriteLine ("Pausing...");
            Console.ReadLine ();

            // Close down the database.
            wb.Dispose();
        }

        private static void EnumerateWikis (WikiDatabase wb)
        {
            Console.WriteLine ();

            var wikis = wb.GetWikis ();
            foreach (Wiki wiki in wikis)
                Console.WriteLine (String.Format ("({0}) {1}: {2}", wiki.Id, wiki.Name, wiki.Description));

            Console.WriteLine ();
        }

        private static void EnumerateWikiIndices (WikiDatabase wb, Wiki wiki)
        {
            Console.WriteLine ();
            Console.WriteLine ("By title:");

            var pages = wb.GetWikiIndexByTitle(wiki.Id);
            foreach (WikiIndex page in pages)
                Console.WriteLine (String.Format ("{0}: {1} ({2}/{3})", page.Id, page.Name, page.Class, page.LastUpdated));

            Console.WriteLine ();
        }

        private static void EnumerateWikiIndicesByDate (WikiDatabase wb, Wiki wiki)
        {
            Console.WriteLine ();
            Console.WriteLine ("By last update:");

            var pages = wb.GetWikiIndexByLastUpdated(wiki.Id);
            foreach (WikiIndex page in pages)
                Console.WriteLine (String.Format ("{0}: {1} ({2}/{3})", page.Id, page.Name, page.Class, page.LastUpdated));

            Console.WriteLine ();
        }
    }
}

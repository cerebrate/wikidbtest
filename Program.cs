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



            // Close down the database.
            wb.Dispose();
        }
    }
}

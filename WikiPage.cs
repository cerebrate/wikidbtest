using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiDbTest
{
    public class WikiPage
    {
        public int Id { get; set; }

        public int Wiki { get; set; }

        public string Name { get; set; }

        public PageClass Class { get; set; }

        public string Contents { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiDbTest
{
    public class WikiIndex
    {
        public int Id { get; set; }

        public int Wiki { get; set; }

        public string Name { get; set; }

        public PageClass Class { get; set; }
    }
}

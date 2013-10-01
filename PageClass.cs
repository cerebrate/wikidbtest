using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiDbTest
{
    /// <summary>
    ///     Class of a particular wiki page.
    /// </summary>
    public enum PageClass
    {
        /// <summary>
        ///     A regular entry in the Wiki.
        /// </summary>
        Entry = 0,

        /// <summary>
        ///     An article-type entry in the Wiki; displayed in boldface.
        /// </summary>
        Article = 1,

        /// <summary>
        ///     A wiki cover page, of which there may only be one per wiki.
        /// </summary>
        Cover = 2
    }
}

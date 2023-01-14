using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractFaces
{
    internal class XmpPerson
    {
        public string PersonDisplayName { get; set; } = string.Empty;

        public RectangleF Rectangle { get; set; } = RectangleF.Empty;
    }
}

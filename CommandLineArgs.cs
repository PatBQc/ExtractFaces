using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractFaces
{
    internal class CommandLineArgs
    {
        public bool ShowExif { get; set; } = false;
        
        public bool ShowXmp { get; set; } = false;

        public bool WaitKeystrokeToEnd { get; set; } = false;

        public bool Flush { get; set; } = false;

        public bool Square { get; set; } = false;

        public bool Recursive { get; set; } = false;

        public bool Verbose { get; set; } = false;

        public int DestinationFilePrefix { get; set; } = 0;

        public int MinWidth { get; set; } = -1;

        public int MinHeight { get; set; } = -1;

        public string Source { get; set; } = null;

        public string Destination { get; set; } = null;

        public HashSet<string> Persons { get; set; } = null;

        public List<int> Percents { get; set; } = null;

        public int TakeDestinationFilePrefix()
        {
            return DestinationFilePrefix++;
        }

    }
}

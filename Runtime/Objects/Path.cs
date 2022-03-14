using System.Collections.Generic;

namespace Scopa.Formats.Map.Objects
{
    public class Path
    {

        public string Name { get; set; }
        public string Type { get; set; }
        public PathDirection Direction { get; set; }
        public List<PathNode> Nodes { get; set; }

        public Path()
        {
            Nodes = new List<PathNode>();
        }
    }
}
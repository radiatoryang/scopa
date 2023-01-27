using System.Collections.Generic;
using System.Drawing;

namespace Sledge.Formats.Map.Objects
{
    public class Path
    {

        public string Name { get; set; }
        public string Type { get; set; }
        public PathDirection Direction { get; set; }
        public List<PathNode> Nodes { get; set; }
        public Color Color { get; set; }
        public int Flags { get; set; }

        public Path()
        {
            Nodes = new List<PathNode>();
        }
    }
}
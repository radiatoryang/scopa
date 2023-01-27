using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace Sledge.Formats.Map.Objects
{
    public class PathNode
    {
        public Vector3 Position { get; set; }
        public int ID { get; set; }
        public string Name { get; set; }
        public Dictionary<string, string> Properties { get; private set; }
        public Color Color { get; set; }

        public PathNode()
        {
            Properties = new Dictionary<string, string>();
        }
    }
}
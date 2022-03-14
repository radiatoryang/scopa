using System.Collections.Generic;
using UnityEngine;

namespace Scopa.Formats.Map.Objects
{
    public class PathNode
    {
        public Vector3 Position { get; set; }
        public int ID { get; set; }
        public string Name { get; set; }
        public Dictionary<string, string> Properties { get; private set; }

        public PathNode()
        {
            Properties = new Dictionary<string, string>();
        }
    }
}
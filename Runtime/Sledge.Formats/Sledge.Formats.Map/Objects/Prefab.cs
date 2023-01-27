using System;
using System.Collections.Generic;
using System.Text;

namespace Sledge.Formats.Map.Objects
{
    public class Prefab
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public MapFile Map { get; set; }
    }
}

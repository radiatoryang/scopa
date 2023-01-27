using System.Collections.Generic;
using System.Numerics;
using Sledge.Formats.Valve;

namespace Sledge.Formats.Map.Objects
{
    public class MapFile
    {
        public Worldspawn Worldspawn { get; }
        public List<Visgroup> Visgroups { get; set; }
        public List<Path> Paths { get; set; }
        public List<Camera> Cameras { get; set; }
        public List<SerialisedObject> AdditionalObjects { get; set; }
        public (Vector3 min, Vector3 max) CordonBounds { get; set; }

        public MapFile()
        {
            Worldspawn = new Worldspawn();
            Visgroups = new List<Visgroup>();
            Paths = new List<Path>();
            Cameras = new List<Camera>();
            AdditionalObjects = new List<SerialisedObject>();
            CordonBounds = (Vector3.Zero, Vector3.Zero);
        }
    }
}
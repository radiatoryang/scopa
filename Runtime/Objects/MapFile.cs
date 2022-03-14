using System.Collections.Generic;

namespace Scopa.Formats.Map.Objects
{
    public class MapFile
    {
        public Worldspawn Worldspawn { get; }
        public List<Visgroup> Visgroups { get; set; }
        // public List<Path> Paths { get; set; }
        // public List<Camera> Cameras { get; set; }
        // public List<SerialisedObject> AdditionalObjects { get; set; }

        public MapFile()
        {
            Worldspawn = new Worldspawn();
            Visgroups = new List<Visgroup>();
            // Paths = new List<Path>();
            // Cameras = new List<Camera>();
            // AdditionalObjects = new List<SerialisedObject>();
        }
    }
}
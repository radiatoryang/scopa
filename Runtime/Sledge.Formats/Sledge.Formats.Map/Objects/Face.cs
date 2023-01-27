using System.Collections.Generic;
using System.Numerics;

namespace Sledge.Formats.Map.Objects
{
    public class Face : Surface
    {
        public Plane Plane { get; set; }
        public List<Vector3> Vertices { get; set; }

        public Face()
        {
            Vertices = new List<Vector3>();
        }
    }
}
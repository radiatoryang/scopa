using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Scopa.Formats.Map.Objects
{
    public class Solid : MapObject
    {
        public List<Face> Faces { get; set; }
        public List<Mesh> Meshes { get; set; }

        public Solid()
        {
            Faces = new List<Face>();
            Meshes = new List<Mesh>();
        }

        public void ComputeVertices()
        {
            if (Faces.Count < 4) return;

            var poly = new Polyhedron(Faces.Select(x => new Plane(x.Plane.normal, x.Plane.distance)));

            foreach (var face in Faces)
            {
                var pg = poly.Polygons.FirstOrDefault(x => Vector3Extensions.Approximately(x.plane.normal, face.Plane.normal, 0.1f) ); // 0.0075d ? Magic number that seems to match VHE
                if (pg != null)
                {
                   face.Vertices.AddRange(pg.vertices);
                }

            }
        }

        public override string ToString()
        {
            var s = "";
            if ( Faces != null && Faces.Count > 0) {
                s += string.Join( "\n    ", Faces.Select( face => face.ToString() ));
            } else {
                s += "(empty)";
            }
            return s + base.ToString();
        }
    }
}
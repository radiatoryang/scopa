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

            var poly = new Polyhedron(Faces.Select(x => new Plane(x.Plane.Normal.ToPrecisionVector3(), x.Plane.D)));

            foreach (var face in Faces)
            {
                if ( !face.Plane.IsOrthogonal() ) {
                    // Debug.Log("ComputeVertices: trying to match non-orthogonal face " + face.Plane);

                    face.Plane.ReverseNormal();

                    // Debug.Log("ComputeVertices possible candidates: " + string.Join("\n", poly.Polygons.Select(p => p.Plane)) );
                }

                var pg = poly.Polygons.FirstOrDefault(x => x.Plane.Normal.EquivalentTo(face.Plane.Normal.ToPrecisionVector3(), 0.0075f)); // Magic number that seems to match VHE
                if (pg != null)
                {
                    face.Vertices.AddRange(pg.Vertices.Select(x => x.ToStandardVector3()));
                    // if ( !face.Plane.IsOrthogonal() ) {
                    //     Debug.Log("ComputeVertices: found vertices! " + string.Join("\n", face.Vertices));
                    // }
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
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Scopa.Formats.Map.Objects
{
    public class Solid : MapObject
    {
        public List<Face> Faces { get; set; }
        public List<Mesh> Meshes { get; set; }
        public int id = -1;

        public Solid()
        {
            Faces = new List<Face>();
            Meshes = new List<Mesh>();
        }

        public void ComputeVertices(float weldingThreshold = 0.1f)
        {
            if (Faces.Count < 4) return;

            var poly = new Polyhedron(Faces.Select(x => new Plane(x.Plane.Normal.ToPrecisionVector3(), x.Plane.D)));
            var polyList = new List<Polygon>( poly.Polygons );

            foreach (var face in Faces)
            {
                var pgList = polyList.OrderBy( poly => Vector3Extensions.GetTotalDelta(poly.Plane.Normal, face.Plane.Normal) );

                if ( pgList.FirstOrDefault() == null || !face.Plane.Normal.EquivalentTo(pgList.First().Plane.Normal, 0.1f) ) {
                    face.Plane.ReverseNormal();

                    pgList = polyList.OrderBy( poly => Vector3Extensions.GetTotalDelta(poly.Plane.Normal, face.Plane.Normal) );
                    if ( pgList.FirstOrDefault() != null && face.Plane.Normal.EquivalentTo(pgList.First().Plane.Normal, 0.1f) ) {
                        face.Vertices.AddRange( pgList.First().Vertices.Select(x => x.ToStandardVector3()) );
                        foreach ( var vert in face.Vertices ) {
                            Debug.DrawRay( vert, face.Plane.Normal, Color.cyan, 60f, true );
                        }
                        polyList.Remove(pgList.First());
                    }
                } else {
                    face.Vertices.AddRange( pgList.First().Vertices.Select(x => x.ToStandardVector3()) );
                    foreach ( var vert in face.Vertices ) {
                        Debug.DrawRay( vert, face.Plane.Normal, Color.yellow, 60f, true );
                    }
                    polyList.Remove(pgList.First());
                }

                // var pg = poly.Polygons.FirstOrDefault(x => x.Plane.Normal.EquivalentTo(face.Plane.Normal.ToPrecisionVector3(), 0.1f));

                // if ( pg == null ) {
                //     // Debug.Log("ComputeVertices: trying to match non-orthogonal face " + face.Plane);

                //     face.Plane.ReverseNormal();
                //     pg = poly.Polygons.FirstOrDefault(x => x.Plane.Normal.EquivalentTo(face.Plane.Normal.ToPrecisionVector3(), 0.1f));

                //     // Debug.Log("ComputeVertices possible candidates: " + string.Join("\n", poly.Polygons.Select(p => p.Plane)) );
                // }
                
                // if (pg != null)
                // {
                //     face.Vertices.AddRange(pg.Vertices.Select(x => x.ToStandardVector3()));
                //     // if ( !face.Plane.IsOrthogonal() ) {
                //     //     Debug.Log("ComputeVertices: found vertices! " + string.Join("\n", face.Vertices));
                //     // }
                // }
            }

            // TODO: how do we actually do reliable welding?
            // weld nearby vertices together within in each solid
            // foreach(var face1 in Faces) {
            //     foreach (var face2 in Faces) {
            //         if ( face1 == face2 )
            //             continue;

            //         for(int a=0; a<face1.Vertices.Count; a++) {
            //             for(int b=0; b<face2.Vertices.Count; b++ ) {
            //                 if ( (face1.Vertices[a] - face2.Vertices[b]).sqrMagnitude < weldingThreshold * weldingThreshold ) {
            //                     face2.Vertices[b] = face1.Vertices[a];
            //                 }
            //             }
            //         }
            //     }
            // }


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
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

        static List<Polygon> polyList = new List<Polygon>(32);

        public static float weldingThreshold = 4f;

        public Solid()
        {
            Faces = new List<Face>();
            Meshes = new List<Mesh>();
        }

        public void ComputeVertices()
        {
            if (Faces.Count < 4) return;

            var poly = new Polyhedron(Faces.Select(x => new Plane(x.Plane.Normal, x.Plane.D)));
            // var polyList = new List<Polygon>( poly.Polygons );
            polyList.Clear();
            polyList.AddRange(poly.Polygons);

            foreach (var face in Faces)
            {
                face.Plane.ReverseNormal();
                polyList = polyList.OrderBy( poly => VectorExtensions.GetTotalDelta(poly.Plane.Normal, face.Plane.Normal) ).ToList();

                if (polyList.Count == 0 || !face.Plane.Normal.EquivalentTo(polyList[0].Plane.Normal, 0.1f) ) {
                    face.Plane.ReverseNormal();

                    polyList = polyList.OrderBy( poly => VectorExtensions.GetTotalDelta(poly.Plane.Normal, face.Plane.Normal) ).ToList();
                    if ( polyList.Count > 0 && face.Plane.Normal.EquivalentTo(polyList[0].Plane.Normal, 0.1f) ) {
                        face.Vertices.AddRange( polyList[0].Vertices ); // .Select(x => x.ToStandardVector3()) );
                        // foreach ( var vert in face.Vertices ) {
                        //     Debug.DrawRay( vert, face.Plane.Normal, Color.cyan, 60f, true );
                        // }
                        polyList.RemoveAt(0);
                    }
                } else {
                    face.Vertices.AddRange( polyList[0].Vertices ); // .Select(x => x.ToStandardVector3()) );
                    // foreach ( var vert in face.Vertices ) {
                    //     Debug.DrawRay( vert, face.Plane.Normal, Color.yellow, 60f, true );
                    // }
                    polyList.RemoveAt(0);
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

            // snap nearby vertices together within in each solid
            var origin = poly.Origin;
            foreach(var face1 in Faces) {
                foreach (var face2 in Faces) {
                    if ( face1 == face2 )
                        continue;

                    for(int a=0; a<face1.Vertices.Count; a++) {
                        for(int b=0; b<face2.Vertices.Count; b++ ) {
                            if ( (face1.Vertices[a] - face2.Vertices[b]).sqrMagnitude < weldingThreshold * weldingThreshold ) {
                                if ( (face1.Vertices[a] - origin).sqrMagnitude > (face2.Vertices[b] - origin).sqrMagnitude )
                                    face2.Vertices[b] = face1.Vertices[a];
                                else
                                    face1.Vertices[a] = face2.Vertices[b];
                            }
                        }
                    }
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
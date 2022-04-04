using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Scopa
{
    /// <summary>
    /// Represents a convex polyhedron with at least 4 sides. Uses high-precision value types.
    /// </summary>
    public class Polyhedron
    {
        public IReadOnlyList<Polygon> Polygons { get; }

        public Vector3 Origin => Polygons.Aggregate(Vector3.zero, (x, y) => x + y.Origin) / Polygons.Count;

        /// <summary>
        /// Creates a polyhedron from a list of polygons which are assumed to be valid.
        /// </summary>
        public Polyhedron(IEnumerable<Polygon> polygons)
        {
            Polygons = polygons.ToList();
        }

        /// <summary>
        /// Creates a polyhedron by intersecting a set of at least 4 planes.
        /// </summary>
        public Polyhedron(IEnumerable<Plane> planes)
        {
            var polygons = new List<Polygon>();
            
            var list = planes.ToList();

            // var anyNonOrtho = planes.Where( plane => !plane.IsOrthogonal() ).Any();
            // if ( anyNonOrtho )
            //     Debug.Log("Polyhedron " + string.Join( "\n", planes) );
            for (var i = 0; i < list.Count; i++)
            {
                // Split the polygon by all the other planes
                var poly = new Polygon(list[i], 100000f);
                for (var j = 0; j < list.Count; j++)
                {
                    if (i != j && poly.Split(list[j], out var back, out var front))
                    {
                        poly = back;
                    }
                }
                
                // if ( !list[i].IsOrthogonal() ) {
                //     Debug.Log("DONE! resulting polygon is: " + string.Join("\n", poly.Vertices) );
                // }
                polygons.Add(poly);
            }

            // Ensure all the faces point outwards
            var origin = polygons.Aggregate(Vector3.zero, (x, y) => x + y.Origin) / polygons.Count;
            for (var i = 0; i < polygons.Count; i++)
            {
                var face = polygons[i];
                if (face.Plane.OnPlane(origin) >= 0) {
                    polygons[i] = new Polygon(face.Vertices.Reverse());
                    face.Plane.ReverseNormal();
                //    Debug.Log($"reversing normal {face.Plane} away from {origin} -> {polygons[i].Plane} ");
                }
            }

            Polygons = polygons;
        }

        /// <summary>
        /// Splits this polyhedron into two polyhedron by intersecting against a plane.
        /// </summary>
        /// <param name="plane">The splitting plane</param>
        /// <param name="back">The back side of the polyhedron</param>
        /// <param name="front">The front side of the polyhedron</param>
        /// <returns>True if the plane splits the polyhedron, false if the plane doesn't intersect</returns>
        // public bool Split(Plane plane, out Polyhedron back, out Polyhedron front)
        // {
        //     back = front = null;

        //     // Check that this solid actually spans the plane
        //     var classify = Polygons.Select(x => x.ClassifyAgainstPlane(plane)).Distinct().ToList();
        //     if (classify.All(x => x != PlaneClassification.Spanning))
        //     {
        //         if (classify.Any(x => x == PlaneClassification.Back)) back = this;
        //         else if (classify.Any(x => x == PlaneClassification.Front)) front = this;
        //         return false;
        //     }

        //     var backPlanes = new List<Plane> { plane };
        //     var frontPlanes = new List<Plane> { new Plane(-plane.Normal, -plane.DistanceFromOrigin) };

        //     foreach (var face in Polygons)
        //     {
        //         var classification = face.ClassifyAgainstPlane(plane);
        //         if (classification != PlaneClassification.Back) frontPlanes.Add(face.Plane);
        //         if (classification != PlaneClassification.Front) backPlanes.Add(face.Plane);
        //     }

        //     back = new Polyhedron(backPlanes);
        //     front = new Polyhedron(frontPlanes);
            
        //     return true;
        // }
    }
}

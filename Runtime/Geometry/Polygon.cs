using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Scopa
{
    /// <summary>
    /// Represents a coplanar, directed polygon with at least 3 vertices. Uses high-precision value types.
    /// </summary>
    public class Polygon
    {
        public IReadOnlyList<Vector3> vertices { get; }

        public Plane plane => new Plane(vertices[0], vertices[1], vertices[2]);
        public Vector3 Origin => vertices.Aggregate(Vector3.zero, (x, y) => x + y) / vertices.Count;

        /// <summary>
        /// Creates a polygon from a list of points
        /// </summary>
        /// <param name="vertices">The vertices of the polygon</param>
        public Polygon(IEnumerable<Vector3> vertices)
        {
            this.vertices = vertices.ToList();
        }

        /// <summary>
        /// Creates a polygon from a plane and a radius.
        /// Expands the plane to the radius size to create a large polygon with 4 vertices.
        /// </summary>
        /// <param name="plane">The polygon plane</param>
        /// <param name="radius">The polygon radius</param>
        public Polygon(Plane plane, float radius = 1000000f)
        {
            // Get aligned up and right axes to the plane
            var tempV = plane.GetClosestAxisToNormal() == Vector3.forward ? -Vector3.up : -Vector3.forward;
            var up = Vector3.Cross(tempV, plane.normal).normalized;
            var right = Vector3.Cross(plane.normal, up).normalized;

            var point = plane.PointOnPlane();

            var verts = new List<Vector3>
            {
                point + right + up, // Top right
                point - right + up, // Top left
                point - right - up, // Bottom left
                point + right - up, // Bottom right
            };
            
            var origin = verts.Aggregate(Vector3.zero, (x, y) => x + y) / verts.Count;
            vertices = verts.Select(x => (x - origin).normalized * radius + origin).ToList();
        }

        // public PlaneClassification ClassifyAgainstPlane(Plane p)
        // {
        //     var count = Vertices.Count;
        //     var front = 0;
        //     var back = 0;
        //     var onplane = 0;

        //     foreach (var t in Vertices)
        //     {
        //         var test = p.OnPlane(t);

        //         // Vertices on the plane are both in front and behind the plane in this context
        //         if (test <= 0) back++;
        //         if (test >= 0) front++;
        //         if (test == 0) onplane++;
        //     }

        //     if (onplane == count) return PlaneClassification.OnPlane;
        //     if (front == count) return PlaneClassification.Front;
        //     if (back == count) return PlaneClassification.Back;
        //     return PlaneClassification.Spanning;
        // }

        /// <summary>
        /// Splits this polygon by a clipping plane, returning the back and front planes.
        /// The original polygon is not modified.
        /// </summary>
        /// <param name="clip">The clipping plane</param>
        /// <param name="back">The back polygon</param>
        /// <param name="front">The front polygon</param>
        /// <returns>True if the split was successful</returns>
        public bool Split(Plane clip, out Polygon back, out Polygon front)
        {
            return Split(clip, out back, out front, out _, out _);
        }

        /// <summary>
        /// Splits this polygon by a clipping plane, returning the back and front planes.
        /// The original polygon is not modified.
        /// </summary>
        /// <param name="clip">The clipping plane</param>
        /// <param name="back">The back polygon</param>
        /// <param name="front">The front polygon</param>
        /// <param name="coplanarBack">If the polygon rests on the plane and points backward, this will not be null</param>
        /// <param name="coplanarFront">If the polygon rests on the plane and points forward, this will not be null</param>
        /// <returns>True if the split was successful</returns>
        public bool Split(Plane clip, out Polygon back, out Polygon front, out Polygon coplanarBack, out Polygon coplanarFront)
        {
            // const double epsilon = NumericsExtensions.Epsilon;
            
            var distances = vertices.Select(clip.GetDistanceToPoint).ToList();
            
            int cb = 0, cf = 0;
            for (var i = 0; i < distances.Count; i++)
            {
                if (distances[i] <= -Mathf.Epsilon) cb++;
                else if (distances[i] >= Mathf.Epsilon) cf++;
                else distances[i] = 0;
            }

            // Check non-spanning cases
            if (cb == 0 && cf == 0)
            {
                // Co-planar
                back = front = coplanarBack = coplanarFront = null;
                if (Vector3.Dot(clip.normal, plane.normal) > 0) 
                    coplanarFront = this;
                else 
                    coplanarBack = this;
                return false;
            }
            else if (cb == 0)
            {
                // All vertices in front
                back = coplanarBack = coplanarFront = null;
                front = this;
                return false;
            }
            else if (cf == 0)
            {
                // All vertices behind
                front = coplanarBack = coplanarFront = null;
                back = this;
                return false;
            }

            // Get the new front and back vertices
            var backVerts = new List<Vector3>();
            var frontVerts = new List<Vector3>();

            for (var i = 0; i < vertices.Count; i++)
            {
                var j = (i + 1) % vertices.Count;

                var s = vertices[i];
                var e = vertices[j];
                var sd = distances[i];
                var ed = distances[j];

                if (sd <= 0) backVerts.Add(s);
                if (sd >= 0) frontVerts.Add(s);

                if ((sd < 0 && ed > 0) || (ed < 0 && sd > 0))
                {
                    var t = sd / (sd - ed);
                    var intersect = s * (1 - t) + e * t;

                    backVerts.Add(intersect);
                    frontVerts.Add(intersect);
                }
            }
            
            back = new Polygon(backVerts.Select(v => new Vector3(v.x, v.y, v.z)));
            front = new Polygon(frontVerts.Select(v => new Vector3(v.x, v.y, v.z)));
            coplanarBack = coplanarFront = null;

            return true;
        }
    }
}

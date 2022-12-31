using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Scopa
{
    /// <summary>
    /// Represents a coplanar, directed polygon with at least 3 vertices.
    /// </summary>
    public class Polygon
    {
        public List<Vector3> Vertices { get; }

        public Plane Plane;
        public Vector3 Origin {
            get {
                var average = Vector3.zero;
                for(int i=0; i<Vertices.Count; i++) {
                    average += Vertices[i];
                }
                return average / Mathf.Max(1, Vertices.Count);
            }
        }

        /// <summary>
        /// Creates a polygon from a list of points
        /// </summary>
        /// <param name="vertices">The vertices of the polygon</param>
        public Polygon(params Vector3[] vertices)
        {
            Vertices = new List<Vector3>(vertices);
            Plane = new Plane(Vertices[0], Vertices[1], Vertices[2]);
        }

        /// <summary>
        /// Creates a polygon from a list of points
        /// </summary>
        /// <param name="vertices">The vertices of the polygon</param>
        public Polygon(IEnumerable<Vector3> vertices)
        {
            Vertices = new List<Vector3>(vertices);
            Plane = new Plane(Vertices[0], Vertices[1], Vertices[2]);
        }

        /// <summary>
        /// Creates a polygon from a list of points
        /// </summary>
        /// <param name="vertices">The vertices of the polygon</param>
        public Polygon(List<Vector3> vertices)
        {
            Vertices = new List<Vector3>(vertices);
            Plane = new Plane(Vertices[0], Vertices[1], Vertices[2]);
        }

        /// <summary>
        /// Creates a polygon from a plane and a radius.
        /// Expands the plane to the radius size to create a large polygon with 4 vertices.
        /// </summary>
        /// <param name="plane">The polygon plane</param>
        /// <param name="radius">The polygon radius</param>
        public Polygon(Plane plane, float radius)
        {
            // Get aligned up and right axes to the plane
            var direction = plane.GetClosestAxisToNormal();
            var tempV = direction == Vector3.forward ? Vector3.up : Vector3.forward;
            var up = tempV.Cross(plane.Normal).Normalise();
            var right = plane.Normal.Cross(up).Normalise();
            up *= radius;
            right *= radius;

            var verts = new List<Vector3>
            {
                plane.PointOnPlane + right + up, // Top right
                plane.PointOnPlane + right - up, // Bottom right
                plane.PointOnPlane - right - up, // Bottom left
                plane.PointOnPlane - right + up, // Top left
            };
            
            // var origin = verts.Aggregate(Vector3.zero, (x, y) => x + y) / verts.Count;
            Vertices = verts.ToList();

            Plane = plane.Clone();
        }

        /// <summary> based on the first 3 vertices (a triangle), sample a random point barycentrically </summary>
        public Vector3 GetRandomPointAsTriangle() {
            // from https://gist.github.com/danieldownes/b1c9bab09cce013cc30a4198bfeda0aa
            float r = Random.value;
            float s = Random.value;

            if (r + s >= 1)
            {
                r = 1 - r;
                s = 1 - s;
            }

            return Vertices[0] + r * (Vertices[1] - Vertices[0]) + s * (Vertices[2] - Vertices[0]);
        }

        /// <summary> based on the first 3 vertices (a triangle), calculate area </summary>
        public float GetSizeAsTriangle() {
            // from https://gist.github.com/danieldownes/b1c9bab09cce013cc30a4198bfeda0aa
            return 0.5f * Vector3.Cross(Vertices[1] - Vertices[0], Vertices[2] - Vertices[0]).magnitude;
        }

        /// <summary> based on the first 3 vertices (a triangle), calculate area </summary>
        public float GetSizeAsTriangle(Vector3 scale) {
            // from https://gist.github.com/danieldownes/b1c9bab09cce013cc30a4198bfeda0aa
            return 0.5f * Vector3.Cross(Vector3.Scale(Vertices[1], scale) - Vector3.Scale(Vertices[0], scale), Vector3.Scale(Vertices[2], scale) - Vector3.Scale(Vertices[0], scale)).magnitude;
        }

        /// <summary> based on the first 3 vertices (a triangle), calculate if any angle is less than X degrees</summary>
        const float LONG_THIN_ANGLE = 10f;
        public bool IsLongAndThin() {
            float angleA = Vector3.Angle(Vertices[1] - Vertices[0], Vertices[2] - Vertices[0]);
            float angleB = Vector3.Angle(Vertices[0] - Vertices[1], Vertices[2] - Vertices[1]);
            return angleA < LONG_THIN_ANGLE || angleB < LONG_THIN_ANGLE || angleA + angleB > 180f - LONG_THIN_ANGLE;
        }

        /// <summary>
        /// Determines if this polygon is behind, in front, or spanning a plane. Returns calculated data.
        /// </summary>
        /// <param name="p">The plane to test against</param>
        /// <param name="classifications">The OnPlane classification for each vertex</param>
        /// <param name="front">The number of vertices in front of the plane</param>
        /// <param name="back">The number of vertices behind the plane</param>
        /// <param name="onplane">The number of vertices on the plane</param>
        /// <returns>A PlaneClassification value.</returns>
        // private PlaneClassification ClassifyAgainstPlane(Plane p, out int[] classifications, out int front, out int back, out int onplane)
        // {
        //     var count = Vertices.Count;
        //     front = 0;
        //     back = 0;
        //     onplane = 0;
        //     classifications = new int[count];

        //     for (var i = 0; i < Vertices.Count; i++)
        //     {
        //         var test = p.OnPlane(Vertices[i]);

        //         // Vertices on the plane are both in front and behind the plane in this context
        //         if (test <= 0) back++;
        //         if (test >= 0) front++;
        //         if (test == 0) onplane++;

        //         classifications[i] = test;
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
            return SplitNew(clip, out back, out front, out _, out _);
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

        // public bool SplitOld(Plane clip, out Polygon back, out Polygon front, out Polygon coplanarBack, out Polygon coplanarFront)
        // {
        //     var count = Vertices.Count;

        //     var classify = ClassifyAgainstPlane(clip, out var classifications, out _, out _, out _);

        //     Debug.Log("Splitting " + Plane.ToString() + " with " + clip.ToString() + "... " + classify.ToString() );

        //     // If the polygon doesn't span the plane, return false.
        //     if (classify != PlaneClassification.Spanning)
        //     {
        //         back = front = null;
        //         coplanarBack = coplanarFront = null;
        //         if (classify == PlaneClassification.Back) back = this;
        //         else if (classify == PlaneClassification.Front) front = this;
        //         else if (Plane.Normal.Dot(clip.Normal) > 0) coplanarFront = this;
        //         else coplanarBack = this;
        //         return false;
        //     }

        //     // Get the new front and back vertices
        //     var backVerts = new List<Vector3>();
        //     var frontVerts = new List<Vector3>();
        //     var prev = 0;

        //     for (var i = 0; i <= count; i++)
        //     {
        //         var idx = i % count;
        //         var end = Vertices[idx];
        //         var cls = classifications[idx];

        //         // Check plane crossing
        //         if (i > 0 && cls != 0 && prev != 0 && prev != cls)
        //         {
        //             // This line end point has crossed the plane
        //             // Add the line intersect to the 
        //             var start = Vertices[i - 1];
        //             // var line = new Line(start, end);
        //             Debug.Log($"looking for intersection on {clip} between {start} and {end}");
        //             var isect = clip.GetIntersectionPoint(start, end);
        //             if (isect == null) Debug.LogError("Expected intersection, got null.");
        //             Debug.Log("found intersection at " + isect);
        //             frontVerts.Add(isect.Value);
        //             backVerts.Add(isect.Value);
        //         }

        //         // Add original points
        //         if (i < Vertices.Count)
        //         {
        //             // OnPlane points get put in both polygons, doesn't generate split
        //             if (cls >= 0) frontVerts.Add(end);
        //             if (cls <= 0) backVerts.Add(end);
        //         }

        //         prev = cls;
        //     }

        //     Debug.Log("resulting backVerts: " + string.Join(" ", backVerts));

        //     back = new Polygon(backVerts);
        //     front = new Polygon(frontVerts);
        //     coplanarBack = coplanarFront = null;

        //     return true;
        // }

        // static buffers for Split function, to reduce GC
        static List<float> distances = new List<float>(32);
        static List<Vector3> backVerts = new List<Vector3>(32);
        static List<Vector3> frontVerts = new List<Vector3>(32);

        public bool SplitNew(Plane clip, out Polygon back, out Polygon front, out Polygon coplanarBack, out Polygon coplanarFront)
        {
            const float epsilon = 0.01f;

            // Debug.Log("Splitting " + Plane.ToString() + " with " + clip.ToString() );
            
            bool isOrthogonal = this.Plane.IsOrthogonal();
            // var debugInfo = this.Plane.ToString() + " vs " + clip.ToString();
            // var tempClip = clip.Clone();
            // if ( isOrthogonal ) {
            //     tempClip.ReverseNormal();
            //     Debug.Log("non ortho, trying...");
            // }

            // var distances = Vertices.Select(clip.EvalAtPoint).ToList();
            distances.Clear();
            for(int n=0; n<Vertices.Count; n++) {
                distances.Add( clip.EvalAtPoint(Vertices[n]) );
            }

            // Debug.Log("Split distances: " + string.Join(", ", distances) );
            
            int cb = 0, cf = 0;
            for (var i = 0; i < distances.Count; i++)
            {
                if (distances[i] < -epsilon) cb++;
                else if (distances[i] > epsilon) cf++;
                else distances[i] = 0;
            }

            // Check non-spanning cases
            if (cb == 0 && cf == 0)
            {
                // if ( !isOrthogonal )
                //     Debug.Log("Split result: non-spanning " + debugInfo);
                // Co-planar
                back = front = coplanarBack = coplanarFront = null;
                if (Plane.Normal.Dot(clip.Normal) >= 0) coplanarFront = this;
                else coplanarBack = this;
                return false;
            }
            else if (cb == 0)
            {
                // if ( !isOrthogonal )
                //     Debug.Log("Split result: all back " + debugInfo);
                // All vertices in front
                back = coplanarBack = coplanarFront = null;
                front = this;

                return false;
            }
            else if (cf == 0)
            {
                // if ( !isOrthogonal )
                //     Debug.Log("Split result: all front " + debugInfo);
                // All vertices behind
                front = coplanarBack = coplanarFront = null;
                back = this;

                return false;
            }

            // if ( !isOrthogonal ) {
            //     Debug.Log("Split result: mixed " + debugInfo);
            // }

            // Get the new front and back vertices
            backVerts.Clear();
            frontVerts.Clear();

            for (var i = 0; i < Vertices.Count; i++)
            {
                var j = (i + 1) % Vertices.Count;

                Vector3 s = Vertices[i], e = Vertices[j];
                float sd = distances[i], ed = distances[j];

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
            
            back = new Polygon(backVerts);
            // front = new Polygon(frontVerts);
            front = null; // we throw away the front anyway, so why bother
            coplanarBack = coplanarFront = null;

            // if ( !isOrthogonal )
            //     Debug.Log( "verts are now: " + string.Join("\n", backVerts.Select( vert => vert.ToString() )) );

            return true;
        }
    }
}

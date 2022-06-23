using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scopa.Formats.Map.Objects
{
    public class Face : Surface
    {
        public Plane Plane { get; set; }

        /// <summary> the UNSCALED vertices generated after clipping the planes </summary>
        public List<Vector3> Vertices { get; set; }
        public bool discardWhenBuildingMesh = false;

        public Face()
        {
            Vertices = new List<Vector3>();
        }

        public Face(IEnumerable<Vector3> verts)
        {
            Vertices = new List<Vector3>( verts );
            Plane = new Plane( Vertices[0], Vertices[1], Vertices[2] );
        }

        const float EPSILON = 0.01f;

        public bool OccludesFace(Face maybeSmallerFace) {
            // first, test (1) share similar plane distance and (2) face opposite directions
            // we are testing the NEGATIVE case for early out
            if ( Mathf.Abs( Plane.distance + maybeSmallerFace.Plane.distance) > 1f || Vector3.Dot(Plane.normal, maybeSmallerFace.Plane.normal) > -0.99f ) {
                return false;
            }

            // then, test whether one face's vertices are completely inside the other
            var ignoreAxis = maybeSmallerFace.Plane.GetMainAxisToNormal(); // 2D math is easier, so let's ignore the least important axis

            // slightly contract the vert, edge is unreliable
            // var otherFaceCenter = maybeSmallerFace.Vertices.Aggregate(Vector3.zero, (x, y) => x + y) / maybeSmallerFace.Vertices.Count; 

            // manually aggregate and average for better perf?
            var otherFaceCenter = maybeSmallerFace.Vertices[0];
            for( int n=1; n<maybeSmallerFace.Vertices.Count; n++) {
                otherFaceCenter += maybeSmallerFace.Vertices[n];
            }
            otherFaceCenter /= maybeSmallerFace.Vertices.Count;

            for( int i=0; i<maybeSmallerFace.Vertices.Count; i++ ) {
                var smallFaceVert = maybeSmallerFace.Vertices[i] + (otherFaceCenter - maybeSmallerFace.Vertices[i]).normalized * 0.1f;
                switch (ignoreAxis) {
                    case Axis.X: if (!IsInPolygonYZ3(smallFaceVert, Vertices)) return false; break;
                    case Axis.Y: if (!IsInPolygonXZ3(smallFaceVert, Vertices)) return false; break;
                    case Axis.Z: if (!IsInPolygonXY3(smallFaceVert, Vertices)) return false; break;
                }
            }

            return true;
        }

        public bool IsCoplanarPointInPolygon(Vector3 point) {
             var ignoreAxis = Plane.GetMainAxisToNormal();
             switch (ignoreAxis) {
                case Axis.X: if (!IsInPolygonYZ3(point, Vertices)) return false; break;
                case Axis.Y: if (!IsInPolygonXZ3(point, Vertices)) return false; break;
                case Axis.Z: if (!IsInPolygonXY3(point, Vertices)) return false; break;
            }
            return true;
        }

        public bool IsInPolygonYZ3( Vector3 testPoint, List<Vector3> vertices){
            // Get the angle between the point and the
            // first and last vertices.
            int max_point = vertices.Count - 1;
            float total_angle = GetAngle(
                vertices[max_point].y, vertices[max_point].z,
                testPoint.y, testPoint.z,
                vertices[0].y, vertices[0].z);

            // Add the angles from the point
            // to each other pair of vertices.
            for (int i = 0; i < max_point; i++) {
                total_angle += GetAngle(
                    vertices[i].y, vertices[i].z,
                    testPoint.y, testPoint.z,
                    vertices[i + 1].y, vertices[i + 1].z);
            }

            // The total angle should be 2 * PI or -2 * PI if
            // the point is in the polygon and close to zero
            // if the point is outside the polygon.
            // The following statement was changed. See the comments.
            //return (Math.Abs(total_angle) > 0.000001);
            return Mathf.Abs(total_angle) > EPSILON;
        }

        public bool IsInPolygonXY3( Vector3 testPoint, List<Vector3> vertices){
            // Get the angle between the point and the
            // first and last vertices.
            int max_point = vertices.Count - 1;
            float total_angle = GetAngle(
                vertices[max_point].x, vertices[max_point].y,
                testPoint.x, testPoint.y,
                vertices[0].x, vertices[0].y);

            // Add the angles from the point
            // to each other pair of vertices.
            for (int i = 0; i < max_point; i++) {
                total_angle += GetAngle(
                    vertices[i].x, vertices[i].y,
                    testPoint.x, testPoint.y,
                    vertices[i + 1].x, vertices[i + 1].y);
            }

            // The total angle should be 2 * PI or -2 * PI if
            // the point is in the polygon and close to zero
            // if the point is outside the polygon.
            // The following statement was changed. See the comments.
            //return (Math.Abs(total_angle) > 0.000001);
            return Mathf.Abs(total_angle) > EPSILON;
        }

        public bool IsInPolygonXZ3( Vector3 testPoint, List<Vector3> vertices){
            // Get the angle between the point and the
            // first and last vertices.
            int max_point = vertices.Count - 1;
            float total_angle = GetAngle(
                vertices[max_point].x, vertices[max_point].z,
                testPoint.x, testPoint.z,
                vertices[0].x, vertices[0].z);

            // Add the angles from the point
            // to each other pair of vertices.
            for (int i = 0; i < max_point; i++) {
                total_angle += GetAngle(
                    vertices[i].x, vertices[i].z,
                    testPoint.x, testPoint.z,
                    vertices[i + 1].x, vertices[i + 1].z);
            }

            // The total angle should be 2 * PI or -2 * PI if
            // the point is in the polygon and close to zero
            // if the point is outside the polygon.
            // The following statement was changed. See the comments.
            //return (Math.Abs(total_angle) > 0.000001);
            return Mathf.Abs(total_angle) > EPSILON;
        }

        // Return the angle ABC.
        // Return a value between PI and -PI.
        // Note that the value is the opposite of what you might
        // expect because Y coordinates increase downward.
        static float GetAngle(float Ax, float Ay,
            float Bx, float By, float Cx, float Cy)
        {
            // Get the dot product.
            float dot_product = DotProduct(Ax, Ay, Bx, By, Cx, Cy);

            // Get the cross product.
            float cross_product = CrossProductLength(Ax, Ay, Bx, By, Cx, Cy);

            // Calculate the angle.
            return (float)Mathf.Atan2(cross_product, dot_product);
        }

        static float DotProduct(float Ax, float Ay,
            float Bx, float By, float Cx, float Cy)
        {
            // Get the vectors' coordinates.
            float BAx = Ax - Bx;
            float BAy = Ay - By;
            float BCx = Cx - Bx;
            float BCy = Cy - By;

            // Calculate the dot product.
            return (BAx * BCx + BAy * BCy);
        }

        // Return the cross product AB x BC.
        // The cross product is a vector perpendicular to AB
        // and BC having length |AB| * |BC| * Sin(theta) and
        // with direction given by the right-hand rule.
        // For two vectors in the X-Y plane, the result is a
        // vector with X and Y components 0 so the Z component
        // gives the vector's length and direction.
        static float CrossProductLength(float Ax, float Ay,
            float Bx, float By, float Cx, float Cy)
        {
            // Get the vectors' coordinates.
            float BAx = Ax - Bx;
            float BAy = Ay - By;
            float BCx = Cx - Bx;
            float BCy = Cy - By;

            // Calculate the Z coordinate of the cross product.
            return (BAx * BCy - BAy * BCx);
        }

        // from https://stackoverflow.com/questions/39853481/is-point-inside-polygon
        public static bool IsInPolygonXY( Vector3 testPoint, List<Vector3> vertices ) {
            bool isInPolygon = false;
            var lastVertex = vertices[vertices.Count - 1];
            foreach( var vertex in vertices ) {
                if( ( testPoint.y - lastVertex.y ) * ( testPoint.y - vertex.y ) < 0 ) {
                    var x = ( testPoint.y - lastVertex.y ) / ( vertex.y - lastVertex.y ) * ( vertex.x - lastVertex.x ) + lastVertex.x;
                    if ( x >= testPoint.x ) 
                        isInPolygon = !isInPolygon;
                }
                else {
                    if( Mathf.Abs(testPoint.y - lastVertex.y) < EPSILON && testPoint.x < lastVertex.x && vertex.y > testPoint.y ) 
                        isInPolygon = !isInPolygon;
                    if( Mathf.Abs(testPoint.y - vertex.y) < EPSILON && testPoint.x < vertex.x && lastVertex.y > testPoint.y ) 
                        isInPolygon = !isInPolygon;
                }
                lastVertex = vertex;
            }
            return isInPolygon;
        }

        public static bool IsInPolygonXZ( Vector3 testPoint, List<Vector3> vertices ) {
            bool isInPolygon = false;
            var lastVertex = vertices[vertices.Count - 1];
            foreach( var vertex in vertices ) {
                if( ( testPoint.z - lastVertex.z ) * ( testPoint.z - vertex.z ) < 0 ) {
                    var x = ( testPoint.z - lastVertex.z ) / ( vertex.z - lastVertex.z ) * ( vertex.x - lastVertex.x ) + lastVertex.x;
                    if ( x >= testPoint.x ) 
                        isInPolygon = !isInPolygon;
                }
                else {
                    if( Mathf.Abs(testPoint.z - lastVertex.z) < EPSILON && testPoint.x + EPSILON < lastVertex.x && vertex.z > testPoint.z + EPSILON ) 
                        isInPolygon = !isInPolygon;
                    if( Mathf.Abs(testPoint.z - vertex.z) < EPSILON && testPoint.x + EPSILON < vertex.x && lastVertex.z > testPoint.z + EPSILON ) 
                        isInPolygon = !isInPolygon;
                }
                lastVertex = vertex;
            }
            return isInPolygon;
        }

        public static bool IsInPolygonYZ( Vector3 testPoint, List<Vector3> vertices ) {
            bool isInPolygon = false;
            var lastVertex = vertices[vertices.Count - 1];
            foreach( var vertex in vertices ) {
                if( ( testPoint.z - lastVertex.z ) * ( testPoint.z - vertex.z ) < 0 ) {
                    var y = ( testPoint.z - lastVertex.z ) / ( vertex.z - lastVertex.z ) * ( vertex.y - lastVertex.y ) + lastVertex.y;
                    if ( y >= testPoint.y ) 
                        isInPolygon = !isInPolygon;
                }
                else {
                    if( Mathf.Abs(testPoint.z - lastVertex.z) < EPSILON && testPoint.y < lastVertex.y && vertex.z > testPoint.z ) 
                        isInPolygon = !isInPolygon;
                    if( Mathf.Abs(testPoint.z - vertex.z) < EPSILON && testPoint.y < vertex.y && lastVertex.z > testPoint.z ) 
                        isInPolygon = !isInPolygon;
                }
                lastVertex = vertex;
            }
            return isInPolygon;
        }

        public static bool IsInPolygonXY2(Vector3 testPoint, List<Vector3> polygon)
        {
            bool result = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count(); i++)
            {
                if (polygon[i].y < testPoint.y && polygon[j].y - testPoint.y > -EPSILON || polygon[j].y < testPoint.y && polygon[i].y - testPoint.y > -EPSILON)
                {
                    if (polygon[i].x + (testPoint.y - polygon[i].y) / (polygon[j].y - polygon[i].y) * (polygon[j].x - polygon[i].x) < testPoint.x)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        public static bool IsInPolygonXZ2(Vector3 testPoint, List<Vector3> polygon)
        {
            bool result = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count(); i++)
            {
                if (polygon[i].z < testPoint.z && polygon[j].z - testPoint.z > EPSILON || polygon[j].z < testPoint.z && polygon[i].z - testPoint.z > EPSILON)
                {
                    if (polygon[i].x + (testPoint.z - polygon[i].z) / (polygon[j].z - polygon[i].z) * (polygon[j].x - polygon[i].x) < testPoint.x)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        public static bool IsInPolygonYZ2(Vector3 testPoint, List<Vector3> polygon)
        {
            bool result = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count(); i++)
            {
                if (polygon[i].z < testPoint.z && polygon[j].z - testPoint.z > -EPSILON || polygon[j].z < testPoint.z && polygon[i].z - testPoint.z > -EPSILON)
                {
                    if (polygon[i].y + (testPoint.z - polygon[i].z) / (polygon[j].z - polygon[i].z) * (polygon[j].y - polygon[i].y) < testPoint.y)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        public void DebugDrawVerts(Color color, float duration = 5f) {
            for(int i=0; i<Vertices.Count-1; i++) {
                Debug.DrawLine(Vertices[i] * 0.03125f, Vertices[i+1] * 0.03125f, color, duration, false);
                Debug.DrawRay(Vertices[i] * 0.03125f, Plane.normal, color * 0.69f, duration, false);
            }
            Debug.DrawLine(Vertices[0] * 0.03125f, Vertices[Vertices.Count-1] * 0.03125f, color, duration, false);
            Debug.DrawRay(Vertices[Vertices.Count-1] * 0.03125f, Plane.normal, color * 0.69f, duration, false);
        }

        public override string ToString() {
            if ( Vertices != null && Vertices.Count > 0 ) {
                return TextureName + " " + string.Join( " ", Vertices.Select( vert => vert.ToString() ) );
            } else {
                return TextureName + " (no vertices?)";
            }
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scopa.Formats.Map.Objects
{
    public class Face : Surface
    {
        public Plane Plane { get; set; }
        public List<Vector3> Vertices { get; set; }
        public bool discardWhenBuildingMesh = false;

        public Face()
        {
            Vertices = new List<Vector3>();
        }

        const float EPSILON = 0.01f;

        public bool OccludesFace(Face otherFace) {
            // first, test (1) share similar plane distance and (2) face opposite directions
            if ( otherFace.discardWhenBuildingMesh || Mathf.Abs(-Plane.distance - otherFace.Plane.distance) > EPSILON || Vector3.Dot(Plane.normal, otherFace.Plane.normal) >= -0.99f ) {
                return false;
            }

            // then, test whether one face's vertices are completely inside the other
            var ignoreAxis = Plane.GetMainAxisToNormal(); // 2D math is easier, so let's ignore the least important axis
            for( int i=0; i<Vertices.Count; i++ ) {
                switch (ignoreAxis) {
                    case Axis.X: if (!IsInPolygonYZ(Vertices[i], otherFace.Vertices)) return false; break;
                    case Axis.Y: if (!IsInPolygonXZ(Vertices[i], otherFace.Vertices)) return false; break;
                    case Axis.Z: if (!IsInPolygonXY(Vertices[i], otherFace.Vertices)) return false; break;
                }
            }

            return true;
        }

        // from https://stackoverflow.com/questions/39853481/is-point-inside-polygon
        public static bool IsInPolygonXY( Vector3 testPoint, List<Vector3> vertices ) {
            bool isInPolygon = false;
            var lastVertex = vertices[vertices.Count - 1];
            foreach( var vertex in vertices ) {
                if( ( testPoint.y - lastVertex.y ) * ( testPoint.y - vertex.y ) < EPSILON ) {
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
                if( ( testPoint.z - lastVertex.z ) * ( testPoint.z - vertex.z ) < EPSILON ) {
                    var x = ( testPoint.z - lastVertex.z ) / ( vertex.z - lastVertex.z ) * ( vertex.x - lastVertex.x ) + lastVertex.x;
                    if ( x >= testPoint.x ) 
                        isInPolygon = !isInPolygon;
                }
                else {
                    if( Mathf.Abs(testPoint.z - lastVertex.z) < EPSILON && testPoint.x < lastVertex.x && vertex.z > testPoint.z ) 
                        isInPolygon = !isInPolygon;
                    if( Mathf.Abs(testPoint.z - vertex.z) < EPSILON && testPoint.x < vertex.x && lastVertex.z > testPoint.z ) 
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
                if( ( testPoint.z - lastVertex.z ) * ( testPoint.z - vertex.z ) < EPSILON ) {
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

        public static bool IsPointInPolygon(Vector2 point, Vector2[] polygon) {
            int polygonLength = polygon.Length, i = 0;
            bool inside = false;
            // x, y for tested point.
            float pointX = point.x, pointY = point.y;
            // start / end point for the current polygon segment.
            float startX, startY, endX, endY;
            Vector2 endPoint = polygon[polygonLength - 1];
            endX = endPoint.x;
            endY = endPoint.y;
            while (i < polygonLength)
            {
                startX = endX; startY = endY;
                endPoint = polygon[i++];
                endX = endPoint.x; endY = endPoint.y;
                //
                inside ^= (endY > pointY ^ startY > pointY) /* ? pointY inside [startY;endY] segment ? */
                            && /* if so, test if it is under the segment */
                            ((pointX - endX) < (pointY - endY) * (startX - endX) / (startY - endY));
            }
            return inside;
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
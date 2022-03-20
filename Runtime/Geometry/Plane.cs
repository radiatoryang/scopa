using UnityEngine;

namespace Scopa {

    public enum PlaneClassification
    {
        Front,
        Back,
        OnPlane,
        Spanning
    }

    /// <summary> wraps around UnityEngine.Plane so I don't have to mess with the other map format code so much </summary>
    public class Plane {
        /// <summary> internal UnityEngine.Plane used for all the math </summary>
        UnityEngine.Plane _plane;

        public float D => _plane.distance;

        public Vector3 normal => _plane.normal;
        public Vector3 Normal => _plane.normal;

        public float distance => _plane.distance;
        public float Distance => _plane.distance;

        public Vector3 PointOnPlane => _plane.normal * _plane.distance;

        public Plane(Vector3 normal, float distance) {
            _plane = new UnityEngine.Plane(normal, distance);
        }

        public Plane(Vector3 a, Vector3 b, Vector3 c) {
            _plane = new UnityEngine.Plane(a, b, c);
        }

        public void ReverseDistance() {
            _plane.distance *= -1;
        }

        public void ReverseNormal() {
            _plane.normal *= -1;
        }

        public void Flip() {
            _plane.Flip();
        }

        /// <summary>
        /// Gets the axis closest to the normal of this plane
        /// </summary>
        /// <returns>Vector3.UnitX, Vector3.UnitY, or Vector3.UnitZ depending on the plane's normal</returns>
        public Vector3 GetClosestAxisToNormal() {
            // VHE prioritises the axes in order of X, Y, Z.
            // so in Unity land, that's X, Z, and Y
            var norm = _plane.normal.Absolute();

            if (norm.x >= norm.y && norm.x >= norm.z) return Vector3.right;
            if (norm.z >= norm.y) return Vector3.forward;
            return Vector3.up;
        }

        public bool IsOrthogonal() {
            if ( Mathf.Abs(_plane.normal.x) > 0.01f && Mathf.Abs(_plane.normal.x) < 0.99f ) {
                return false;
            } else if ( Mathf.Abs(_plane.normal.y) > 0.01f && Mathf.Abs(_plane.normal.y) < 0.99f ) {
                return false;
            } else if ( Mathf.Abs(_plane.normal.z) > 0.01f && Mathf.Abs(_plane.normal.z) < 0.99f ) {
                return false;
            }
            return true;
        }

        public Vector3? GetIntersectionPoint(Vector3 start, Vector3 end) {
            if (_plane.Raycast(new Ray(start, end - start), out var enter) ) {
                return start + (end-start).normalized * enter;
                // return _plane.ClosestPointOnPlane(start);
            } else {
                return null;
            }
        }

        public float EvalAtPoint(Vector3 point) {
            return _plane.GetDistanceToPoint(-point);
        }

        public float GetDistanceToPoint(Vector3 point) {
            return _plane.GetDistanceToPoint(point);
        }

        public int OnPlane(Vector3 point) {
            var res = _plane.GetDistanceToPoint(point);
            if (Mathf.Abs(res) < 0.001f) return 0;
            if (res < 0) return -1;
            return 1;
        }

        public void DebugDraw() {
            Debug.DrawRay( _plane.normal * _plane.distance, _plane.normal * 10f, Color.yellow, 60f );
        }

        public override string ToString()
        {
            return $"[Plane normal {normal}, dist {distance}]";
        }

        public Plane Clone() {
            return new Plane(normal, distance);
        }
    }
}
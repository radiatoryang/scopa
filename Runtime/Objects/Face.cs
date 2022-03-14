using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scopa.Formats.Map.Objects
{
    public class Face : Surface
    {
        public Plane Plane { get; set; }
        public List<Vector3> Vertices { get; set; }

        public Face()
        {
            Vertices = new List<Vector3>();
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
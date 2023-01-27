using UnityEngine;
using System.Collections.Generic;
using Sledge.Formats.Map.Objects;
using Mesh = UnityEngine.Mesh;

namespace Scopa {
    public class ScopaBehaviour: MonoBehaviour {
        
        public MapFile mapFileData;

        // public void OnDrawGizmos() {
        //     if ( mapFileData != null ) {
        //         var quadMesh = GetQuadMesh();

        //         foreach ( var child in mapFileData.Worldspawn.Children ) {
        //             Debug.Log("child");
        //             if ( child is Solid ) {
        //                 Debug.Log("drawing");
        //                 var solid = child as Solid;
        //                 foreach ( var face in solid.Faces) {
        //                     Gizmos.DrawMesh( quadMesh, -1, face.Plane.PointOnPlane(), Quaternion.LookRotation(face.Plane.normal), Vector3.one );
        //                 }
        //             }
        //         }
        //     }
        // }

        public Mesh GetQuadMesh() {
            // create quad
            var quadMesh = new Mesh();
            var vertices = new Vector3[4]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0)
            };
            quadMesh.SetVertices( vertices );

            int[] tris = new int[6]
            {
                // lower left triangle
                0, 2, 1,
                // upper right triangle
                2, 3, 1
            };
            quadMesh.SetTriangles(tris, 0);

            quadMesh.RecalculateBounds();
            quadMesh.RecalculateNormals();
            return quadMesh;
        }


    }
}
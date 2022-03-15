using System.Linq;
using System.Collections.Generic;
using Scopa.Formats.Map.Formats;
using Scopa.Formats.Map.Objects;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

namespace Scopa {
    public static class Scopa {

        public static MapFile Import( string pathToMapFile ) {
            IMapFormat importer = null;
            if ( pathToMapFile.EndsWith(".map")) {
                importer = new QuakeMapFormat();
            } 

            if ( importer == null) {
                Debug.LogError($"No file importer found for {pathToMapFile}");
                return null;
            }

            var mapFile = importer.ReadFromFile( pathToMapFile );

            Debug.Log( pathToMapFile );

            Debug.Log( mapFile.Worldspawn );

            return mapFile;
        }

        public static Mesh BuildMesh( Entity ent, string mapName, float scalingFactor = 0.03125f ) {
            var verts = new List<Vector3>();
            // ar normals = new List<Vector3>();
            var tris = new List<int>();
            int vertCount = 0;

            var solids = ent.Children.Where( x => x is Solid);

            foreach (Solid solid in solids) {
                foreach (var face in solid.Faces) {
                    if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                        continue;

                    for(int i=0; i<face.Vertices.Count-2; i++) {
                        tris.Add(vertCount + i);
                        tris.Add(vertCount + i + 2);
                        tris.Add(vertCount + i + 1);
                    }

                    for( int v=0; v<face.Vertices.Count; v++) {
                        verts.Add(face.Vertices[v] * scalingFactor);
                        // normals.Add(face.Plane.normal);
                    }
                    vertCount += face.Vertices.Count;

                    // add UVs?
                }
            }

            var mesh = new Mesh();
            mesh.name = mapName + "-" + ent.ClassName;
            mesh.SetVertices(verts);
            // mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.Optimize();
            
            return mesh;
        }

    }
}
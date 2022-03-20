using System.Linq;
using System.Collections.Generic;
using Scopa.Formats.Map.Formats;
using Scopa.Formats.Map.Objects;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

namespace Scopa {
    public static class Scopa {
        public static float scalingFactor = 0.03125f;

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

            //Debug.Log( pathToMapFile );

            //Debug.Log( mapFile.Worldspawn );

            return mapFile;
        }

        public static List<Mesh> BuildMeshRecursive( Entity ent, string mapName, float scalingFactor = 1f ) {
            var allMeshes = new List<Mesh>();

            var newMesh = BuildMesh(ent, mapName, scalingFactor) ;
            if ( newMesh != null )
                allMeshes.Add( newMesh );

            foreach ( var child in ent.Children ) {
                if ( child is Entity ) {
                    var newMeshChildren = BuildMeshRecursive(child as Entity, mapName, scalingFactor);
                    if ( newMeshChildren.Count > 0)
                        allMeshes.AddRange( newMeshChildren );
                }
            }

            return allMeshes;
        }

        public static bool IsExcludedTexName(string textureName) {
            var texName = textureName.ToLowerInvariant();
            return texName.Contains("sky") || texName.Contains("trigger") || texName.Contains("clip") || texName.Contains("skip") || texName.Contains("water");
        }

        public static Mesh BuildMesh( Entity ent, string mapName, float scalingFactor = 1f ) {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int vertCount = 0;

            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();
            var allFaces = new List<Face>();

            // pass 1: gather all faces + cull any faces we're obviously not going to use
            foreach (var solid in solids) {
                foreach (var face in solid.Faces) {
                    if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                        continue;

                    // skip tool textures and other objects?
                    if ( IsExcludedTexName(face.TextureName) ) {
                        face.discardWhenBuildingMesh = true;
                        continue;
                    }
                    
                    allFaces.Add(face);
                }
            }

            // pass 2: now build mesh, while also culling unseen faces
            foreach (var solid in solids) {
                foreach (var face in solid.Faces) {
                    if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                        continue;

                    if ( face.discardWhenBuildingMesh )
                        continue;

                    // test for unseen / hidden faces, and discard
                    // TODO: doesn't actually work? ugh
                    // foreach( var otherFace in allFaces ) {
                    //     if (otherFace.OccludesFace(face)) {
                    //         Debug.Log("discarding unseen face at " + face);
                    //         otherFace.discardWhenBuildingMesh = true;
                    //         break;
                    //     }
                    // }

                    if ( face.discardWhenBuildingMesh )
                        continue;

                    for(int i=2; i<face.Vertices.Count; i++) {
                        tris.Add(vertCount);
                        tris.Add(vertCount + i - 1);
                        tris.Add(vertCount + i);
                    }

                    for( int v=0; v<face.Vertices.Count; v++) {
                        verts.Add(face.Vertices[v] * scalingFactor);
                        // normals.Add(face.Plane.normal);
                    }
                    vertCount += face.Vertices.Count;

                    // add UVs?
                }
            }

            if ( verts.Count == 0 || tris.Count == 0) 
                return null;
                
            var mesh = new Mesh();
            mesh.name = mapName + "-" + ent.ClassName + "-" + verts[0].ToString();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.Optimize();
            
            #if UNITY_EDITOR
            UnityEditor.MeshUtility.SetMeshCompression(mesh, UnityEditor.ModelImporterMeshCompression.Medium);
            #endif
            
            return mesh;
        }

        public static List<ColliderData> GenerateColliders(Entity ent, bool forceBoxCollidersForAll = true) {
            var newColliders = new List<ColliderData>();
            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();

            bool isAABB = true;
            foreach ( var solid in solids ) {
                // ignore solids that are textured in all invisible textures
                bool exclude = true;
                foreach ( var face in solid.Faces ) {
                    if ( !IsExcludedTexName(face.TextureName) ) {
                        exclude = false;
                        break;
                    }
                }
                if ( exclude )
                    continue;

                // first, detect boxy AABB solids; if so, then it can be an efficient box collider
                isAABB = true;
                var verts = new List<Vector3>();
                foreach ( var face in solid.Faces ) {
                    if (!forceBoxCollidersForAll && !face.Plane.IsOrthogonal() ) {
                        isAABB = false;
                        break;
                    } else {
                        verts.AddRange( face.Vertices );
                    }
                }
                if ( isAABB ) {
                    newColliders.Add( new ColliderData(GeometryUtility.CalculateBounds(verts.ToArray(), Matrix4x4.identity)) );
                    continue;
                }

                // TODO: otherwise, use a mesh collider
            }

            return newColliders;
        }

    }

    public class ColliderData {
        public Bounds boxColliderBounds;
        public Vector3[] meshColliderVerts;

        public ColliderData(Bounds bounds) {
            boxColliderBounds = bounds;
        }

        public ColliderData(Vector3[] verts) {
            meshColliderVerts = verts;
        }
    }
}
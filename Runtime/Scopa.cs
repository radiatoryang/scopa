using System.Linq;
using System.Collections.Generic;
using Scopa.Formats.Map.Formats;
using Scopa.Formats.Map.Objects;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

namespace Scopa {
    public static class Scopa {
        public static float scalingFactor = 0.03125f; // 1/32, since 1.0 meters = 32 units

        static bool warnedUserAboutMultipleColliders = false;
        const string warningMessage = "WARNING! Unity will complain about too many colliders with same name on same object, because it may not re-import in the same order / same way again."
            + "\n\nHowever, this is by design. You don't want a game object for each box colllder / a thousand box colliders. So just IGNORE UNITY'S WARNINGS.";

        /// <summary>Parses the .MAP text data into a usable data structure.</summary>
        public static MapFile Parse( string pathToMapFile ) {
            IMapFormat importer = null;
            if ( pathToMapFile.EndsWith(".map")) {
                importer = new QuakeMapFormat();
            } 

            if ( importer == null) {
                Debug.LogError($"No file importer found for {pathToMapFile}");
                return null;
            }

            var mapFile = importer.ReadFromFile( pathToMapFile );
            mapFile.name = System.IO.Path.GetFileNameWithoutExtension( pathToMapFile );

            return mapFile;
        }

        /// <summary>The main core function for converting parsed a set of MapFile data into a GameObject with 3D mesh and colliders.
        /// Outputs a lists of built meshes (e.g. so UnityEditor can serialize them)</summary>
        public static GameObject BuildMapIntoGameObject( MapFile mapFile, Material defaultMaterial, out List<Mesh> meshList ) {
            var rootGameObject = new GameObject( mapFile.name );
            // gameObject.AddComponent<ScopaBehaviour>().mapFileData = mapFile;

            warnedUserAboutMultipleColliders = false;
            meshList = Scopa.AddGameObjectFromEntityRecursive(rootGameObject, mapFile.Worldspawn, mapFile.name, defaultMaterial);

            return rootGameObject;
        }

        /// <summary> The main core function for converting entities (worldspawn, func_, etc.) into 3D meshes. </summary>
        public static List<Mesh> AddGameObjectFromEntityRecursive( GameObject rootGameObject, Entity ent, string namePrefix, Material defaultMaterial ) {
            var allMeshes = new List<Mesh>();

            var newMeshes = AddGameObjectFromEntity(rootGameObject, ent, namePrefix, defaultMaterial) ;
            if ( newMeshes != null )
                allMeshes.AddRange( newMeshes );

            foreach ( var child in ent.Children ) {
                if ( child is Entity ) {
                    var newMeshChildren = AddGameObjectFromEntityRecursive(rootGameObject, child as Entity, namePrefix, defaultMaterial);
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

        public static List<Mesh> AddGameObjectFromEntity( GameObject rootGameObject, Entity ent, string namePrefix, Material defaultMaterial ) {
            var meshList = new List<Mesh>();
            var verts = new List<Vector3>();
            var tris = new List<int>();

            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();
            var allFaces = new List<Face>(); // used later for testing unseen faces
            var lastSolidID = -1;

            // pass 1: gather all faces + cull any faces we're obviously not going to use
            foreach (var solid in solids) {
                lastSolidID = solid.id;
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
            foreach ( var solid in solids) {
                BuildMeshFromSolid( solid, false, verts, tris);
            }

            if ( verts.Count == 0 || tris.Count == 0) 
                return null;
                
            var mesh = new Mesh();
            mesh.name = namePrefix + "-" + ent.ClassName + "-" + lastSolidID.ToString();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.Optimize();
            meshList.Add( mesh );
            
            #if UNITY_EDITOR
            UnityEditor.MeshUtility.SetMeshCompression(mesh, UnityEditor.ModelImporterMeshCompression.Medium);
            #endif

            // finally, add mesh as game object + do collision, while we still have all the entity information
            var newMeshObj = new GameObject( mesh.name.Substring(namePrefix.Length+1) );
            newMeshObj.transform.SetParent(rootGameObject.transform);
            newMeshObj.AddComponent<MeshFilter>().sharedMesh = mesh;
            newMeshObj.AddComponent<MeshRenderer>().sharedMaterial = defaultMaterial;

            // collision pass
            meshList.AddRange( Scopa.AddColliders( newMeshObj, ent, namePrefix ) );
            
            return meshList;
        }

        /// <summary> given a brush / solid, either 
        /// (a) adds mesh data to provided verts / tris lists 
        /// OR (b) returns a mesh if no lists provided</summary>
        public static Mesh BuildMeshFromSolid(Solid solid, bool includeDiscardedFaces = false, List<Vector3> verts = null, List<int> tris = null) {
            bool returnMesh = false;

            if (verts == null || tris == null) {
                verts = new List<Vector3>();
                tris = new List<int>();
                returnMesh = true;
            }

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

                // if ( face.discardWhenBuildingMesh )
                //     continue;

                AddFaceVerticesToMesh(face.Vertices, verts, tris);
            }

            if ( !returnMesh ) {
                return null;
            }

            var mesh = new Mesh();
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

        /// <summary> build mesh fragment (verts / tris), usually run for each face of a solid </summary>
        static void AddFaceVerticesToMesh(List<Vector3> addTheseVertices, List<Vector3> verts, List<int> tris) {
            var vertCount = verts.Count;

            for( int v=0; v<addTheseVertices.Count; v++) {
                verts.Add(addTheseVertices[v]);
            }

            // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
            for(int i=2; i<addTheseVertices.Count; i++) {
                tris.Add(vertCount);
                tris.Add(vertCount + i - 1);
                tris.Add(vertCount + i);
            }

            // TODO: UVs?
        }

        /// <summary> for each solid in an Entity, add either a Box Collider or a Mesh Collider component </summary>
        public static List<Mesh> AddColliders(GameObject gameObject, Entity ent, string namePrefix, bool forceBoxCollidersForAll = false) {
            var meshList = new List<Mesh>();

            // ignore any "illusionary" entities
            if ( ent.ClassName.ToLowerInvariant().Contains("illusionary") ) {
                return meshList;
            }

            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();

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
                
                if ( TryAddBoxCollider(gameObject, solid) ) // first, try to add a box collider
                    continue;

                // otherwise, use a mesh collider
                var newMeshCollider = AddMeshCollider(gameObject, solid);
                newMeshCollider.name = namePrefix + "-" + ent.ClassName + "-" + solid.id;
                meshList.Add( newMeshCollider ); 
            }

            return meshList;
        }

        static bool TryAddBoxCollider(GameObject gameObject, Solid solid) {
            var verts = new List<Vector3>();
            foreach ( var face in solid.Faces ) {
                if (!face.Plane.IsOrthogonal() ) {
                    return false;
                } else {
                    verts.AddRange( face.Vertices );
                }
            }
 
            if (!warnedUserAboutMultipleColliders) {
                Debug.LogWarning(warningMessage);
                warnedUserAboutMultipleColliders = true;
            }

            var bounds = GeometryUtility.CalculateBounds(verts.ToArray(), Matrix4x4.identity);
            var boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.center = bounds.center;
            boxCol.size = bounds.size;
            return true;
        }

        static Mesh AddMeshCollider(GameObject gameObject, Solid solid) {
            var newMesh = BuildMeshFromSolid(solid, true);

            if (!warnedUserAboutMultipleColliders) {
                Debug.LogWarning(warningMessage);
                warnedUserAboutMultipleColliders = true;
            }
        
            var newMeshCollider = gameObject.AddComponent<MeshCollider>();
            newMeshCollider.sharedMesh = newMesh;
            newMeshCollider.convex = true;
            newMeshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation 
                | MeshColliderCookingOptions.EnableMeshCleaning 
                | MeshColliderCookingOptions.WeldColocatedVertices 
                | MeshColliderCookingOptions.UseFastMidphase;
            
            return newMesh;
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
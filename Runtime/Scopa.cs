using System;
using System.Linq;
using System.Collections.Generic;
using Scopa.Formats.Map.Formats;
using Scopa.Formats.Map.Objects;
using Scopa.Formats.Texture.Wad;
using Scopa.Formats.Id;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    public static class Scopa {
        public static float scalingFactor = 0.03125f; // 1/32, since 1.0 meters = 32 units

        static bool warnedUserAboutMultipleColliders = false;
        const string warningMessage = "WARNING! Unity will complain about too many colliders with same name on same object, because it may not re-import in the same order / same way again... "
            + "However, this is by design. You don't want a game object for each box colllder / a thousand box colliders. So just IGNORE UNITY'S WARNINGS.";

        /// <summary>Parses the .MAP text data into a usable data structure.</summary>
        public static MapFile ParseMap( string pathToMapFile ) {
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
            return string.IsNullOrWhiteSpace(texName) || texName.Contains("sky") || texName.Contains("trigger") || texName.Contains("clip") || texName.Contains("skip") || texName.Contains("water");
        }

        public static List<Mesh> AddGameObjectFromEntity( GameObject rootGameObject, Entity ent, string namePrefix, Material defaultMaterial ) {
            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();
            var allFaces = new List<Face>(); // used later for testing unseen faces
            var lastSolidID = -1;

            // pass 1: gather all faces for occlusion checks later + build material list + cull any faces we're obviously not going to use
            var textureLookup = new Dictionary<string, Material>();
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

                    if ( !textureLookup.ContainsKey(face.TextureName) ) {
                        var newMaterial = defaultMaterial;
                        
                        // TODO: this is a good time to grab the proper Material override
                        
                        // EDITOR ONLY: if still no better material found, then search the AssetDatabase for a matching texture name
                        #if UNITY_EDITOR
                        var searchQuery = face.TextureName + " t:Material";
                        Debug.Log("search: " + searchQuery);
                        var materialSearchGUID = AssetDatabase.FindAssets(searchQuery).FirstOrDefault();

                        // if there's multiple Materials attached to one Asset, we have to do additional filtering
                        if ( !string.IsNullOrEmpty(materialSearchGUID) ) {
                            var allAssets = AssetDatabase.LoadAllAssetsAtPath( AssetDatabase.GUIDToAssetPath(materialSearchGUID) );
                            foreach ( var asset in allAssets ) {
                                if ( asset is Material && asset.name.Contains(face.TextureName) ) {
                                    newMaterial = asset as Material;
                                    Debug.Log("found: " + newMaterial.name);
                                    break;
                                }
                            }
                        }
                        #endif

                        textureLookup.Add(face.TextureName, newMaterial);
                    }
                }
            }

            // pass 2: now build one mesh + one game object per textureName
            var meshList = new List<Mesh>();

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();
            var meshParent = new GameObject( ent.ClassName + "#" + ent.ID.ToString() );

            meshParent.transform.SetParent(rootGameObject.transform);

            foreach ( var textureKVP in textureLookup ) {
                verts.Clear();
                tris.Clear();
                uvs.Clear();
                
                foreach ( var solid in solids) {
                    BuildMeshFromSolid( solid, textureKVP.Key, false, verts, tris, uvs);
                }

                if ( verts.Count == 0 || tris.Count == 0) 
                    continue;
                    
                var mesh = new Mesh();
                mesh.name = namePrefix + "-" + ent.ClassName + "#" + ent.ID.ToString() + "-" + textureKVP.Key;
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.SetUVs(0, uvs);

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                mesh.Optimize();
                meshList.Add( mesh );
                
                #if UNITY_EDITOR
                UnityEditor.MeshUtility.SetMeshCompression(mesh, UnityEditor.ModelImporterMeshCompression.Medium);
                #endif

                // finally, add mesh as game object, while we still have all the entity information
                var newMeshObj = textureLookup.Count > 1 ? new GameObject( textureKVP.Key ) : meshParent; // but if there's only one texture name, then just reuse meshParent
                if ( newMeshObj != meshParent )
                    newMeshObj.transform.SetParent(meshParent.transform);
                newMeshObj.AddComponent<MeshFilter>().sharedMesh = mesh;
                newMeshObj.AddComponent<MeshRenderer>().sharedMaterial = textureKVP.Value;
            }

            // collision pass, now treat it all as one object and ignore texture names
            if ( !IsEntityIllusionary(ent.ClassName) )
                meshList.AddRange( Scopa.AddColliders( meshParent, ent, namePrefix ) );
            
            return meshList;
        }

        static bool IsEntityIllusionary(string className) {
            return className.Contains("illusionary");
        }

        static bool IsEntityTrigger(string className) {
            return className.Contains("trigger");
        }

        /// <summary> given a brush / solid (and optional textureFilter texture name) 
        /// either (a) adds mesh data to provided verts / tris / UV lists 
        /// OR (b) returns a mesh if no lists provided</summary>
        public static Mesh BuildMeshFromSolid(Solid solid, string textureFilter = null, bool includeDiscardedFaces = false, List<Vector3> verts = null, List<int> tris = null, List<Vector2> uvs = null) {
            bool returnMesh = false;

            if (verts == null || tris == null) {
                verts = new List<Vector3>();
                tris = new List<int>();
                uvs = new List<Vector2>();
                returnMesh = true;
            }

            foreach (var face in solid.Faces) {
                if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                    continue;

                if ( face.discardWhenBuildingMesh )
                    continue;

                if ( !string.IsNullOrEmpty(textureFilter) && textureFilter.GetHashCode() != face.TextureName.GetHashCode() )
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

                BuildFaceMesh(face, verts, tris, uvs);
            }

            if ( !returnMesh ) {
                return null;
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.Optimize();

            #if UNITY_EDITOR
            UnityEditor.MeshUtility.SetMeshCompression(mesh, UnityEditor.ModelImporterMeshCompression.Medium);
            #endif

            return mesh;
        }

        /// <summary> build mesh fragment (verts / tris / uvs), usually run for each face of a solid </summary>
        static void BuildFaceMesh(Face face, List<Vector3> verts, List<int> tris, List<Vector2> uvs, int textureWidth = 1, int textureHeight = 1) {
            var lastVertIndexOfList = verts.Count;

            // add all verts and UVs
            for( int v=0; v<face.Vertices.Count; v++) {
                verts.Add(face.Vertices[v]);

                uvs.Add(new Vector2(
                    (Vector3.Dot(face.Vertices[v], face.UAxis / face.XScale) + (face.XShift % textureWidth)) / textureWidth,
                    (Vector3.Dot(face.Vertices[v], face.VAxis / face.YScale) + (face.YShift % textureHeight)) / textureHeight
                ));
            }

            // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
            for(int i=2; i<face.Vertices.Count; i++) {
                tris.Add(lastVertIndexOfList);
                tris.Add(lastVertIndexOfList + i - 1);
                tris.Add(lastVertIndexOfList + i);
            }
        }

        /// <summary> for each solid in an Entity, add either a Box Collider or a Mesh Collider component </summary>
        public static List<Mesh> AddColliders(GameObject gameObject, Entity ent, string namePrefix, bool forceBoxCollidersForAll = false) {
            var meshList = new List<Mesh>();

            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();
            bool isTrigger = IsEntityTrigger(ent.ClassName);

            foreach ( var solid in solids ) {
                // ignore solids that are textured in all invisible textures
                // bool exclude = true;
                // foreach ( var face in solid.Faces ) {
                //     if ( !IsExcludedTexName(face.TextureName) ) {
                //         exclude = false;
                //         break;
                //     }
                // }
                // if ( exclude )
                //     continue;
                
                if ( TryAddBoxCollider(gameObject, solid, isTrigger) ) // first, try to add a box collider
                    continue;

                // otherwise, use a mesh collider
                var newMeshCollider = AddMeshCollider(gameObject, solid, isTrigger);
                newMeshCollider.name = namePrefix + "-" + ent.ClassName + "#" + solid.id + "-Collider";
                meshList.Add( newMeshCollider ); 
            }

            return meshList;
        }

        static bool TryAddBoxCollider(GameObject gameObject, Solid solid, bool isTrigger = false) {
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
            boxCol.isTrigger = isTrigger;
            return true;
        }

        static Mesh AddMeshCollider(GameObject gameObject, Solid solid, bool isTrigger = false) {
            var newMesh = BuildMeshFromSolid(solid, null, true);

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
            newMeshCollider.isTrigger = isTrigger;
            
            return newMesh;
        }

        static float Frac(float decimalNumber) {
            if ( Mathf.Round(decimalNumber) > decimalNumber ) {
                return (Mathf.Ceil(decimalNumber) - decimalNumber);
            } else {
                return (decimalNumber - Mathf.Floor(decimalNumber));
            }
        }

        public static WadFile ParseWad(string fileName)
        {
            using (var fStream = System.IO.File.OpenRead(fileName))
            {
                var newWad = new WadFile(fStream);
                newWad.Name = System.IO.Path.GetFileNameWithoutExtension(fileName);
                return newWad;
            }
        }

        public static List<Texture2D> BuildWadTextures(WadFile wad, bool compressTextures = true) {
            if ( wad == null || wad.Entries == null || wad.Entries.Count == 0) {
                Debug.LogError("Couldn't parse WAD file " + wad.Name);
            }

            var textureList = new List<Texture2D>();

            foreach ( var entry in wad.Entries ) {
                // Debug.Log(entry.Name + " : " + entry.Type);
                if ( entry.Type != LumpType.RawTexture && entry.Type != LumpType.MipTexture )
                    continue;

                var texData = (wad.GetLump(entry) as MipTexture);
                // Debug.Log( "BITMAP: " + string.Join(", ", texData.MipData[0].Select( b => b.ToString() )) );
                // Debug.Log( "PALETTE: " + string.Join(", ", texData.Palette.Select( b => b.ToString() )) );

                // Half-Life GoldSrc textures use individualized 256 color palettes
                var width = System.Convert.ToInt32(texData.Width);
                var height = System.Convert.ToInt32(texData.Height);
                var palette = new Color32[256];
                for (int i=0; i<palette.Length; i++) {
                    palette[i] = new Color32( texData.Palette[i*3], texData.Palette[i*3+1], texData.Palette[i*3+2], 0xff );
                }
                
                var mipSize = texData.MipData[0].Length;
                var pixels = new Color32[mipSize];

                // for some reason, WAD texture bytes are flipped? have to unflip them for Unity
                for( int y=0; y < height; y++) {
                    for (int x=0; x < width; x++) {
                        pixels[y*width+x] = palette[ texData.MipData[0][(height-1-y)*width + x] ];

                        // TODO: in Quake WADs, some colors are reserved as fullbright colors / for transparency

                        // TODO: in Half-Life WADs, blue 255 is reserved for transparency
                    }
                }

                // we have all pixel color data now, so we can build the Texture2D
                var newTexture = new Texture2D( width, height, TextureFormat.RGB24, true);
                newTexture.name = texData.Name.ToLowerInvariant().Replace("*", "").Replace("+", "").Replace("{", "");
                newTexture.SetPixels32(pixels);
                newTexture.Apply();
                if ( compressTextures ) {
                    newTexture.Compress(false);
                }
                textureList.Add( newTexture );
                
            }

            return textureList;
        }

    }

}
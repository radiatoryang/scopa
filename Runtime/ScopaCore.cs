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
    public static class ScopaCore {
        // public static float scalingFactor = 0.03125f; // 1/32, since 1.0 meters = 32 units

        public static bool warnedUserAboutMultipleColliders {get; private set;}
        public const string colliderWarningMessage = "WARNING: upon import, Unity will complain about too many colliders with same name/type on same object. "
            + "However, this is by design: for a thousand box colliders, it's worse to make a game object for each.";

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

        /// <summary>The main function for converting parsed MapFile data into a Unity GameObject with 3D mesh and colliders.
        /// Outputs a lists of built meshes (e.g. so UnityEditor can serialize them)</summary>
        public static GameObject BuildMapIntoGameObject( MapFile mapFile, Material defaultMaterial, ScopaMapConfig config, out List<Mesh> meshList ) {
            var rootGameObject = new GameObject( mapFile.name );
            // gameObject.AddComponent<ScopaBehaviour>().mapFileData = mapFile;

            warnedUserAboutMultipleColliders = false;
            meshList = ScopaCore.AddGameObjectFromEntityRecursive(rootGameObject, mapFile.Worldspawn, mapFile.name, defaultMaterial, config);

            return rootGameObject;
        }

        /// <summary> The main core function for converting entities (worldspawn, func_, etc.) into 3D meshes. </summary>
        public static List<Mesh> AddGameObjectFromEntityRecursive( GameObject rootGameObject, Entity ent, string namePrefix, Material defaultMaterial, ScopaMapConfig config ) {
            var allMeshes = new List<Mesh>();

            var newMeshes = AddGameObjectFromEntity(rootGameObject, ent, namePrefix, defaultMaterial, config) ;
            if ( newMeshes != null )
                allMeshes.AddRange( newMeshes );

            foreach ( var child in ent.Children ) {
                if ( child is Entity ) {
                    var newMeshChildren = AddGameObjectFromEntityRecursive(rootGameObject, child as Entity, namePrefix, defaultMaterial, config);
                    if ( newMeshChildren.Count > 0)
                        allMeshes.AddRange( newMeshChildren );
                }
            }

            return allMeshes;
        }

        public static List<Mesh> AddGameObjectFromEntity( GameObject rootGameObject, Entity ent, string namePrefix, Material defaultMaterial, ScopaMapConfig config ) {
            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();
            var allFaces = new List<Face>(); // used later for testing unseen faces
            var lastSolidID = -1;

            var smoothNormalAngle = -1;
            if (ent.Properties.ContainsKey("_phong") && ent.Properties["_phong"] == "1") {
                // Debug.Log("phong?");
                if ( ent.Properties.ContainsKey("_phong_angle") ) {
                    smoothNormalAngle = Mathf.RoundToInt( float.Parse(ent.Properties["_phong_angle"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) );
                } else {
                    smoothNormalAngle = 89;
                }
            }

            // pass 1: gather all faces for occlusion checks later + build material list + cull any faces we're obviously not going to use
            var textureLookup = new Dictionary<string, Material>();
            foreach (var solid in solids) {
                lastSolidID = solid.id;
                foreach (var face in solid.Faces) {
                    if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                        continue;

                    // skip tool textures and other objects?
                    if ( config.IsTextureNameCulled(face.TextureName) ) {
                        face.discardWhenBuildingMesh = true;
                        continue;
                    }
                    
                    allFaces.Add(face);

                    if ( !textureLookup.ContainsKey(face.TextureName) ) {
                        var newMaterial = defaultMaterial;
                        var materialOverride = config.GetMaterialOverrideFor(face.TextureName);

                        if ( materialOverride != null) {
                            newMaterial = materialOverride;
                        }
                        
                        // EDITOR ONLY: if still no better material found, then search the AssetDatabase for a matching texture name
                        #if UNITY_EDITOR
                        if ( config.findMaterials && materialOverride == null ) {
                            var searchQuery = face.TextureName + " t:Material";
                            // Debug.Log("search: " + searchQuery);
                            var materialSearchGUID = AssetDatabase.FindAssets(searchQuery).FirstOrDefault();

                            // if there's multiple Materials attached to one Asset, we have to do additional filtering
                            if ( !string.IsNullOrEmpty(materialSearchGUID) ) {
                                var allAssets = AssetDatabase.LoadAllAssetsAtPath( AssetDatabase.GUIDToAssetPath(materialSearchGUID) );
                                foreach ( var asset in allAssets ) {
                                    if ( asset is Material && asset.name.Contains(face.TextureName) ) {
                                        newMaterial = asset as Material;
                                        // Debug.Log("found: " + newMaterial.name);
                                        break;
                                    }
                                }
                            }
                        }
                        #endif

                        // merge entries with the same Material by renaming the face's texture name
                        if (textureLookup.ContainsValue(newMaterial)) {
                            face.TextureName = textureLookup.Where( kvp => kvp.Value == newMaterial).FirstOrDefault().Key;
                        } else { // otherwise add to lookup
                            textureLookup.Add( face.TextureName, newMaterial );
                        }
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
                    var matName = textureKVP.Value.name;
                    if ( textureKVP.Value == defaultMaterial ) {
                        textureKVP.Value.name = textureKVP.Key;
                    }
                    BuildMeshFromSolid( solid, config, textureKVP.Value, false, verts, tris, uvs);
                    textureKVP.Value.name = matName;
                }

                if ( verts.Count == 0 || tris.Count == 0) 
                    continue;
                    
                var mesh = new Mesh();
                mesh.name = namePrefix + "-" + ent.ClassName + "#" + ent.ID.ToString() + "-" + textureKVP.Key;
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.SetUVs(0, uvs);

                mesh.RecalculateBounds();
                if ( smoothNormalAngle > 0) {
                    // Debug.Log("phong shading!");
                    mesh.RecalculateNormals(smoothNormalAngle);
                } else {
                    // mesh.SnapVertices();
                    mesh.RecalculateNormals(0);
                }
                mesh.RecalculateTangents();
                meshList.Add( mesh );
                
                #if UNITY_EDITOR
                Unwrapping.GenerateSecondaryUVSet( mesh );
                UnityEditor.MeshUtility.SetMeshCompression(mesh, UnityEditor.ModelImporterMeshCompression.Low);
                #endif

                mesh.Optimize();

                // finally, add mesh as game object, while we still have all the entity information
                var newMeshObj = textureLookup.Count > 1 ? new GameObject( textureKVP.Key ) : meshParent; // but if there's only one texture name, then just reuse meshParent
                if ( newMeshObj != meshParent )
                    newMeshObj.transform.SetParent(meshParent.transform);
                newMeshObj.AddComponent<MeshFilter>().sharedMesh = mesh;
                newMeshObj.AddComponent<MeshRenderer>().sharedMaterial = textureKVP.Value;
            }

            // collision pass, now treat it all as one object and ignore texture names
            if ( config.colliderMode != ScopaMapConfig.ColliderImportMode.None && !config.IsEntityNonsolid(ent.ClassName) )
                meshList.AddRange( ScopaCore.AddColliders( meshParent, ent, config, namePrefix ) );
            
            return meshList;
        }

        /// <summary> given a brush / solid (and optional textureFilter texture name) 
        /// either (a) adds mesh data to provided verts / tris / UV lists 
        /// OR (b) returns a mesh if no lists provided</summary>
        public static Mesh BuildMeshFromSolid(Solid solid, ScopaMapConfig config, Material textureFilter = null, bool includeDiscardedFaces = false, List<Vector3> verts = null, List<int> tris = null, List<Vector2> uvs = null) {
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

                if ( textureFilter != null && textureFilter.name.GetHashCode() != face.TextureName.GetHashCode() )
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

                BuildFaceMesh(face, config.scalingFactor, verts, tris, uvs, textureFilter != null ? textureFilter.mainTexture.width : config.defaultTexSize, textureFilter != null ? textureFilter.mainTexture.height : config.defaultTexSize);
            }

            if ( !returnMesh ) {
                return null;
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);

            // mesh.SnapVertices();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.Optimize();

            #if UNITY_EDITOR
            UnityEditor.MeshUtility.SetMeshCompression(mesh, UnityEditor.ModelImporterMeshCompression.Medium);
            #endif

            return mesh;
        }

        /// <summary> build mesh fragment (verts / tris / uvs), usually run for each face of a solid </summary>
        static void BuildFaceMesh(Face face, float scalingFactor, List<Vector3> verts, List<int> tris, List<Vector2> uvs, int textureWidth = 128, int textureHeight = 128) {
            var lastVertIndexOfList = verts.Count;

            // add all verts and UVs
            for( int v=0; v<face.Vertices.Count; v++) {
                verts.Add(face.Vertices[v] * scalingFactor);

                uvs.Add(new Vector2(
                    (Vector3.Dot(face.Vertices[v], face.UAxis / face.XScale) + (face.XShift % textureWidth)) / (textureWidth),
                    (Vector3.Dot(face.Vertices[v], face.VAxis / -face.YScale) + (-face.YShift % textureHeight)) / (textureHeight)
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
        public static List<Mesh> AddColliders(GameObject gameObject, Entity ent, ScopaMapConfig config, string namePrefix, bool forceBoxCollidersForAll = false) {
            var meshList = new List<Mesh>();

            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();
            bool isTrigger = config.IsEntityTrigger(ent.ClassName);

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
                
                if ( config.colliderMode != ScopaMapConfig.ColliderImportMode.ConvexMeshColliderOnly && TryAddBoxCollider(gameObject, solid, config, isTrigger) ) // first, try to add a box collider
                    continue;

                // otherwise, use a mesh collider
                var newMeshCollider = AddMeshCollider(gameObject, solid, config, isTrigger);
                newMeshCollider.name = namePrefix + "-" + ent.ClassName + "#" + solid.id + "-Collider";
                meshList.Add( newMeshCollider ); 
            }

            return meshList;
        }

        /// <summary> when we generate many Box Colliders for one object, they all have the same reference path and the import isn't deterministic... and Unity throws a lot of warnings at the user. This is a warning about the warnings. </summary>
        public static void ShowColliderWarning(bool always=false) {
            if (always || !warnedUserAboutMultipleColliders) {
                Debug.LogWarning(colliderWarningMessage);
                warnedUserAboutMultipleColliders = true;
            }
        }

        /// <summary> given a brush solid, calculate the AABB bounds for all its vertices, and add that Box Collider to the gameObject </summary>
        static bool TryAddBoxCollider(GameObject gameObject, Solid solid, ScopaMapConfig config, bool isTrigger = false) {
            var verts = new List<Vector3>();

            foreach ( var face in solid.Faces ) {
                if ( config.colliderMode != ScopaMapConfig.ColliderImportMode.BoxColliderOnly && !face.Plane.IsOrthogonal() ) {
                    return false;
                } else {
                    verts.AddRange( face.Vertices );
                }
            }
 
            ShowColliderWarning();

            var bounds = GeometryUtility.CalculateBounds(verts.ToArray(), Matrix4x4.Scale(Vector3.one * config.scalingFactor));
            var boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.center = bounds.center;
            boxCol.size = bounds.size;
            boxCol.isTrigger = isTrigger;
            return true;
        }

        /// <summary> given a brush solid, build a convex mesh from its vertices, and add that Mesh Collider to the gameObject </summary>
        static Mesh AddMeshCollider(GameObject gameObject, Solid solid, ScopaMapConfig config, bool isTrigger = false) {
            var newMesh = BuildMeshFromSolid(solid, config, null, true);

            ShowColliderWarning();
        
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

        public static List<Texture2D> BuildWadTextures(WadFile wad, ScopaWadConfig config) {
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
                // the last color is reserved for transparency
                // palette[255].a = 0x00;
                palette[255] = new Color32(0x00, 0x00, 0x00, 0x00);
                
                var mipSize = texData.MipData[0].Length;
                var pixels = new Color32[mipSize];
                var usesTransparency = false;

                // for some reason, WAD texture bytes are flipped? have to unflip them for Unity
                for( int y=0; y < height; y++) {
                    for (int x=0; x < width; x++) {
                        int paletteIndex = texData.MipData[0][(height-1-y)*width + x];
                        pixels[y*width+x] = palette[paletteIndex];
                        if ( !usesTransparency && paletteIndex == 255) {
                            usesTransparency = true;
                        }
                    }
                }

                // we have all pixel color data now, so we can build the Texture2D
                var newTexture = new Texture2D( width, height, usesTransparency ? TextureFormat.RGBA32 : TextureFormat.RGB24, true, config.linearColorspace);
                newTexture.name = texData.Name.ToLowerInvariant().Replace("*", "").Replace("+", "").Replace("{", "");
                newTexture.SetPixels32(pixels);
                newTexture.alphaIsTransparency = usesTransparency;
                newTexture.filterMode = config.filterMode;
                newTexture.anisoLevel = config.anisoLevel;
                newTexture.Apply();
                if ( config.compressTextures ) {
                    newTexture.Compress(false);
                }
                textureList.Add( newTexture );
                
            }

            return textureList;
        }

        public static Material BuildMaterialForTexture( Texture2D texture, ScopaWadConfig config ) {
            var material = texture.alphaIsTransparency ? 
                (config.alphaTemplate != null ? config.alphaTemplate : GenerateDefaultMaterialAlpha())
                : (config.opaqueTemplate != null ? config.opaqueTemplate : GenerateDefaultMaterialOpaque());
            material.name = texture.name;
            material.mainTexture = texture;

            return material;
        }

        public static Material GenerateDefaultMaterialOpaque() {
            // TODO: URP, HDRP
            var material = new Material( Shader.Find("Standard") );
            material.SetFloat("_Glossiness", 0.16f);
            return material;
        }

        public static Material GenerateDefaultMaterialAlpha() {
            // TODO: URP, HDRP
            var material = new Material( Shader.Find("Standard") );
            material.SetFloat("_Glossiness", 0.16f);
            material.SetFloat("_Mode", 1);
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 2450;
            return material;
        }

    }

}
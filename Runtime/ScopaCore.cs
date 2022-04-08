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
        public static bool warnedUserAboutMultipleColliders {get; private set;}
        public const string colliderWarningMessage = "WARNING: Unity may complain about 'identifier uniqueness violations', importing too many colliders with same name and type. Ignore it.";

        // to avoid GC, we use big static lists so we just allocate once
        static List<Face> allFaces = new List<Face>();
        static List<Vector3> verts = new List<Vector3>(4096);
        static List<int> tris = new List<int>(8192);
        static List<Vector2> uvs = new List<Vector2>(4096);
        
        static Color32[] palette = new Color32[256];
        

        // (editor only) search for all materials in the project once per import, save results here
        static Dictionary<string, Material> materials = new Dictionary<string, Material>(512);

        /// <summary>Parses the .MAP text data into a usable data structure.</summary>
        public static MapFile ParseMap( string pathToMapFile, ScopaMapConfig config ) {
            IMapFormat importer = null;
            if ( pathToMapFile.EndsWith(".map")) {
                importer = new QuakeMapFormat();
            } 

            if ( importer == null) {
                Debug.LogError($"No file importer found for {pathToMapFile}");
                return null;
            }

            Solid.weldingThreshold = config.weldingThreshold;
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
            CacheMaterialSearch();
            meshList = ScopaCore.AddGameObjectFromEntityRecursive(rootGameObject, mapFile.Worldspawn, mapFile.name, defaultMaterial, config);

            return rootGameObject;
        }

        static void CacheMaterialSearch() {
            #if UNITY_EDITOR
            materials.Clear();
            var materialSearch = AssetDatabase.FindAssets("t:Material");
            foreach ( var materialSearchGUID in materialSearch) {
                // if there's multiple Materials attached to one Asset, we have to do additional filtering
                var allAssets = AssetDatabase.LoadAllAssetsAtPath( AssetDatabase.GUIDToAssetPath(materialSearchGUID) );
                foreach ( var asset in allAssets ) {
                    if ( !materials.ContainsKey(asset.name) && asset is Material ) {
                        materials.Add(asset.name, asset as Material);
                    }
                }
            }
            #endif
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

        public static List<Mesh> AddGameObjectFromEntity( GameObject rootGameObject, Entity entData, string namePrefix, Material defaultMaterial, ScopaMapConfig config ) {
            var solids = entData.Children.Where( x => x is Solid).Cast<Solid>();
            allFaces.Clear(); // used later for testing unseen faces
            var lastSolidID = -1;

            // detect per-entity smoothing angle, if defined
            var smoothNormalAngle = 0;
            if (entData.TryGetInt("_phong", out var phong) && phong >= 1) {
                if ( entData.TryGetFloat("_phong_angle", out var phongAngle) ) {
                    smoothNormalAngle = Mathf.RoundToInt( phongAngle );
                } else {
                    smoothNormalAngle = 89;
                }
            }

            // for worldspawn, pivot point should be 0, 0, 0... else, see if origin is defined... otherwise, calculate min of bounds
            var calculateOrigin = entData.ClassName.ToLowerInvariant() != "worldspawn";
            var entityOrigin = calculateOrigin ? Vector3.one * 999999 : Vector3.zero;
            if ( entData.TryGetVector3Unscaled("origin", out var newVec) ) {
                entityOrigin = newVec;
                calculateOrigin = false;
            }

            var needsCollider = !config.IsEntityNonsolid(entData.ClassName);
            var isTrigger = config.IsEntityTrigger(entData.ClassName);

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
                    
                    allFaces.Add(face); // TODO: we need this for per-entity optimization later

                    // start calculating min bounds, if needed
                    if ( calculateOrigin ) {
                        for(int i=0; i<face.Vertices.Count; i++) {
                            entityOrigin.x = Mathf.Min(entityOrigin.x, face.Vertices[i].x);
                            entityOrigin.y = Mathf.Min(entityOrigin.y, face.Vertices[i].y);
                            entityOrigin.z = Mathf.Min(entityOrigin.z, face.Vertices[i].z);
                        }
                    }

                    // match this face's texture name to a material
                    if ( !textureLookup.ContainsKey(face.TextureName) ) {
                        var newMaterial = defaultMaterial;

                        var materialOverride = config.GetMaterialOverrideFor(face.TextureName);
                        if ( materialOverride != null) {
                            newMaterial = materialOverride;
                        }
                        
                        // if still no better material found, then search the AssetDatabase for a matching texture name
                        if ( config.findMaterials && materialOverride == null && materials.Count > 0 && materials.ContainsKey(face.TextureName) ) {
                            newMaterial = materials[face.TextureName];
                        }

                        // temporarily merge entries with the same Material by renaming the face's texture name
                        if (textureLookup.ContainsValue(newMaterial)) {
                            face.TextureName = textureLookup.Where( kvp => kvp.Value == newMaterial).FirstOrDefault().Key;
                        } else { // otherwise add to lookup
                            textureLookup.Add( face.TextureName, newMaterial );
                        }
                    }
                }
            }
            entityOrigin *= config.scalingFactor;

            // pass 2: now build one mesh + one game object per textureName
            var meshList = new List<Mesh>();

            // user can specify a template entityPrefab if desired
            var entityPrefab = config.GetEntityPrefabFor(entData.ClassName);
            var meshPrefab = config.GetMeshPrefabFor(entData.ClassName);

            var entityObject = entityPrefab != null ? Instantiate(entityPrefab) : new GameObject();

            entityObject.name = entData.ClassName + "#" + entData.ID.ToString();
            entityObject.transform.position = entityOrigin;
            entityObject.transform.localRotation = Quaternion.identity;
            entityObject.transform.localScale = Vector3.one;
            entityObject.transform.SetParent(rootGameObject.transform);

            // only set Layer if it's a generic game object
            if ( entityPrefab == null) { 
                entityObject.layer = config.layer;
            }

            // main loop: for each material, build a mesh and add a game object with mesh components
            foreach ( var textureKVP in textureLookup ) {
                ClearMeshBuffers();
                
                foreach ( var solid in solids) {
                    var matName = textureKVP.Value.name;
                    if ( textureKVP.Value == defaultMaterial ) {
                        textureKVP.Value.name = textureKVP.Key;
                    }
                    BufferMeshDataFromSolid( solid, config, textureKVP.Value, false);
                    textureKVP.Value.name = matName;
                }

                if ( verts.Count == 0 || tris.Count == 0) 
                    continue;
                    
                var newMesh = BuildMeshFromBuffers(namePrefix + "-" + entData.ClassName + "#" + entData.ID.ToString() + "-" + textureKVP.Key, config, entityOrigin, smoothNormalAngle);
                meshList.Add(newMesh);

                // finally, add mesh as game object, while we still have all the entity information
                var newMeshObj = meshPrefab != null ? Instantiate(meshPrefab) : new GameObject();

                newMeshObj.name = textureKVP.Key;
                newMeshObj.transform.SetParent(entityObject.transform);
                newMeshObj.transform.localPosition = Vector3.zero;
                newMeshObj.transform.localRotation = Quaternion.identity;
                newMeshObj.transform.localScale = Vector3.one;

                // if user set a specific prefab, it probably has its own static flags and layer
                // ... but if it's a generic game object we made, then we set it ourselves
                if ( meshPrefab == null ) { 
                    newMeshObj.layer = config.layer;
                    if (config.isStatic) {
                        SetGameObjectStatic(newMeshObj, needsCollider && !isTrigger);
                    }
                }

                // populate components... if the mesh components aren't there, then add them
                var meshFilter = newMeshObj.GetComponent<MeshFilter>() ? newMeshObj.GetComponent<MeshFilter>() : newMeshObj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = newMesh;
                var meshRenderer = newMeshObj.GetComponent<MeshRenderer>() ? newMeshObj.GetComponent<MeshRenderer>() : newMeshObj.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = textureKVP.Value;
            }

            // collision pass, now treat it all as one object and ignore texture names
            if ( config.colliderMode != ScopaMapConfig.ColliderImportMode.None && needsCollider )
                meshList.AddRange( ScopaCore.AddColliders( entityObject, entData, config, namePrefix ) );
            
            return meshList;
        }

        static GameObject Instantiate(GameObject prefab) {
            // using InstantiatePrefab didn't actually help, since the instance still doesn't auto-update and requires manual reimport anyway
            // #if UNITY_EDITOR
            //     return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            // #else
                return UnityEngine.Object.Instantiate(prefab);
            // #endif
        }

        static void SetGameObjectStatic(GameObject go, bool isNavigationStatic = true) {
            if ( isNavigationStatic ) {
                go.isStatic = true;
            } else {
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI 
                    | StaticEditorFlags.OccluderStatic 
                    | StaticEditorFlags.BatchingStatic 
                    | StaticEditorFlags.OccludeeStatic 
                    | StaticEditorFlags.OffMeshLinkGeneration 
                    | StaticEditorFlags.ReflectionProbeStatic
                );
            }
        }

        /// <summary> given a brush / solid (and optional textureFilter texture name) it generates mesh data for verts / tris / UV list buffers
        ///  but if returnMesh is true, then it will also CLEAR THOSE BUFFERS and GENERATE A MESH </summary>
        public static void BufferMeshDataFromSolid(Solid solid, ScopaMapConfig config, Material textureFilter = null, bool includeDiscardedFaces = false) {
           
            foreach (var face in solid.Faces) {
                if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                    continue;

                if ( face.discardWhenBuildingMesh )
                    continue;

                if ( textureFilter != null && textureFilter.name.GetHashCode() != face.TextureName.GetHashCode() )
                    continue;

                // test for unseen / hidden faces, and discard
                foreach( var otherFace in allFaces ) {
                    if (otherFace.OccludesFace(face)) {
                        Debug.Log("discarding unseen face at " + face);
                        face.DebugDrawVerts(Color.yellow);
                        face.discardWhenBuildingMesh = true;
                    }
                }

                if ( face.discardWhenBuildingMesh )
                    continue;

                BufferScaledMeshDataForFace(face, config.scalingFactor, verts, tris, uvs, textureFilter != null ? textureFilter.mainTexture.width : config.defaultTexSize, textureFilter != null ? textureFilter.mainTexture.height : config.defaultTexSize);
            }
        }

        /// <summary> utility function that actually generates the Mesh object </summary>
        static Mesh BuildMeshFromBuffers(string meshName, ScopaMapConfig config, Vector3 meshOrigin = default(Vector3), float smoothNormalAngle = 0) {
            var mesh = new Mesh();
            mesh.name = meshName;

            if ( meshOrigin != default(Vector3) ) {
                for(int i=0; i<verts.Count; i++) {
                    verts[i] -= meshOrigin;
                }
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);

            mesh.RecalculateBounds();
            if ( smoothNormalAngle > 0.1 )
                mesh.RecalculateNormals(smoothNormalAngle);
            else if ( smoothNormalAngle >= 0)
                mesh.RecalculateNormals(); // built-in Unity method

            if ( config.addTangents && smoothNormalAngle >= 0 )
                mesh.RecalculateTangents();
            
            #if UNITY_EDITOR
            if ( config.addLightmapUV2 )
                Unwrapping.GenerateSecondaryUVSet( mesh );

            if ( config.meshCompression != ScopaMapConfig.ModelImporterMeshCompression.Off)
                UnityEditor.MeshUtility.SetMeshCompression(mesh, (ModelImporterMeshCompression)config.meshCompression);
            #endif

            mesh.Optimize();

            return mesh;
        }

        /// <summary> build mesh fragment (verts / tris / uvs), usually run for each face of a solid </summary>
        static void BufferScaledMeshDataForFace(Face face, float scalingFactor, List<Vector3> verts, List<int> tris, List<Vector2> uvs, int textureWidth = 128, int textureHeight = 128) {
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

        /// <summary> for each solid in an Entity, add either a Box Collider or a Mesh Collider component... or make one big merged Mesh Collider </summary>
        public static List<Mesh> AddColliders(GameObject gameObject, Entity ent, ScopaMapConfig config, string namePrefix, bool forceBoxCollidersForAll = false) {
            var meshList = new List<Mesh>();

            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();
            if ( solids.Count() == 0)
                return meshList;

            bool isTrigger = config.IsEntityTrigger(ent.ClassName);

            // just one big Mesh Collider... one collider to rule them all
            if ( !isTrigger && config.colliderMode == ScopaMapConfig.ColliderImportMode.MergeAllToOneConcaveMeshCollider ) {
                ClearMeshBuffers();
                foreach ( var solid in solids ) {
                    BufferMeshDataFromSolid(solid, config, null, true);
                }

                var newMesh = BuildMeshFromBuffers(namePrefix + "-" + ent.ClassName + "#" + ent.ID.ToString() + "-Collider", config, gameObject.transform.position, -1 );
                var newMeshCollider = gameObject.AddComponent<MeshCollider>();
                newMeshCollider.convex = ent.TryGetInt("_convex", out var num) && num == 1;
                newMeshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation 
                    | MeshColliderCookingOptions.EnableMeshCleaning 
                    | MeshColliderCookingOptions.WeldColocatedVertices 
                    | MeshColliderCookingOptions.UseFastMidphase;
                newMeshCollider.isTrigger = isTrigger;
                newMeshCollider.sharedMesh = newMesh;

                meshList.Add( newMesh );

            } // otherwise, generate individual colliders for each brush solid
            else 
            { 
                foreach ( var solid in solids ) {   
                    // box collider is the simplest, so we should always try it first       
                    if ( config.colliderMode != ScopaMapConfig.ColliderImportMode.ConvexMeshColliderOnly && TryAddBoxCollider(gameObject, solid, config, isTrigger) ) 
                        continue;

                    // otherwise, use a convex mesh collider
                    var newMeshCollider = AddMeshCollider(gameObject, solid, config, isTrigger);
                    newMeshCollider.name = namePrefix + "-" + ent.ClassName + "#" + solid.id + "-Collider";
                    meshList.Add( newMeshCollider ); 
                }
            }

            return meshList;
        }

        /// <summary> when we generate many colliders for one object, they all have the same reference path and the import isn't deterministic... and Unity throws a lot of warnings at the user. This is a warning about the warnings. </summary>
        public static void ShowColliderWarning(bool always=false) {
            if (Application.isEditor && (always || !warnedUserAboutMultipleColliders) ) {
                Debug.LogWarning(colliderWarningMessage);
                warnedUserAboutMultipleColliders = true;
            }
        }

        /// <summary> given a brush solid, calculate the AABB bounds for all its vertices, and add that Box Collider to the gameObject </summary>
        static bool TryAddBoxCollider(GameObject gameObject, Solid solid, ScopaMapConfig config, bool isTrigger = false) {
            verts.Clear();

            for ( int x=0; x<solid.Faces.Count; x++ ) {
                if ( config.colliderMode != ScopaMapConfig.ColliderImportMode.BoxColliderOnly && !solid.Faces[x].Plane.IsOrthogonal() ) {
                    return false;
                } else {
                    for( int y=0; y<solid.Faces[x].Vertices.Count; y++) {
                        verts.Add( solid.Faces[x].Vertices[y] * config.scalingFactor - gameObject.transform.position);
                    }
                }
            }
 
            ShowColliderWarning();

            var bounds = GeometryUtility.CalculateBounds(verts.ToArray(), Matrix4x4.identity);
            var boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.center = bounds.center;
            boxCol.size = bounds.size;
            boxCol.isTrigger = isTrigger;
            return true;
        }

        /// <summary> given a brush solid, build a convex mesh from its vertices, and add that Mesh Collider to the gameObject </summary>
        static Mesh AddMeshCollider(GameObject gameObject, Solid solid, ScopaMapConfig config, bool isTrigger = false) {
            ClearMeshBuffers();
            BufferMeshDataFromSolid(solid, config, null, true);
            var newMesh = BuildMeshFromBuffers( solid.id.ToString() + "-Collider", config, gameObject.transform.position, -1);

            ShowColliderWarning();
        
            var newMeshCollider = gameObject.AddComponent<MeshCollider>();
            newMeshCollider.convex = true;
            newMeshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation 
                | MeshColliderCookingOptions.EnableMeshCleaning 
                | MeshColliderCookingOptions.WeldColocatedVertices 
                | MeshColliderCookingOptions.UseFastMidphase;
            newMeshCollider.isTrigger = isTrigger;
            newMeshCollider.sharedMesh = newMesh;
            
            return newMesh;
        }

        static float Frac(float decimalNumber) {
            if ( Mathf.Round(decimalNumber) > decimalNumber ) {
                return (Mathf.Ceil(decimalNumber) - decimalNumber);
            } else {
                return (decimalNumber - Mathf.Floor(decimalNumber));
            }
        }

        static void ClearMeshBuffers() {
            verts.Clear();
            tris.Clear();
            uvs.Clear();
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
                if ( entry.Type != LumpType.RawTexture && entry.Type != LumpType.MipTexture )
                    continue;

                var texData = (wad.GetLump(entry) as MipTexture);
                // Debug.Log(entry.Name);
                // Debug.Log( "BITMAP: " + string.Join(", ", texData.MipData[0].Select( b => b.ToString() )) );
                // Debug.Log( "PALETTE: " + string.Join(", ", texData.Palette.Select( b => b.ToString() )) );

                // Half-Life GoldSrc textures use individualized 256 color palettes; Quake textures will have a reference to the hard-coded Quake palette
                var width = System.Convert.ToInt32(texData.Width);
                var height = System.Convert.ToInt32(texData.Height);

                for (int i=0; i<256; i++) {
                    palette[i] = new Color32( texData.Palette[i*3], texData.Palette[i*3+1], texData.Palette[i*3+2], 0xff );
                }

                // the last color is reserved for transparency
                var paletteHasTransparency = false;
                if ( (palette[255].r == QuakePalette.Data[255*3] && palette[255].g == QuakePalette.Data[255*3+1] && palette[255].b == QuakePalette.Data[255*3+2])
                    || (palette[255].r == 0x00 && palette[255].g == 0x00 && palette[255].b == 0xff) ) {
                    paletteHasTransparency = true;
                    palette[255] = new Color32(0x00, 0x00, 0x00, 0x00);
                }
                
                var mipSize = texData.MipData[0].Length;
                var pixels = new Color32[mipSize];
                var usesTransparency = false;

                // for some reason, WAD texture bytes are flipped? have to unflip them for Unity
                for( int y=0; y < height; y++) {
                    for (int x=0; x < width; x++) {
                        int paletteIndex = texData.MipData[0][(height-1-y)*width + x];
                        pixels[y*width+x] = palette[paletteIndex];
                        if ( !usesTransparency && paletteHasTransparency && paletteIndex == 255) {
                            usesTransparency = true;
                        }
                    }
                }

                // we have all pixel color data now, so we can build the Texture2D
                var newTexture = new Texture2D( width, height, usesTransparency ? TextureFormat.RGBA32 : TextureFormat.RGB24, true, config.linearColorspace);
                newTexture.name = texData.Name.ToLowerInvariant();
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
            material.SetFloat("_Glossiness", 0.1f);
            return material;
        }

        public static Material GenerateDefaultMaterialAlpha() {
            // TODO: URP, HDRP
            var material = new Material( Shader.Find("Standard") );
            material.SetFloat("_Glossiness", 0.1f);
            material.SetFloat("_Mode", 1);
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 2450;
            return material;
        }

    }

}
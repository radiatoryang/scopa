using UnityEngine;
using System.Collections.Generic;

namespace Scopa {
    /// <summary> processes the MapWorld data from the MapParser into Unity assets and game objects </summary>
    public class MapBaseImporter {

        /// <summary> Imports the specified world into the SabreCSG model.</summary>
        public GameObject ImportAsGameObject(Transform parent) {
            // scalar = 1.0f / scaleMeter;

            // create a material searcher to associate materials automatically.
            var materialSearcher = new MaterialSearcher();

            var mapTransform = CreateGameObjectWithUniqueName(mapData.mapName, parent);
            mapTransform.position = Vector3.zero;

            // Index of entities by trenchbroom id
            var entitiesById = new Dictionary<int, EntityContainer>();
            var layers = new List<EntityContainer>();

            for (int e = 0; e < mapData.Entities.Count; e++) {
                var entity = mapData.Entities[e];
                //EntityContainer eContainer = null;

                if (entity.tbId >= 0) {
                    var name = String.IsNullOrEmpty(entity.tbName) ? "Unnamed" : entity.tbName;
                    var t = CreateGameObjectWithUniqueName(name, mapTransform);
                    var eContainer = new EntityContainer(t, entity);
                    entitiesById.Add(entity.tbId, eContainer);

                    if (entity.tbType == "_tb_layer") { // also read TrenchBroom layers while we're at it
                        layers.Add(eContainer);
                        eContainer.transform.SetParent(null); // unparent until layers are sorted by sort index
                    }
                }
            }

            var defaultLayer = CreateGameObjectWithUniqueName("Default Layer", mapTransform);
            layers = layers.OrderBy(l => l.entity.tbLayerSortIndex).ToList(); // sort layers by layer sort index

            foreach (var l in layers) {
                l.transform.SetParent(mapTransform); // parent layers to map in order
            }

            bool valveFormat = mapData.valveFormat;

            // iterate through all entities.
            for (int e = 0; e < mapData.Entities.Count; e++)
            {
// #if UNITY_EDITOR
//                 UnityEditor.EditorUtility.DisplayProgressBar("Scopa: Importing Map", "Converting Quake 1 Entities To Brushes (" + (e + 1) + " / " + world.Entities.Count + ")...", e / (float)world.Entities.Count);
// #endif
                MapEntity entity = mapData.Entities[e];

                Transform brushParent = mapTransform;

                bool isLayer = false;
                bool isTrigger = false;

                if (entity.ClassName == "worldspawn")
                {
                    brushParent = defaultLayer;
                }
                else if (entity.tbType == "_tb_layer")
                {
                    isLayer = true;
                    if (entitiesById.TryGetValue(entity.tbId, out EntityContainer eContainer))
                    {
                        brushParent = eContainer.transform;
                    }
                }
                else if (entity.tbType == "_tb_group")
                {
                    if (entitiesById.TryGetValue(entity.tbId, out EntityContainer eContainer))
                    {
                        brushParent = eContainer.transform;
                    }
                }
                else
                {
                    if (entity.ClassName.Contains("trigger")) isTrigger = true;

                    brushParent = CreateGameObjectWithUniqueName(entity.ClassName, mapTransform);
                }

                if (brushParent != mapTransform && brushParent != defaultLayer)
                {
                    if (entity.tbGroup > 0)
                    {
                        if (entitiesById.TryGetValue(entity.tbGroup, out EntityContainer eContainer))
                        {
                            brushParent.SetParent(eContainer.transform);
                        }
                    }
                    else if (entity.tbLayer > 0)
                    {
                        if (entitiesById.TryGetValue(entity.tbLayer, out EntityContainer eContainer))
                        {
                            brushParent.SetParent(eContainer.transform);
                        }
                    }
                    else if (!isLayer)
                    {
                        brushParent.SetParent(defaultLayer);
                    }
                }

                //if(entity.)

                if (entity.Brushes.Count == 0) continue;

                var model = ChiselModelManager.CreateNewModel(brushParent);
                // var model = OperationsUtility.CreateModelInstanceInScene(brushParent);
                var parent = model.transform;

                if (isTrigger)
                {
                    //model.Settings = (model.Settings | ModelSettingsFlags.IsTrigger | ModelSettingsFlags.SetColliderConvex | ModelSettingsFlags.DoNotRender);
                }

                // iterate through all entity brushes.
                for (int i = 0; i < entity.Brushes.Count; i++)
                {
                    MapBrush brush = entity.Brushes[i];


                    // build a very large cube brush.
                    ChiselBrush go = ChiselComponentFactory.Create<ChiselBrush>(model);
                    go.definition.surfaceDefinition = new ChiselSurfaceDefinition();
                    go.definition.surfaceDefinition.EnsureSize(6);
                    BrushMesh brushMesh = new BrushMesh();
                    go.definition.brushOutline = brushMesh;
                    BrushMeshFactory.CreateBox(ref brushMesh, new Vector3(-4096, -4096, -4096), new Vector3(4096, 4096, 4096), in go.definition.surfaceDefinition);

                    // prepare for uv calculations of clip planes after cutting.
                    var planes = new float4[brush.Sides.Count];
                    var planeSurfaces = new ChiselSurface[brush.Sides.Count];

                    // compute all the sides of the brush that will be clipped.
                    for (int j = brush.Sides.Count; j-- > 0;)
                    {
                        MapBrushSide side = brush.Sides[j];

                        // detect excluded polygons.
                        //if (IsExcludedMaterial(side.Material))
                        //polygon.UserExcludeFromFinal = true;
                        // detect collision-only brushes.
                        //if (IsInvisibleMaterial(side.Material))
                        //pr.IsVisible = false;

                        // find the material in the unity project automatically.
                        Material material;

                        string materialName = side.materialName.Replace("*", "#");
                        material = materialSearcher.FindMaterial(new string[] { materialName });
                        if (material == null)
                        {
                            material = ChiselMaterialManager.DefaultFloorMaterial;
                        }

                        // create chisel surface for the clip.
                        ChiselSurface surface = new ChiselSurface();
                        surface.brushMaterial = ChiselBrushMaterial.CreateInstance(material, ChiselMaterialManager.DefaultPhysicsMaterial);
                        surface.surfaceDescription = SurfaceDescription.Default;

                        // detect collision-only polygons.
                        if (IsInvisibleMaterial(side.materialName))
                        {
                            surface.brushMaterial.LayerUsage &= ~LayerUsageFlags.RenderReceiveCastShadows;
                        }
                        // detect excluded polygons.
                        if (IsExcludedMaterial(side.materialName))
                        {
                            surface.brushMaterial.LayerUsage &= LayerUsageFlags.CastShadows;
                            surface.brushMaterial.LayerUsage |= LayerUsageFlags.Collidable;
                        }

                        // calculate the clipping planes.
                        Plane clip = new Plane(go.transform.InverseTransformPoint(new Vector3(side.plane.P1.X, side.plane.P1.Z, side.plane.P1.Y) * worldScalar), go.transform.InverseTransformPoint(new Vector3(side.plane.P2.X, side.plane.P2.Z, side.plane.P2.Y) * worldScalar), go.transform.InverseTransformPoint(new Vector3(side.plane.P3.X, side.plane.P3.Z, side.plane.P3.Y) * worldScalar));
                        planes[j] = new float4(clip.normal, clip.distance);
                        planeSurfaces[j] = surface;
                    }

                    // cut all the clipping planes out of the brush in one go.
                    brushMesh.Cut(planes, planeSurfaces);

                    // now iterate over the planes to calculate UV coordinates.
                    int[] indices = new int[brush.Sides.Count];
                    for (int k = 0; k < planes.Length; k++)
                    {
                        var plane = planes[k];
                        int closestIndex = 0;
                        float closestDistance = math.lengthsq(plane - brushMesh.planes[0]);
                        for (int j = 1; j < brushMesh.planes.Length; j++)
                        {
                            float testDistance = math.lengthsq(plane - brushMesh.planes[j]);
                            if (testDistance < closestDistance)
                            {
                                closestIndex = j;
                                closestDistance = testDistance;
                            }
                        }
                        indices[k] = closestIndex;
                    }

                    for (int j = 0; j < indices.Length; j++)
                        brushMesh.planes[indices[j]] = planes[j];

                    for (int j = brush.Sides.Count; j-- > 0;)
                    {
                        MapBrushSide side = brush.Sides[j];

                        var surface = brushMesh.polygons[indices[j]].surface;
                        var material = surface.brushMaterial.RenderMaterial;

                        // calculate the texture coordinates.
                        int w = 256;
                        int h = 256;
                        if (material.mainTexture != null)
                        {
                            w = material.mainTexture.width;
                            h = material.mainTexture.height;
                        }
                        var clip = new Plane(planes[j].xyz, planes[j].w);

                        if (mapData.valveFormat)
                        {
                            var uAxis = new VmfAxis(side.t1, side.offset.X, side.scale.X);
                            var vAxis = new VmfAxis(side.t2, side.offset.Y, side.scale.Y);
                            CalculateTextureCoordinates(go, surface, clip, w, h, uAxis, vAxis);
                        }
                        else
                        {
                            if (GetTextureAxises(clip, out MapVector3 t1, out MapVector3 t2))
                            {
                                var uAxis = new VmfAxis(t1, side.offset.X, side.scale.X);
                                var vAxis = new VmfAxis(t2, side.offset.Y, side.scale.Y);
                                CalculateTextureCoordinates(go, surface, clip, w, h, uAxis, vAxis);
                            }
                        }
                    }

                    try
                    {
                        // finalize the brush by snapping planes and centering the pivot point.
                        go.transform.position += brushMesh.CenterAndSnapPlanes();
                    }
                    catch
                    {
                        // Brush failed, destroy brush
                        GameObject.DestroyImmediate(go);
                    }
                }
            }

// #if UNITY_EDITOR
//             UnityEditor.EditorUtility.ClearProgressBar();
// #endif

            return mapTransform;
        }

        /// <summary>
        /// Determines whether the specified name is an excluded material.
        /// </summary>
        /// <param name="name">The name of the material.</param>
        /// <returns><c>true</c> if the specified name is an excluded material; otherwise, <c>false</c>.</returns>
        private static bool IsExcludedMaterial(string name)
        {
            if (name.StartsWith("sky"))
                return true;
            return false;
        }

        /// <summary>
        /// Determines whether the specified name is an invisible material.
        /// </summary>
        /// <param name="name">The name of the material.</param>
        /// <returns><c>true</c> if the specified name is an invisible material; otherwise, <c>false</c>.</returns>
        private static bool IsInvisibleMaterial(string name)
        {
            switch (name)
            {
                case "clip":
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the specified name is a special material, these brush will not be
        /// imported into SabreCSG.
        /// </summary>
        /// <param name="name">The name of the material.</param>
        /// <returns><c>true</c> if the specified name is a special material; otherwise, <c>false</c>.</returns>
        private static bool IsSpecialMaterial(string name)
        {
            switch (name)
            {
                case "trigger":
                case "skip":
                case "waterskip":
                    return true;
            }
            return false;
        }

        private static Transform CreateGameObjectWithUniqueName(string name, Transform parent)
        {
            var go = new GameObject(UnityEditor.GameObjectUtility.GetUniqueNameForSibling(parent, name));
            go.transform.SetParent(parent);
            return go.transform;
        }

    }
}
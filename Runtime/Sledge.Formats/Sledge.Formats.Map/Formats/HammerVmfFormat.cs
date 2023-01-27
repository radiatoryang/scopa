using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Sledge.Formats.Map.Formats.VmfObjects;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Valve;

namespace Sledge.Formats.Map.Formats
{
    public class HammerVmfFormat : IMapFormat
    {
        public string Name => "Hammer VMF";
        public string Description => "The .vmf file format used by Valve Hammer Editor 4.";
        public string ApplicationName => "Hammer";
        public string Extension => "vmf";
        public string[] AdditionalExtensions => new[] { "vmx" };
        public string[] SupportedStyleHints => new string[0];

        private readonly SerialisedObjectFormatter _formatter;

        public HammerVmfFormat()
        {
            _formatter = new SerialisedObjectFormatter();
        }

        public MapFile Read(Stream stream)
        {
            var map = new MapFile();

            var objs = new List<SerialisedObject>();
            foreach (var so in _formatter.Deserialize(stream))
            {
                switch (so.Name?.ToLower())
                {
                    case "visgroups":
                        LoadVisgroups(map, so);
                        break;
                    case "cameras":
                        LoadCameras(map, so);
                        break;
                    case "world":
                    case "entity":
                        objs.Add(so);
                        break;
                    default:
                        map.AdditionalObjects.Add(so);
                        break;
                }
            }
            LoadWorld(map, objs);

            return map;
        }

        #region Read

        private void LoadWorld(MapFile map, List<SerialisedObject> objects)
        {
            var vos = objects.Select(VmfObject.Deserialise).Where(vo => vo != null).ToList();
            var world = vos.OfType<VmfWorld>().FirstOrDefault() ?? new VmfWorld(new SerialisedObject("world"));

            // A map of loaded object -> vmf id
            var mapToSource = new Dictionary<MapObject, int>();
            world.Editor.Apply(map.Worldspawn);
            mapToSource.Add(map.Worldspawn, world.ID);

            map.Worldspawn.ClassName = world.ClassName;
            map.Worldspawn.SpawnFlags = world.SpawnFlags;
            foreach (var wp in world.Properties) map.Worldspawn.Properties[wp.Key] = wp.Value;

            var tree = new List<VmfObject>();

            foreach (var vo in vos)
            {
                if (vo.Editor.ParentID == 0) vo.Editor.ParentID = world.ID;

                // Flatten the tree (nested hiddens -> no more hiddens)
                // (Flat tree includes self as well)
                var flat = vo.Flatten().ToList();

                // Set the default parent id for all the child objects
                foreach (var child in flat)
                {
                    if (child.Editor.ParentID == 0) child.Editor.ParentID = vo.ID;
                }

                // Add the objects to the tree
                tree.AddRange(flat);
            }

            world.Editor.ParentID = 0;
            tree.Remove(world);

            // All objects should have proper ids by now, get rid of anything with parentid 0 just in case
            var grouped = tree.GroupBy(x => x.Editor.ParentID).ToDictionary(x => x.Key, x => x.ToList());

            // Step through each level of the tree and add them to their parent branches
            var leaves = new List<MapObject> { map.Worldspawn };

            // Use a iteration limit of 1000. If the tree's that deep, I don't want to load your map anyway...
            for (var i = 0; i < 1000 && leaves.Any(); i++) // i.e. while (leaves.Any())
            {
                var newLeaves = new List<MapObject>();
                foreach (var leaf in leaves)
                {
                    var sourceId = mapToSource[leaf];
                    if (!grouped.ContainsKey(sourceId)) continue;

                    var items = grouped[sourceId];

                    // Create objects from items
                    foreach (var item in items)
                    {
                        var mapObject = item.ToMapObject();
                        mapToSource.Add(mapObject, item.ID);
                        leaf.Children.Add(mapObject);
                        newLeaves.Add(mapObject);
                    }
                }
                leaves = newLeaves;
            }

            // Now we should have a nice neat hierarchy of objects
        }

        private void LoadCameras(MapFile map, SerialisedObject so)
        {
            var activeCam = so.Get("activecamera", 0);

            var cams = so.Children.Where(x => string.Equals(x.Name, "camera", StringComparison.InvariantCultureIgnoreCase)).ToList();
            for (var i = 0; i < cams.Count; i++)
            {
                var cm = cams[i];
                map.Cameras.Add(new Camera
                {
                    EyePosition = cm.Get("position", Vector3.Zero),
                    LookPosition = cm.Get("look", Vector3.UnitX),
                    IsActive = activeCam == i
                });
            }
        }

        private void LoadVisgroups(MapFile map, SerialisedObject so)
        {
            var vis = new Visgroup();
            LoadVisgroupsRecursive(so, vis);
            map.Visgroups.AddRange(vis.Children);
        }

        private void LoadVisgroupsRecursive(SerialisedObject so, Visgroup parent)
        {
            foreach (var vg in so.Children.Where(x => string.Equals(x.Name, "visgroup", StringComparison.InvariantCultureIgnoreCase)))
            {
                var v = new Visgroup
                {
                    Name = vg.Get("name", ""),
                    ID = vg.Get("visgroupid", -1),
                    Color = vg.GetColor("color"),
                    Visible = true
                };
                LoadVisgroupsRecursive(vg, v);
                parent.Children.Add(v);
            }
        }

        #endregion

        public void Write(Stream stream, MapFile map, string styleHint)
        {
            var list = new List<SerialisedObject>();

            list.AddRange(map.AdditionalObjects);

            var visObj = new SerialisedObject("visgroups");
            SaveVisgroups(map.Visgroups, visObj);
            list.Add(visObj);

            SaveWorld(map, list);
            SaveCameras(map, list);

            _formatter.Serialize(stream, list);
        }

        #region Write

        private static string FormatVector3(Vector3 c)
        {
            return $"{FormatDecimal(c.X)} {FormatDecimal(c.Y)} {FormatDecimal(c.Z)}";
        }

        private static string FormatDecimal(float d)
        {
            return d.ToString("0.00####", CultureInfo.InvariantCulture);
        }

        private void SaveVisgroups(IEnumerable<Visgroup> visgroups, SerialisedObject parent)
        {
            foreach (var visgroup in visgroups)
            {
                var vgo = new SerialisedObject("visgroup");
                vgo.Set("visgroupid", visgroup.ID);
                vgo.SetColor("color", visgroup.Color);
                SaveVisgroups(visgroup.Children, vgo);
                parent.Children.Add(vgo);
            }
        }

        private void SaveWorld(MapFile map, List<SerialisedObject> list)
        {
            // call the avengers

            var id = 1;
            var idMap = map.Worldspawn.FindAll().ToDictionary(x => x, x => id++);

            // Get the world, groups, and non-entity solids
            var vmfWorld = new VmfWorld(map.Worldspawn);
            var worldObj = vmfWorld.ToSerialisedObject();
            SerialiseWorldspawnChildren(map.Worldspawn, worldObj, idMap, 0, map.Worldspawn.Children);
            list.Add(worldObj);

            // Entities are separate from the world
            var entities = map.Worldspawn.FindAll().OfType<Entity>().Where(x => x != map.Worldspawn).Select(x => SerialiseEntity(x, idMap)).ToList();
            list.AddRange(entities);
        }

        private void SerialiseWorldspawnChildren(Worldspawn worldspawn, SerialisedObject worldObj, Dictionary<MapObject, int> idMap, int groupId, List<MapObject> list)
        {
            foreach (var c in list)
            {
                var cid = idMap[c];
                switch (c)
                {
                    case Entity _:
                        // Ignore everything underneath an entity
                        break;
                    case Group g:
                        var sg = new VmfGroup(g, cid);
                        if (groupId != 0) sg.Editor.GroupID = groupId;
                        worldObj.Children.Add(sg.ToSerialisedObject());
                        SerialiseWorldspawnChildren(worldspawn, worldObj, idMap, cid, g.Children);
                        break;
                    case Solid s:
                        var ss = new VmfSolid(s, cid);
                        if (groupId != 0) ss.Editor.GroupID = groupId;
                        worldObj.Children.Add(ss.ToSerialisedObject());
                        break;
                }
            }
        }

        private SerialisedObject SerialiseEntity(MapObject obj, Dictionary<MapObject, int> idMap)
        {
            var self = VmfObject.Serialise(obj, idMap[obj]);
            if (self == null) return null;

            var so = self.ToSerialisedObject();

            foreach (var solid in obj.FindAll().OfType<Solid>())
            {
                var s = VmfObject.Serialise(solid, idMap[obj]);
                if (s != null) so.Children.Add(s.ToSerialisedObject());
            }

            return so;
        }

        private void SaveCameras(MapFile map, List<SerialisedObject> list)
        {
            var cams = map.Cameras;

            var so = new SerialisedObject("cameras");
            so.Set("activecamera", -1);

            for (var i = 0; i < cams.Count; i++)
            {
                var camera = cams[i];
                if (camera.IsActive) so.Set("activecamera", i);

                var vgo = new SerialisedObject("camera");
                vgo.Set("position", $"[{FormatVector3(camera.EyePosition)}]");
                vgo.Set("look", $"[{FormatVector3(camera.LookPosition)}]");
                so.Children.Add(vgo);
            }

            list.Add(so);
        }

        #endregion
    }
}

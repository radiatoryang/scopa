using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Valve;

namespace Sledge.Formats.Map.Formats.VmfObjects
{
    internal class VmfEditor
    {
        public Color Color { get; set; }
        public List<int> VisgroupIDs { get; set; }
        public int GroupID { get; set; }
        public int ParentID { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public VmfEditor(SerialisedObject obj)
        {
            if (obj == null) obj = new SerialisedObject("editor");

            Color = obj.GetColor("color");
            ParentID = GroupID = obj.Get("groupid", 0);
            Properties = new Dictionary<string, string>();
            VisgroupIDs = new List<int>();

            foreach (var kv in obj.Properties)
            {
                switch (kv.Key.ToLower())
                {
                    case "visgroupid":
                        if (int.TryParse(kv.Value, out var id)) VisgroupIDs.Add(id);
                        break;
                    case "color":
                    case "groupid":
                        break;
                    default:
                        Properties[kv.Key] = kv.Value;
                        break;
                }
            }
        }

        public VmfEditor(MapObject obj, int groupId, int parentId)
        {
            Color = obj.Color;
            VisgroupIDs = obj.Visgroups.ToList();
            GroupID = groupId;
            ParentID = parentId;
            Properties = new Dictionary<string, string>();
        }

        public void Apply(MapObject obj)
        {
            obj.Color = Color;
            obj.Visgroups.AddRange(VisgroupIDs);
        }

        public SerialisedObject ToSerialisedObject()
        {
            var so = new SerialisedObject("editor");
            so.SetColor("color", Color);
            if (GroupID > 0) so.Set("groupid", GroupID);
            foreach (var kv in Properties)
            {
                so.Set(kv.Key, kv.Value);
            }
            foreach (var id in VisgroupIDs.Distinct())
            {
                so.Properties.Add(new KeyValuePair<string, string>("visgroupid", Convert.ToString(id, CultureInfo.InvariantCulture)));
            }
            return so;
        }
    }
}
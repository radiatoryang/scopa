using System.Collections.Generic;
using System.Linq;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Valve;

namespace Sledge.Formats.Map.Formats.VmfObjects
{
    internal class VmfEntity : VmfObject
    {
        public List<VmfObject> Objects { get; set; }
        public string ClassName { get; set; }
        public int SpawnFlags { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        private static readonly string[] ExcludedKeys = { "id", "spawnflags", "classname" };

        public VmfEntity(SerialisedObject obj) : base(obj)
        {
            Objects = new List<VmfObject>();
            foreach (var so in obj.Children)
            {
                var o = Deserialise(so);
                if (o != null) Objects.Add(o);
            }

            Properties = new Dictionary<string, string>();
            foreach (var kv in obj.Properties)
            {
                if (kv.Key == null || ExcludedKeys.Contains(kv.Key.ToLower())) continue;
                Properties[kv.Key] = kv.Value;
            }
            ClassName = obj.Get("classname", "");
            SpawnFlags = obj.Get("spawnflags", 0);
        }

        public VmfEntity(Entity ent, int id) : base(ent, id)
        {
            Objects = new List<VmfObject>();
            ClassName = ent.ClassName;
            SpawnFlags = ent.SpawnFlags;
            Properties = new Dictionary<string, string>(ent.Properties);
        }

        public override IEnumerable<VmfObject> Flatten()
        {
            return Objects.SelectMany(x => x.Flatten()).Union(new[] { this });
        }

        public override MapObject ToMapObject()
        {
            var ent = new Entity
            {
                ClassName = ClassName,
                SpawnFlags = SpawnFlags,
                Properties = new Dictionary<string, string>(Properties)
            };

            Editor.Apply(ent);

            return ent;
        }

        protected virtual string SerialisedObjectName => "entity";

        public override SerialisedObject ToSerialisedObject()
        {
            var so = new SerialisedObject(SerialisedObjectName);
            so.Set("id", ID);
            so.Set("classname", ClassName);
            if (SpawnFlags > 0) so.Set("spawnflags", SpawnFlags);
            foreach (var prop in Properties)
            {
                so.Properties.Add(new KeyValuePair<string, string>(prop.Key, prop.Value));
            }

            so.Children.Add(Editor.ToSerialisedObject());

            return so;
        }
    }
}
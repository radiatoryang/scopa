using System;
using System.Collections.Generic;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Valve;

namespace Sledge.Formats.Map.Formats.VmfObjects
{
    internal class VmfWorld : VmfEntity
    {
        public VmfWorld(SerialisedObject obj) : base(obj)
        {
            ID = -1;
        }

        public VmfWorld(Worldspawn root) : base(root, -1)
        {
        }

        public override MapObject ToMapObject()
        {
            throw new NotSupportedException();
        }

        protected override string SerialisedObjectName => "world";

        public override SerialisedObject ToSerialisedObject()
        {
            var so = new SerialisedObject(SerialisedObjectName);
            so.Set("id", 1);
            so.Set("classname", ClassName);
            if (SpawnFlags > 0) so.Set("spawnflags", SpawnFlags);
            foreach (var prop in Properties)
            {
                so.Properties.Add(new KeyValuePair<string, string>(prop.Key, prop.Value));
            }

            return so;
        }
    }
}
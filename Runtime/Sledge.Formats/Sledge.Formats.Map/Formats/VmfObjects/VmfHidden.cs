using System;
using System.Collections.Generic;
using System.Linq;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Valve;

namespace Sledge.Formats.Map.Formats.VmfObjects
{
    internal class VmfHidden : VmfObject
    {
        public List<VmfObject> Objects { get; set; }

        public VmfHidden(SerialisedObject obj) : base(obj)
        {
            Objects = new List<VmfObject>();
            foreach (var so in obj.Children)
            {
                var o = VmfObject.Deserialise(so);
                if (o != null) Objects.Add(o);
            }
        }

        public override IEnumerable<VmfObject> Flatten()
        {
            return Objects.SelectMany(x => x.Flatten());
        }

        public override MapObject ToMapObject()
        {
            throw new NotSupportedException();
        }

        public override SerialisedObject ToSerialisedObject()
        {
            throw new NotImplementedException();
        }
    }
}
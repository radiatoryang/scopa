using System.Collections.Generic;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Valve;

namespace Sledge.Formats.Map.Formats.VmfObjects
{
    internal class VmfGroup : VmfObject
    {
        public VmfGroup(SerialisedObject obj) : base(obj)
        {
        }

        public VmfGroup(Group grp, int id) : base(grp, id)
        {
        }

        public override IEnumerable<VmfObject> Flatten()
        {
            yield return this;
        }

        public override MapObject ToMapObject()
        {
            var grp = new Group();
            Editor.Apply(grp);
            return grp;
        }

        public override SerialisedObject ToSerialisedObject()
        {
            var so = new SerialisedObject("group");
            so.Set("id", ID);

            so.Children.Add(Editor.ToSerialisedObject());

            return so;
        }
    }
}
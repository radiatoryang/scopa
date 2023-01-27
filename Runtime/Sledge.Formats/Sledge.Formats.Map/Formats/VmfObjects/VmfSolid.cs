using System.Collections.Generic;
using System.Linq;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Valve;

namespace Sledge.Formats.Map.Formats.VmfObjects
{
    internal class VmfSolid : VmfObject
    {
        public List<VmfSide> Sides { get; set; }

        public VmfSolid(SerialisedObject obj) : base(obj)
        {
            Sides = new List<VmfSide>();
            foreach (var so in obj.Children.Where(x => x.Name == "side"))
            {
                Sides.Add(new VmfSide(so));
            }
        }

        public VmfSolid(Solid sol, int id) : base(sol, id)
        {
            Sides = sol.Faces.Select(x => new VmfSide(x, 0)).ToList();
        }

        public override IEnumerable<VmfObject> Flatten()
        {
            yield return this;
        }

        public override MapObject ToMapObject()
        {
            var sol = new Solid();
            Editor.Apply(sol);
            sol.Faces.AddRange(Sides.Select(x => x.ToFace()));
            sol.ComputeVertices();
            return sol;
        }

        public override SerialisedObject ToSerialisedObject()
        {
            var so = new SerialisedObject("solid");
            so.Set("id", ID);
            so.Children.AddRange(Sides.Select(x => x.ToSerialisedObject()));
            so.Children.Add(Editor.ToSerialisedObject());
            return so;
        }
    }
}
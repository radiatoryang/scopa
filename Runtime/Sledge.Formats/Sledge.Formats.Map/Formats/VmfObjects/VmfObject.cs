using System.Collections.Generic;
using System.Linq;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Valve;

namespace Sledge.Formats.Map.Formats.VmfObjects
{
    internal abstract class VmfObject
    {
        public int ID { get; set; }
        public VmfEditor Editor { get; set; }

        protected VmfObject(SerialisedObject obj)
        {
            ID = obj.Get("id", 0);
            Editor = new VmfEditor(obj.Children.FirstOrDefault(x => x.Name == "editor"));
        }

        protected VmfObject(MapObject obj, int id)
        {
            ID = id;
            Editor = new VmfEditor(obj, 0, 0);
        }

        public abstract IEnumerable<VmfObject> Flatten();
        public abstract MapObject ToMapObject();
        public abstract SerialisedObject ToSerialisedObject();

        public static VmfObject Deserialise(SerialisedObject obj)
        {
            switch (obj.Name)
            {
                case "world":
                    return new VmfWorld(obj);
                case "entity":
                    return new VmfEntity(obj);
                case "group":
                    return new VmfGroup(obj);
                case "solid":
                    return new VmfSolid(obj);
                case "hidden":
                    return new VmfHidden(obj);
                default:
                    return null;
            }
        }

        public static VmfObject Serialise(MapObject obj, int id)
        {
            switch (obj)
            {
                case Worldspawn r:
                    return new VmfWorld(r);
                case Entity e:
                    return new VmfEntity(e, id);
                case Group g:
                    return new VmfGroup(g, id);
                case Solid s:
                    return new VmfSolid(s, id);
                default:
                    return null;
            }
        }
    }
}

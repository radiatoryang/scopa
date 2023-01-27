using System.Collections.Generic;

namespace Sledge.Formats.Map.Objects
{
    public class Entity : MapObject
    {
        public string ClassName { get; set; }
        public int SpawnFlags { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public Entity()
        {
            Properties = new Dictionary<string, string>();
        }
    }
}
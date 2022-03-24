using System.Collections.Generic;
using System.Linq;

namespace Scopa.Formats.Map.Objects
{
    public class Entity : MapObject
    {
        public string ClassName { get; set; }
        public int SpawnFlags { get; set; }
        public Dictionary<string, string> Properties { get; set; }
        public int ID;

        public Entity()
        {
            Properties = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return (ClassName != null ? ClassName : "(empty entity)") 
            + "\n     " + string.Join( "\n     ", Properties.Select( kvp => $"{kvp.Key}: {kvp.Value}") )
            + "\n" + base.ToString();
        }
    }
}
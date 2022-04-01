using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

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

        /// <summary> parses an entity property as an int; empty or whitespace will return false </summary>
        public bool TryGetInt(string propertyKey, out int num) {
            num = 0;

            if (!Properties.ContainsKey(propertyKey))
                return false;

            if ( string.IsNullOrWhiteSpace(Properties[propertyKey]) )
                return false;
            
            return int.TryParse(Properties[propertyKey], System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out num);
        }

        /// <summary> parses an entity property as a float; empty or whitespace will return false </summary>
        public bool TryGetFloat(string propertyKey, out float num) {
            num = 0;

            if (!Properties.ContainsKey(propertyKey))
                return false;

            if ( string.IsNullOrWhiteSpace(Properties[propertyKey]) )
                return false;
            
            return float.TryParse(Properties[propertyKey], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out num);
        }

        /// <summary> parses an entity property as an unscaled Vector3 (swizzled for Unity in XZY format), if it exists as a valid Vector3; empty or whitespace will return false </summary>
        public bool TryGetVector3Unscaled(string propertyKey, out Vector3 vec) {
            vec = Vector3.zero;

            if (!Properties.ContainsKey(propertyKey))
                return false;

            if ( string.IsNullOrWhiteSpace(Properties[propertyKey]) )
                return false;
            
            var vecParts = Properties[propertyKey].Split(" ");
            if ( vecParts.Length < 3) {
                Debug.LogError($"couldn't parse {ClassName}#{ID} {propertyKey} as Vector3, needs three numbers separated by spaces");
                return false;
            }
            
            vec = Vector3Extensions.Parse(vecParts[0], vecParts[2], vecParts[1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
            return true;
        }

        public override string ToString()
        {
            return (ClassName != null ? ClassName : "(empty entity)") 
            + "\n     " + string.Join( "\n     ", Properties.Select( kvp => $"{kvp.Key}: {kvp.Value}") )
            + "\n" + base.ToString();
        }
    }
}
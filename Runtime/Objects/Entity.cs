using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Scopa;

namespace Scopa.Formats.Map.Objects
{
    [System.Serializable]
    public class Entity : MapObject
    {
        [ReadOnly] public string ClassName;
        [ReadOnly] public int SpawnFlags;
        [ReadOnly] public GenericDictionary<string, string> Properties = new GenericDictionary<string, string>();
        [ReadOnly] public int ID;
        [HideInInspector] public bool discard = false;

        void LogNoKey(string propertyKey) {
            Debug.LogWarning($"{ClassName}#{ID} doesn't have a property with key: {propertyKey} ");
        }

        void LogEmpty(string propertyKey) {
            Debug.LogWarning($"{ClassName}#{ID} has a key {propertyKey} but it's empty or whitespace");
        }

        /// <summary> convenience function to return spawn flags as a set of booleans; 24 is the default max limit for Quake 1 </summary>
        public bool[] GetSpawnFlags(int maxFlagCount = 24) {
            var ret = new bool[maxFlagCount];
            for( int i = 0; i < maxFlagCount; ++i ) {
                ret[i] = ((SpawnFlags >> i) & 1) == 1;
            }
            return ret;
        }

        /// <summary> parses an entity property as an string; empty or whitespace will return false </summary>
        public bool TryGetString(string propertyKey, out string data, bool verbose = false) {
            data = null;

            if (!Properties.ContainsKey(propertyKey)) {
                if (verbose)
                    LogNoKey(propertyKey);
                return false;
            }

            if ( string.IsNullOrWhiteSpace(Properties[propertyKey]) ) {
                if (verbose)
                    LogEmpty(propertyKey);
                return false;
            }
            
            data = Properties[propertyKey];
            return true;
        }

        /// <summary> parses an entity property as an int; empty or whitespace will return false </summary>
        public bool TryGetInt(string propertyKey, out int num, bool verbose = false) {
            num = 0;

            if (!Properties.ContainsKey(propertyKey)) {
                if (verbose)
                    LogNoKey(propertyKey);
                return false;
            }

            if ( string.IsNullOrWhiteSpace(Properties[propertyKey]) ) {
                if (verbose)
                    LogEmpty(propertyKey);
                return false;
            }
            
            return int.TryParse(Properties[propertyKey], System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out num);
        }

        /// <summary> parses an entity property as a float; empty or whitespace will return false </summary>
        public bool TryGetFloat(string propertyKey, out float num, bool verbose = false) {
            num = 0;

            if (!Properties.ContainsKey(propertyKey)) {
                if (verbose)
                    LogNoKey(propertyKey);
                return false;
            }

            if ( string.IsNullOrWhiteSpace(Properties[propertyKey]) ) {
                if (verbose)
                    LogEmpty(propertyKey);
                return false;
            }
            
            return float.TryParse(Properties[propertyKey], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out num);
        }

        /// <summary> parses an entity property as a Quake-style single number rotation; > 0 is negative yaw + 90 degrees, -1 is up, -2 is down; empty or whitespace will return false / Quaternion.identity </summary>
        public bool TryGetAngleSingle(string propertyKey, out Quaternion rotation, bool verbose = false) {
            rotation = Quaternion.identity;

            if ( TryGetFloat(propertyKey, out var angle, verbose) ) {
                var angleInt = Mathf.RoundToInt(angle);
                if ( angleInt == -1)
                    rotation = Quaternion.LookRotation(Vector3.up);
                else if ( angleInt == -2)
                    rotation = Quaternion.LookRotation(Vector3.down);
                else
                    rotation = Quaternion.Euler(0, -angleInt + 90, 0);
                return true;
            }

            return false;
        }

        /// <summary> parses an entity property as an unscaled Vector3 and applies axis corrections </summary>
        public bool TryGetAngles3D(string propertyKey, out Quaternion rotation, bool verbose = false) {
            rotation = Quaternion.identity;

            if ( TryGetVector3Unscaled(propertyKey, out var angles, verbose) ) {
                rotation = Quaternion.Euler(angles.x, -angles.z + 90, angles.y);
                return true;
            }

            return false;
        }

        /// <summary> parses an entity property as an unscaled Vector3 (swizzled for Unity in XZY format), if it exists as a valid Vector3; empty or whitespace will return false </summary>
        public bool TryGetVector3Unscaled(string propertyKey, out Vector3 vec, bool verbose = false) {
            vec = Vector3.zero;

            if (!Properties.ContainsKey(propertyKey)) {
                if (verbose)
                    LogNoKey(propertyKey);
                return false;
            }

            if ( string.IsNullOrWhiteSpace(Properties[propertyKey]) ) {
                if (verbose)
                    LogEmpty(propertyKey);
                return false;
            }
            
            var vecParts = Properties[propertyKey].Split(" ");
            if ( vecParts.Length < 3) {
                Debug.LogError($"couldn't parse {ClassName}#{ID} {propertyKey} as Vector3, needs three numbers separated by spaces");
                return false;
            }
            
            vec = VectorExtensions.Parse3(vecParts[0], vecParts[2], vecParts[1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary> parses an entity property as an SCALED Vector3 (+ swizzled for Unity in XZY format), if it exists as a valid Vector3; empty or whitespace will return false </summary>
        public bool TryGetVector3Scaled(string propertyKey, out Vector3 vec, float scalar = 0.03125f, bool verbose = false) {
            vec = Vector3.zero;

            if (TryGetVector3Unscaled(propertyKey, out vec, verbose) ) {
                vec *= scalar;
                return true;
            }
            
            return false;
        }

        /// <summary> parses an entity property as an unscaled Vector4 (and NOT swizzled), if it exists as a valid Vector4; empty or whitespace will return false </summary>
        public bool TryGetVector4Unscaled(string propertyKey, out Vector4 vec, bool verbose = false) {
            vec = Vector4.zero;

            if (!Properties.ContainsKey(propertyKey)) {
                if (verbose)
                    LogNoKey(propertyKey);
                return false;
            }

            if ( string.IsNullOrWhiteSpace(Properties[propertyKey]) ) {
                if (verbose)
                    LogEmpty(propertyKey);
                return false;
            }
            
            var vecParts = Properties[propertyKey].Split(" ");
            if ( vecParts.Length < 4) {
                Debug.LogError($"couldn't parse {ClassName}#{ID} {propertyKey} as Vector4, needs four numbers separated by spaces");
                return false;
            }
            
            vec = VectorExtensions.Parse4(vecParts[0], vecParts[1], vecParts[2], vecParts[3], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary> parses an entity property as an RGB Color (and try to detect if it's 0.0-1.0 or 0-255); empty or whitespace will return false and Color.black</summary>
        public bool TryGetColorRGB(string propertyKey, out Color color, bool verbose = false) {
            color = Color.black;

            if ( TryGetVector3Unscaled(propertyKey, out var vec, verbose) ) {
                // if we're parsing it as a vector, remember to unswizzle the Y and Z components
                if ( vec.x > 1 || vec.y > 1 || vec.z > 1 ) {
                    color = new Color(vec.x / 255, vec.z / 255, vec.y / 255);
                } else {
                    color = new Color(vec.x, vec.z, vec.y);
                }
                return true;
            }
            
            return false;
        }

        /// <summary> parses an entity property as an RGBA Color (and try to detect if it's 0.0-1.0 or 0-255); empty or whitespace will return false and Color.black</summary>
        public bool TryGetColorRGBA(string propertyKey, out Color color, bool verbose = false) {
            color = Color.black;

            if ( TryGetVector4Unscaled(propertyKey, out var vec, verbose) ) {
                if ( vec.x > 1 || vec.y > 1 || vec.z > 1 || vec.w > 1 ) {
                    color = new Color(vec.x / 255, vec.y / 255, vec.z / 255, vec.w / 255);
                } else {
                    color = new Color(vec.x, vec.y, vec.z, vec.w);
                }
                return true;
            }
            
            return false;
        }

        /// <summary> parses an entity property as an RGB Color (0-255) with a fourth number as light intensity scalar, common as the Half-Life 1 GoldSrc / Half-Life 2 Source light color format (e.g. "255 255 255 200"); empty or whitespace will return false and Color.black and intensity 0.0</summary>
        public bool TryGetColorLight(string propertyKey, out Color color, out float intensity, bool verbose = false) {
            color = Color.black;
            intensity = 0;

            if ( TryGetVector4Unscaled(propertyKey, out var vec, verbose) ) {
                color = new Color(vec.x / 255, vec.y / 255, vec.z / 255);
                intensity = vec.w / 255;
                return true;
            }
            
            return false;
        }

        public override string ToString()
        {
            return (ClassName != null ? ClassName : "(empty entity)") 
            + "\n     " + string.Join( "\n     ", Properties.Select( kvp => $"{kvp.Key}: {kvp.Value}") )
            + "\n" + base.ToString();
        }
    }
}
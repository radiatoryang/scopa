using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scopa.Formats.Map.Objects;

namespace Scopa {
    /// <summary> container to hold entity data; call GetComponent<ScopaEntity>().TryGet*() to poll for data </summary>
    public class ScopaEntity : MonoBehaviour
    {
        public Entity entityData;

        /// <summary> the entity type, e.g. func_wall, info_player_start, worldspawn </summary>
        public string className => entityData.ClassName;

        /// <summary> raw bitmask of the true/false flags set for each entity; you may prefer to use GetSpawnFlags() instead </summary>
        public int spawnFlags => entityData.SpawnFlags;

        /// <summary> convenience function to return spawn flags as a set of booleans; 24 is the default max limit for Quake 1 </summary>
        public bool[] GetSpawnFlags(int maxFlagCount = 24) => entityData.GetSpawnFlags(maxFlagCount);

        /// <summary> the entity number </summary>
        public int id => entityData.ID;

        /// <summary> parses property as an string, essentially the raw data; empty or whitespace will return false </summary>
        public bool TryGetString(string propertyKey, out string text) => entityData.TryGetString(propertyKey, out text);

        /// <summary> parses property as an int; empty or whitespace will return false </summary>
        public bool TryGetInt(string propertyKey, out int num) => entityData.TryGetInt(propertyKey, out num);

        /// <summary> parses property as a float; empty or whitespace will return false </summary>
        public bool TryGetFloat(string propertyKey, out float num) => entityData.TryGetFloat(propertyKey, out num);

        /// <summary> parses an entity property as a Quake-style single number rotation; > 0 is negative yaw + 90 degrees, -1 is up, -2 is down; empty or whitespace will return false / Quaternion.identity </summary>
        public bool TryGetAngleSingle(string propertyKey, out Quaternion rotation, bool verbose = false) => entityData.TryGetAngleSingle(propertyKey, out rotation, verbose);

        /// <summary> parses property as an unscaled Vector3 (swizzled for Unity in XZY format), if it exists as a valid Vector3; empty or whitespace will return false </summary>
        public bool TryGetVector3Unscaled(string propertyKey, out Vector3 vec) => entityData.TryGetVector3Unscaled(propertyKey, out vec);

        /// <summary> parses property as an SCALED Vector3 (+ swizzled for Unity in XZY format) at a default scale of 0.03125 (32 map units = 1 Unity meter), if it exists as a valid Vector3; empty or whitespace will return false </summary>
        public bool TryGetVector3Scaled(string propertyKey, out Vector3 vec, float scalar = 0.03125f) => entityData.TryGetVector3Scaled(propertyKey, out vec, scalar);

        /// <summary> parses property as an unscaled Vector4 (and NOT swizzled), if it exists as a valid Vector4; empty or whitespace will return false </summary>
        public bool TryGetVector4Unscaled(string propertyKey, out Vector4 vec) => entityData.TryGetVector4Unscaled(propertyKey, out vec);

        /// <summary> parses property as an RGB Color (and try to detect if it's 0.0-1.0 or 0-255); empty or whitespace will return false and Color.black</summary>
        public bool TryGetColorRGB(string propertyKey, out Color color) => entityData.TryGetColorRGB(propertyKey, out color);

        /// <summary> parses property as an RGBA Color (and try to detect if it's 0.0-1.0 or 0-255); empty or whitespace will return false and Color.black</summary>
        public bool TryGetColorRGBA(string propertyKey, out Color color) => entityData.TryGetColorRGBA(propertyKey, out color);

        /// <summary> parses property as an RGB Color (0-255) with a fourth number as light intensity scalar (255 = 1.0f), common as the Half-Life 1 GoldSrc / Half-Life 2 Source light color format (e.g. "255 255 255 200"); empty or whitespace will return false and Color.black and intensity 0.0</summary>
        public bool TryGetColorLight(string propertyKey, out Color color, out float intensity) => entityData.TryGetColorLight(propertyKey, out color, out intensity);
        
        /// <summary> returns a string of all entity data, including all properties and keyvalue pairs</summary>
        public override string ToString() => entityData.ToString();
    }

}

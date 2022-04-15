using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scopa {
    /// <summary> ScriptableObject to use for configuring how Scopa generates WAD files </summary>
    [CreateAssetMenu(fileName = "New ScopaWadCreator", menuName = "Scopa/WAD Creator", order = 1)]
    public class ScopaWadCreatorAsset : ScriptableObject {
        public ScopaWadCreator config = new ScopaWadCreator();
    }

    [System.Serializable]
    public class ScopaWadCreator {

        [HideInInspector] public string lastSavePath;

        [Tooltip("(default: WAD3) what kind of WAD file to generate? only Half-Life WAD3 is supported for now")]
        public WadFormat format = WadFormat.WAD3;

        [Tooltip("(default: Quarter) how much smaller should each WAD texture be? e.g. if your Unity textures are 2048x2048, we strongly recommend going with Quarter Res or smaller, to keep the WAD size manageable")]
        public WadResolution resolution = WadResolution.Quarter;

        [Tooltip("for each material, generate a WAD format texture with: (1) name based on material name (all lowercase, up to 16 characters), (2) image (mainTexture * mainColor) palettized to 256 colors")]
        public Material[] materials;

        public enum WadFormat {
            WAD3
        }

        public enum WadResolution {
            Full = 1,
            Half = 2,
            Quarter = 4,
            Eighth = 8,
            Sixteenth = 16
        }

    }

    
}


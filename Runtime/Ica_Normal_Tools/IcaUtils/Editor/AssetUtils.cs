using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ica.Utils.Editor
{
    public static class AssetUtils
    {
        public static GameObject FindAndInstantiateAsset(string name)
        {
            try
            {
                var paths = AssetDatabase.FindAssets(name);
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(paths[0]));
                var obj = (GameObject)Object.Instantiate(asset);
                return obj;

            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }
        
    }
}
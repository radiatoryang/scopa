using UnityEngine;

namespace Ica.Utils.Mesh
{
    public static class MeshUtils
    {
        public static void TransferBlendShapeValues(SkinnedMeshRenderer from, SkinnedMeshRenderer to)
        {
            for (int i = 0; i < from.sharedMesh.blendShapeCount; i++)
            {
                to.SetBlendShapeWeight(i, from.GetBlendShapeWeight(i));
            }
        }
    }
}
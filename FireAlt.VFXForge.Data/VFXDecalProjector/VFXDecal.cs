using UnityEngine;
using UnityEngine.VFX;

namespace FireAlt.VFXForge.Data
{
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct VFXDecal
    {
        public Vector3 Size;
        public Vector4 UvAtlas;
        public Vector3 Pivot;
        public float Opacity;
        public float DrawDistance;
        public float StartFade;
        public Vector2 AngleFade;
        public float NormalBlend;
    }
}
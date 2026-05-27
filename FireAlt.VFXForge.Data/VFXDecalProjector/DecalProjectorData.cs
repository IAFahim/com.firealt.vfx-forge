using FireAlt.Core.Rendering;
using Unity.Entities;
using Unity.Mathematics;

namespace FireAlt.VFXForge.Data
{
    public struct DecalProjectorData : IComponentData
    {
        public SpriteProperties SpriteProperties;
        public float NormalBlend;
        public float ProjectionDepth;
        public float ProjectionDepthPivot;
        public float Opacity;
        public float DrawDistance;
        public float StartFade;
        public float2 AngleFade;
    }

    public struct DecalProjectorVFX : IComponentData
    {
        public VFXKey Key;
        public TrackedEntity TrackedEntity;
    }
}
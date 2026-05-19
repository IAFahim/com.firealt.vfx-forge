using Unity.Entities;

namespace FireAlt.VFXForge
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(UpdateVFXSystemGroup))]
    [UpdateBefore(typeof(VFXTransformSystem))]
    public partial class BeforeVFXTransformSystemGroup : ComponentSystemGroup { }
}
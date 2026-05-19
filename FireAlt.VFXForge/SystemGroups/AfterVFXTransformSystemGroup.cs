using Unity.Entities;

namespace FireAlt.VFXForge
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(UpdateVFXSystemGroup))]
    [UpdateAfter(typeof(VFXTransformSystem))]
    public partial class AfterVFXTransformSystemGroup : ComponentSystemGroup { }
}
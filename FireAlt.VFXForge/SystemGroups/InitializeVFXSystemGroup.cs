using KrasCore;
using Unity.Entities;

namespace FireAlt.VFXForge
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(LateInitializationSystemGroup))]
    public partial class InitializeVFXSystemGroup : ComponentSystemGroup { }
}
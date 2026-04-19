using Unity.Entities;

namespace FireAlt.VFXForge
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
    public partial class UpdateVFXSystemGroup : ComponentSystemGroup { }
}
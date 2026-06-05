using FireAlt.VFXForge.Data;
using Unity.Entities;

namespace FireAlt.VFXForge
{
    public interface IVFXGraphEntry
    {
        UnityObjectRef<HybridVisualEffect> HybridVisualEffect { get; }
        VFXKey VFXKey { get; }
        ulong DataStableTypeHash { get; }
        ulong ArrayDataStableTypeHash { get; } 
        int DataSizeInBytes { get; }
        int ArrayDataSizeInBytes { get; } 
        int RequestsCount { get; }
        int ArrayRequestsCount { get; }
        bool HasPendingRequests { get; }
    }
}
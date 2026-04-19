using System;
using FireAlt.VFXForge.Data;
using Unity.Entities;

namespace FireAlt.VFXForge
{
    public struct AliveVFX : IEquatable<AliveVFX>
    {
        public VFXKey Key;
        public UnityObjectRef<HybridVisualEffect> HybridVisualEffect;
        public float TimeoutDuration;
        public float InactivityTimeRemaining;

        public void SetTimeoutDuration(float duration)
        {
            TimeoutDuration = duration;
            InactivityTimeRemaining = duration;
        }
        
        public bool Equals(AliveVFX other)
        {
            return Key.Equals(other.Key);
        }
        
        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }
}
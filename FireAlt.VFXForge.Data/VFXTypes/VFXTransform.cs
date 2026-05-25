using KrasCore;
using UnityEngine;
using UnityEngine.VFX;

namespace FireAlt.VFXForge.Data
{
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct VFXTransform
    {
        /// <summary>
        /// 0 -> IsActive;
        /// 1 -> IsEntityAlive;
        /// 2 -> IsInTrackDuration;
        /// 3 -> IsEntityEnabled;
        /// 31 -> DidTransformSystemRun;
        /// </summary>
        public uint State;

        public float StartTrackingTime;
        public float TrackingDuration;
        
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
        
        public bool IsAlive() => BitOperations.Get32(0, State);
        public void Kill() => SetAlive(false);
        
        internal bool DidTransformSystemRun() => BitOperations.Get32(31, State);
        
        internal void SetAlive(bool isActive)
        {
            BitOperations.Set32(0, ref State, isActive);
        }
        
        internal void SetEntityAlive(bool isEntityAlive)
        {
            BitOperations.Set32(1, ref State, isEntityAlive);
        }
        
        internal void SetInTrackingDuration(bool isInTrackingDuration)
        {
            BitOperations.Set32(2, ref State, isInTrackingDuration);
        }
        
        internal void SetEntityEnabled(bool isEntityEnabled)
        {
            BitOperations.Set32(3, ref State, isEntityEnabled);
        }
        
        internal void SetDidTransformSystemRun()
        {
            BitOperations.Set32(31, ref State, true);
        }
    }
}
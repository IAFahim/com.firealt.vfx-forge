using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace FireAlt.VFXForge.Data
{
    public class VFXDecalDefinition : ScriptableObject
    {
        public VisualEffectAsset visualEffectAsset;
        public int capacity = 100;
        public float timeoutDuration = 30f;
        
        [VFXDataTypeDropdown(VFXDataTypeBakerKind.Data)]
        public ulong vfxDataType;
        [VFXDataTypeDropdown(VFXDataTypeBakerKind.ArrayData)]
        public ulong vfxArrayDataType;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            timeoutDuration = math.max(timeoutDuration, 0);
            capacity = math.max(capacity, 1);
        }
#endif

        public VFXDefinition CreateDefinition(ushort newId)
        {
            var inst = CreateInstance<VFXDefinition>();
            inst.key = newId;
            inst.timeoutDuration = timeoutDuration;
            inst.capacity = capacity;
            inst.vfxDataType = vfxDataType;
            inst.vfxArrayDataType = vfxArrayDataType;
            inst.visualEffectAsset = visualEffectAsset;
            inst.vfxType = VFXType.Persistent;
            return inst;
        }
    }
}
using System;
using ArtificeToolkit.Attributes;
using BovineLabs.Core.ObjectManagement;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace FireAlt.VFXForge.Data
{
    [AutoRef("VFXSettings", "vfxDefinitions", nameof(VFXDefinition), "VFX")]
    public class VFXDefinition : ScriptableObject, IUID, ICloneable
    {
        [SerializeField, ReadOnly]
        private ushort key;
        
        int IUID.ID
        {
            get => key;
            set
            {
                if (value is < 0 or > ushort.MaxValue)
                {
                    Debug.LogError("Ran out of keys");
                    return;
                }

                key = (ushort)value;
            }
        }
        
        public static implicit operator VFXKey(VFXDefinition definition)
        {
            return definition == null ? 0 : definition.key;
        }
        
        public VisualEffectAsset visualEffectAsset;
        [EnableIf(nameof(IsPersistent), true)]
        public int capacity = 100;
        public float timeoutDuration = 30f;
        
        [EnumToggle]
        public VFXType vfxType;
        [VFXDataTypeDropdown(VFXDataTypeBakerKind.Data)]
        public ulong vfxDataType;
        [VFXDataTypeDropdown(VFXDataTypeBakerKind.ArrayData)]
        public ulong vfxArrayDataType;
        
        public bool IsPersistent => vfxType == VFXType.Persistent;
        public int DataGpuSize => DataTypeInfo.GpuSize;
        public int ArrayDataGpuSize => ArrayDataTypeInfo.GpuSize;
        
        public VFXTypeRegistry.TypeInfo DataTypeInfo => VFXTypeRegistry.GetTypeInfo(vfxDataType);
        public VFXTypeRegistry.TypeInfo ArrayDataTypeInfo => VFXTypeRegistry.GetTypeInfo(vfxArrayDataType);
        
#if UNITY_EDITOR
        public static event Action OnVFXDefinitionChanged = delegate { };
        
        private void OnValidate()
        {
            timeoutDuration = math.max(timeoutDuration, 0);
            capacity = math.max(capacity, 1);
            OnVFXDefinitionChanged.Invoke();
        }
#endif

        public VFXDefinition Clone(ushort newId)
        {
            var inst = CreateInstance<VFXDefinition>();
            inst.key = newId;
            inst.timeoutDuration = timeoutDuration;
            inst.capacity = capacity;
            inst.vfxDataType = vfxDataType;
            inst.vfxArrayDataType = vfxArrayDataType;
            inst.visualEffectAsset = visualEffectAsset;
            inst.vfxType = vfxType;
            return inst;
        }
        object ICloneable.Clone() => Clone(key);
    }
}

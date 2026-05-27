using System;
using FireAlt.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace FireAlt.VFXForge.Data
{
    [Serializable]
    public class VFXDataTypeBakerWrapper : VFXTypeBakerWrapper<IVFXDataTypeBaker>
    {
        [SerializeField, VFXBakerTypeDropdown(VFXDataTypeBakerKind.Data)]
        private string _selectedBakerTypeName;

        protected override VFXDataTypeBakerKind BakerKind => VFXDataTypeBakerKind.Data;
        protected override string SelectedBakerTypeName
        {
            get => _selectedBakerTypeName;
            set => _selectedBakerTypeName = value;
        }

        public unsafe bool TryGetTempDataRaw(out byte* ptr)
        {
            if (Baker != null)
            {
                var data = Baker.BakeBoxed();
                var tempArray = MemoryUtils.StructureToNativeByteArray(data, Allocator.Temp);
                
                ptr = (byte*)tempArray.GetUnsafePtr();
                return true;
            }
            ptr = null;
            return false;
        }
    }
}
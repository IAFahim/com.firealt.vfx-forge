using System;
using Unity.Collections;
using UnityEngine;

namespace FireAlt.VFXForge.Data
{
    [Serializable]
    public class VFXArrayDataTypeBakerWrapper : VFXTypeBakerWrapper<IVFXArrayDataTypeBaker>
    {
        [SerializeField, VFXBakerTypeDropdown(VFXDataTypeBakerKind.ArrayData)]
        private string _selectedBakerTypeName;

        protected override VFXDataTypeBakerKind BakerKind => VFXDataTypeBakerKind.ArrayData;
        protected override string SelectedBakerTypeName
        {
            get => _selectedBakerTypeName;
            set => _selectedBakerTypeName = value;
        }
        
        public bool TryGetTempBytesDataRaw(out NativeArray<byte> byteArray)
        {
            if (Baker != null)
            {
                byteArray = Baker.BakeBytes();
                if (byteArray.IsCreated)
                {
                    return true;
                }
            }
            byteArray = default;
            return false;
        }
    }
}
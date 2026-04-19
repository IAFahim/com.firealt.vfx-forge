using UnityEngine;

namespace FireAlt.VFXForge.Data
{
    public enum VFXDataTypeBakerKind
    {
        Data,
        ArrayData,
    }

    public class VFXDataTypeDropdownAttribute : PropertyAttribute
    {
        public readonly VFXDataTypeBakerKind BakerKind;

        public VFXDataTypeDropdownAttribute(VFXDataTypeBakerKind bakerKind)
        {
            BakerKind = bakerKind;
        }
    }
}

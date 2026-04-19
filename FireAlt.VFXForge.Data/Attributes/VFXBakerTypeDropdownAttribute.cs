using UnityEngine;

namespace FireAlt.VFXForge.Data
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class VFXBakerTypeDropdownAttribute : PropertyAttribute
    {
        public readonly VFXDataTypeBakerKind BakerKind;

        public VFXBakerTypeDropdownAttribute(VFXDataTypeBakerKind bakerKind)
        {
            BakerKind = bakerKind;
        }
    }
}

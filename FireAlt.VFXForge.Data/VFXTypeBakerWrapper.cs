using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireAlt.VFXForge.Data
{
    [Serializable]
    public abstract class VFXTypeBakerWrapper<TBakerInterface>
        where TBakerInterface : class
    {
        [SerializeField]
        private string _vfxDataTypeName;

        [SerializeReference]
        private TBakerInterface _baker;

        protected TBakerInterface Baker => _baker;
        protected abstract VFXDataTypeBakerKind BakerKind { get; }
        protected abstract string SelectedBakerTypeName { get; set; }

        public void SetBaker(Type vfxDataType)
        {
            _vfxDataTypeName = VFXTypeNameResolver.ToStoredTypeName(vfxDataType);

            if (vfxDataType == null || !TryGetAvailableBakerTypes(vfxDataType, out var availableBakerTypes))
            {
                _baker = null;
                SelectedBakerTypeName = null;
                return;
            }

            var selectedBakerType = ResolveSelectedBakerType(availableBakerTypes);
            if (selectedBakerType == null)
            {
                _baker = null;
                SelectedBakerTypeName = null;
                return;
            }

            SelectedBakerTypeName = VFXTypeNameResolver.ToStoredTypeName(selectedBakerType);
            _baker = (TBakerInterface)VFXBakerInstanceFactory.EnsureBakerInstance(_baker, selectedBakerType, SelectedBakerTypeName);
        }

        private bool TryGetAvailableBakerTypes(Type vfxDataType, out List<Type> availableBakerTypes)
        {
            return VFXTypeCache.TryGetBakerTypes(vfxDataType, BakerKind, out availableBakerTypes)
                && availableBakerTypes.Count > 0;
        }

        private Type ResolveSelectedBakerType(List<Type> availableBakerTypes)
        {
            var selectedType = string.IsNullOrEmpty(SelectedBakerTypeName)
                ? null
                : VFXTypeNameResolver.ResolveType(SelectedBakerTypeName);

            if (selectedType != null && availableBakerTypes.Contains(selectedType))
            {
                return selectedType;
            }

            return availableBakerTypes.Count > 0 ? availableBakerTypes[0] : null;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class VFXTypeBakerFieldAttribute : PropertyAttribute
    {
        public readonly string OnChangedMethod;

        public VFXTypeBakerFieldAttribute(string onChangedMethod = "")
        {
            OnChangedMethod = onChangedMethod;
        }
    }
}

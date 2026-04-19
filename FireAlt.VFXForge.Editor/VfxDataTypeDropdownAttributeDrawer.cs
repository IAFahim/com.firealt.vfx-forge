using System.Collections.Generic;
using BovineLabs.Core.Editor.SearchWindow;
using KrasCore.Editor.UI;
using FireAlt.VFXForge.Data;
using UnityEditor;
using UnityEngine.UIElements;

namespace FireAlt.VFXForge.Editor
{
    [CustomPropertyDrawer(typeof(VFXDataTypeDropdownAttribute))]
    public class VfxDataTypeDropdownAttributeDrawer : PropertyDrawer
    {
        private static readonly Dictionary<VFXDataTypeBakerKind, List<SearchView.Item>> ItemsByBakerKind = new();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var dropdownAttribute = (VFXDataTypeDropdownAttribute)attribute;

            if (!ItemsByBakerKind.TryGetValue(dropdownAttribute.BakerKind, out var items))
            {
                items = ItemsByBakerKind[dropdownAttribute.BakerKind] = GenerateItems(dropdownAttribute.BakerKind);
            }

            var searchElement = new SearchElement(items, string.Empty, property.displayName);
            searchElement.OnSelection += item =>
            {
                var stableTypeHash = (ulong)item.Data!;
                property.longValue = (long)stableTypeHash;
                property.serializedObject.ApplyModifiedProperties();
            };

            var searchButton = searchElement.Q<Button>();
            searchElement.SetText = item => HashToName((ulong)item.Data, searchButton.worldBound.width);

            searchElement.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                searchElement.Text = HashToName((ulong)property.longValue, searchButton.worldBound.width);
            });

            return searchElement;
        }

        private static string HashToName(ulong stableTypeHash, float width)
        {
            var type = VFXTypeRegistry.GetType(stableTypeHash);
            var name = type == null ? "None" : type.ToString();
            return HybridVFXDrawerUtils.TrimNameToWidth(name, width);
        }
        
        protected List<SearchView.Item> GenerateItems(VFXDataTypeBakerKind bakerKind)
        {
            var componentTypes = new List<SearchView.Item> { new() { Path = "None", Data = 0UL } };
            var vfxTypes = bakerKind == VFXDataTypeBakerKind.Data
                ? VFXTypeCache.DataTypesList
                : VFXTypeCache.ArrayTypesList;

            foreach (var e in vfxTypes)
            {
                var stableTypeHash = VFXTypeRegistry.GetStableTypeHash(e);

                componentTypes.Add(new SearchView.Item { Path = VFXTypeCache.TypeNamesDictionary[e], Data = stableTypeHash });
            }

            return componentTypes;
        }
    }
}

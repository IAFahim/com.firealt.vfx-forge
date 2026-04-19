using UnityEditor;

namespace FireAlt.VFXForge.Editor
{
    internal static class HybridVFXDrawerUtils
    {
        private const float CharacterWidthEstimate = 7.5f;

        public static SerializedProperty FindRelativeProperty(SerializedProperty property, string relativePropertyPath)
        {
            if (property == null || string.IsNullOrEmpty(relativePropertyPath))
            {
                return null;
            }

            var rootProperty = property.serializedObject.FindProperty(property.propertyPath);
            return rootProperty?.FindPropertyRelative(relativePropertyPath);
        }

        public static SerializedProperty FindSiblingProperty(SerializedProperty property, string siblingPropertyName)
        {
            if (property == null || string.IsNullOrEmpty(siblingPropertyName))
            {
                return null;
            }

            var parentPath = GetParentPath(property.propertyPath);
            if (string.IsNullOrEmpty(parentPath))
            {
                return null;
            }

            return property.serializedObject.FindProperty($"{parentPath}.{siblingPropertyName}");
        }

        public static string TrimNameToWidth(string name, float width)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name ?? string.Empty;
            }

            var maxLength = width / CharacterWidthEstimate;
            if (name.Length < maxLength)
            {
                return name;
            }

            var parts = name.Split('.');
            var trimmedName = parts[^1];
            var length = trimmedName.Length;

            for (var i = parts.Length - 2; i >= 0; i--)
            {
                length += parts[i].Length + 1;
                if (length > maxLength)
                {
                    return trimmedName;
                }

                trimmedName = parts[i] + "." + trimmedName;
            }

            return trimmedName;
        }

        private static string GetParentPath(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            var lastDotIndex = propertyPath.LastIndexOf('.');
            if (lastDotIndex <= 0)
            {
                return null;
            }

            return propertyPath.Substring(0, lastDotIndex);
        }
    }
}

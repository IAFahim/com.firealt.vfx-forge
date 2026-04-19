using System;

namespace FireAlt.VFXForge.Data
{
    public static class VFXTypeNameResolver
    {
        public static string ToStoredTypeName(Type type)
        {
            return type?.FullName;
        }

        public static Type ResolveType(string storedTypeName)
        {
            if (string.IsNullOrEmpty(storedTypeName))
            {
                return null;
            }

            var type = Type.GetType(storedTypeName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(storedTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}

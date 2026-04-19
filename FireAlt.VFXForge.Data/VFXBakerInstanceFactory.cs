using System;

namespace FireAlt.VFXForge.Data
{
    public static class VFXBakerInstanceFactory
    {
        public static object EnsureBakerInstance(object currentBaker, Type selectedBakerType, string currentBakerFullName)
        {
            if (selectedBakerType == null)
            {
                return null;
            }

            if (currentBaker != null && currentBaker.GetType() == selectedBakerType && selectedBakerType.FullName == currentBakerFullName)
            {
                return currentBaker;
            }

            return Activator.CreateInstance(selectedBakerType);
        }
    }
}

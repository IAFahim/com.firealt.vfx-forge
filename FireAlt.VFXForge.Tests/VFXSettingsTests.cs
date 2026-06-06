using FireAlt.VFXForge.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace FireAlt.VFXForge.Tests
{
    public class VFXSettingsTests
    {
        private const string DEFAULT_DECAL_VFX_GUID_KEY = "FireAlt.VFXForge.DefaultDecalVFXGuid";
        private const string PACKAGE_DEFAULT_DECAL_VFX_PATH = "Packages/com.firealt.vfx-forge/Shaders/Decals/DecalDefinition.asset";

        private bool hadStoredGuid;
        private string storedGuid;

        [SetUp]
        public void SetUp()
        {
            hadStoredGuid = EditorPrefs.HasKey(DEFAULT_DECAL_VFX_GUID_KEY);
            storedGuid = EditorPrefs.GetString(DEFAULT_DECAL_VFX_GUID_KEY, string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadStoredGuid)
            {
                EditorPrefs.SetString(DEFAULT_DECAL_VFX_GUID_KEY, storedGuid);
            }
            else
            {
                EditorPrefs.DeleteKey(DEFAULT_DECAL_VFX_GUID_KEY);
            }
        }

        [Test]
        public void DefaultDecalVFX_WhenPreferenceIsMissing_UsesAndStoresPackageDefault()
        {
            EditorPrefs.DeleteKey(DEFAULT_DECAL_VFX_GUID_KEY);

            var definition = VFXSettings.DefaultDecalVFX;

            Assert.IsNotNull(definition);
            Assert.AreEqual(PACKAGE_DEFAULT_DECAL_VFX_PATH, AssetDatabase.GetAssetPath(definition));
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(PACKAGE_DEFAULT_DECAL_VFX_PATH),
                EditorPrefs.GetString(DEFAULT_DECAL_VFX_GUID_KEY));
        }

        [Test]
        public void DefaultDecalVFX_WhenStoredGuidIsStale_UsesAndStoresPackageDefault()
        {
            EditorPrefs.SetString(DEFAULT_DECAL_VFX_GUID_KEY, "00000000000000000000000000000000");

            var definition = VFXSettings.DefaultDecalVFX;

            Assert.IsNotNull(definition);
            Assert.AreEqual(PACKAGE_DEFAULT_DECAL_VFX_PATH, AssetDatabase.GetAssetPath(definition));
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(PACKAGE_DEFAULT_DECAL_VFX_PATH),
                EditorPrefs.GetString(DEFAULT_DECAL_VFX_GUID_KEY));
        }
    }
}

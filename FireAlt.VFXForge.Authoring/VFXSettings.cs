using BovineLabs.Core.Settings;
using FireAlt.VFXForge.Data;

namespace FireAlt.VFXForge.Authoring
{
    [SettingsGroup("HybridECS")]
    public class VFXSettings : SettingsSingleton<VFXSettings>
    {
        public override bool IncludeInBuild => false;

        public VFXDefinition defaultDecalVFX;
    }
}
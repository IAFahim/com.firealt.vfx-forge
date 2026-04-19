using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using BovineLabs.Core.Settings;
using FireAlt.VFXForge.Data;
using UnityEngine;

namespace FireAlt.VFXForge.Authoring
{
    [SettingsGroup("HybridECS")]
    public class VFXSettings : SettingsSingleton<VFXSettings>
    {
        public override bool IncludeInBuild => false;

        [SerializeField, ReadOnly]
        private List<VFXDefinition> vfxDefinitions = new();

        public VFXDefinition defaultDecalVFX;
    }
}
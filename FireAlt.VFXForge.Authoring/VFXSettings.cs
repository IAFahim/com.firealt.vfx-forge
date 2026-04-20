using System.Collections.Generic;
using BovineLabs.Core.Settings;
using FireAlt.VFXForge.Data;
using KrasCore;
using UnityEngine;

namespace FireAlt.VFXForge.Authoring
{
    [SettingsGroup("HybridECS")]
    public class VFXSettings : SettingsSingleton<VFXSettings>
    {
        public override bool IncludeInBuild => false;

        [SerializeField, InspectorReadOnly]
        private List<VFXDefinition> vfxDefinitions = new();

        public VFXDefinition defaultDecalVFX;
    }
}
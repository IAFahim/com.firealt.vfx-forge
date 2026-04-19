using System.IO;
using KrasCore.Editor;
using FireAlt.VFXForge.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace FireAlt.VFXForge.Editor
{
    public static class VFXDefinitionMenu
    {
        [MenuItem("FireAlt/Create VFX Definitions from VFX Assets", priority = -17)]
        public static void CreateDefinitionsFromAssets()
        {
            if (Selection.objects == null)
            {
                return;
            }
            
            foreach (var select in Selection.objects)
            {
                if (select is not VisualEffectAsset asset) continue;
                
                var go = new GameObject(select.name);
                var visualEffect = go.AddComponent<VisualEffect>();
                go.AddComponent<HybridVisualEffect>();
                visualEffect.visualEffectAsset = asset;
                
                var path = Path.Combine(AssetDatabaseUtils.GetFolderPath(AssetDatabase.GetAssetPath(asset)),
                    $"{select.name}Definition.asset");
                var definition = ScriptableObject.CreateInstance<VFXDefinition>();
                
                definition.visualEffectAsset = asset;
                
                AssetDatabase.CreateAsset(definition, path);
                AssetDatabase.ImportAsset(path);
            }
            
            AssetDatabase.SaveAssets();
        }
    }
}
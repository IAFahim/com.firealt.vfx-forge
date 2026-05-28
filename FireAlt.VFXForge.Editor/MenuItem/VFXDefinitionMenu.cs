using System.Collections.Generic;
using System.IO;
using FireAlt.Core.Editor;
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

            var paths = new List<(string, GameObject)>();
            foreach (var select in Selection.objects)
            {
                if (select is not VisualEffectAsset asset) continue;
                
                var go = new GameObject(select.name);
                go.AddComponent<VisualEffect>();
                go.AddComponent<HybridVisualEffect>();
                
                var path = Path.Combine(AssetDatabaseUtils.GetFolderPath(AssetDatabase.GetAssetPath(asset)),
                    $"{select.name}Definition.asset");
                var definition = ScriptableObject.CreateInstance<VFXDefinition>();
                
                definition.visualEffectAsset = asset;
                
                AssetDatabase.CreateAsset(definition, path);
                AssetDatabase.ImportAsset(path);
                paths.Add((path, go));
            }
            
            AssetDatabase.SaveAssets();

            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<VFXDefinition>(path.Item1);
                var component = path.Item2.GetComponent<HybridVisualEffect>();
                
                component.VFXDefinition = asset;
                EditorUtility.SetDirty(component);
            }
        }
        
        [MenuItem("FireAlt/Create VFX Decal Definitions from VFX Assets", priority = -17)]
        public static void CreateDecalDefinitionsFromAssets()
        {
            if (Selection.objects == null)
            {
                return;
            }

            foreach (var select in Selection.objects)
            {
                if (select is not VisualEffectAsset asset) continue;
                
                var path = Path.Combine(AssetDatabaseUtils.GetFolderPath(AssetDatabase.GetAssetPath(asset)),
                    $"{select.name}Definition.asset");
                var definition = ScriptableObject.CreateInstance<VFXDecalDefinition>();
                
                definition.visualEffectAsset = asset;
                
                AssetDatabase.CreateAsset(definition, path);
                AssetDatabase.ImportAsset(path);
            }
            
            AssetDatabase.SaveAssets();
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using FireAlt.Core.ObjectManagement;
using FireAlt.VFXForge.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.VFX;

namespace FireAlt.VFXForge.Tests
{
    internal sealed class VFXPlayModeTestFixture : IDisposable
    {
        private const string INSTANT_VFX_ASSET_PATH = "Packages/com.firealt.vfx-forge/Shaders/Templates/Instant(Single).vfx";
        private const string PERSISTENT_VFX_ASSET_PATH = "Packages/com.firealt.vfx-forge/Shaders/Templates/Persistent(Single).vfx";
        private const string PERSISTENT_ARRAY_VFX_ASSET_PATH = "Packages/com.firealt.vfx-forge/Shaders/Templates/Persistent(Array).vfx";
        private const string PERSISTENT_SINGLE_ARRAY_VFX_ASSET_PATH =
            "Packages/com.firealt.vfx-forge/Shaders/Templates/Persistent(Single+Array).vfx";

        private readonly List<GameObject> _gameObjects = new();
        private readonly List<VFXDefinition> _definitions = new();
        private readonly World _previousWorld;

        private VFXPlayModeTestFixture(string worldName)
        {
            _previousWorld = World.DefaultGameObjectInjectionWorld;
            World = World.DefaultGameObjectInjectionWorld = new World(worldName, WorldFlags.Game);
            SyncVFXSystem = World.CreateSystem<SyncVFXSystem>();
            VFXTransformSystem = World.CreateSystem<VFXTransformSystem>();
            World.CreateSystemManaged<InitializeVFXSystem>();
        }

        internal World World { get; }

        internal SystemHandle SyncVFXSystem { get; }

        internal SystemHandle VFXTransformSystem { get; }

        internal static IEnumerator Run(Action<VFXPlayModeTestFixture> test, string worldName = "VFX Forge Test World")
        {
            using (var fixture = new VFXPlayModeTestFixture(worldName))
            {
                test(fixture);
            }

            yield return null;
        }

        internal VFXDefinition CreateDefinition(
            int id,
            VFXType vfxType,
            int capacity = 100,
            float timeoutDuration = 5f,
            bool hasData = false,
            bool hasArrayData = false)
        {
            var definition = ScriptableObject.CreateInstance<VFXDefinition>();
            ((IUID)definition).ID = id;
#if UNITY_EDITOR
            definition.visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                GetVFXAssetPath(vfxType, hasData, hasArrayData));
#else
            throw new InvalidOperationException("VFX Forge playmode tests require the Unity Editor to load package VFX assets.");
#endif
            definition.capacity = capacity;
            definition.timeoutDuration = timeoutDuration;
            definition.vfxType = vfxType;
            definition.vfxDataType = hasData ? VFXTypeRegistry.GetStableTypeHash<VFXDecal>() : 0;
            definition.vfxArrayDataType = hasArrayData ? VFXTypeRegistry.GetStableTypeHash<VFXDecal>() : 0;
            _definitions.Add(definition);
            return definition;
        }

        internal HybridVisualEffect CreateAndRegisterVisualEffect(VFXDefinition definition, string name = "VFX Forge Test")
        {
            var gameObject = new GameObject(name);
            _gameObjects.Add(gameObject);

            gameObject.AddComponent<VisualEffect>();
            var hybridVisualEffect = gameObject.AddComponent<HybridVisualEffect>();
            hybridVisualEffect.VFXDefinition = definition;
            hybridVisualEffect.Init();
            return hybridVisualEffect;
        }

        internal GameObject CreateTrackedGameObject(string name = "VFX Forge Tracked GameObject")
        {
            var gameObject = new GameObject(name);
            _gameObjects.Add(gameObject);
            return gameObject;
        }

        internal Entity CreateTrackedEntity()
        {
            var entity = World.EntityManager.CreateEntity(typeof(LocalToWorld));
            World.EntityManager.SetComponentData(entity, new LocalToWorld { Value = float4x4.TRS(float3.zero, quaternion.identity, 1f) });
            return entity;
        }

        internal VFXSingleton GetSingleton()
        {
            var query = World.EntityManager.CreateEntityQuery(typeof(VFXSingleton));
            return query.GetSingleton<VFXSingleton>();
        }

        internal void UpdateSystems()
        {
            VFXTransformSystem.Update(World.Unmanaged);
            SyncVFXSystem.Update(World.Unmanaged);
            World.EntityManager.CompleteAllTrackedJobs();
        }

        internal void SpawnInstant(VFXDefinition definition)
        {
            var singleton = GetSingleton();
            ref var entry = ref singleton.GetInstant(definition);
            entry.Spawn();
        }

        internal TrackedEntity SpawnPersistent(VFXDefinition definition, Entity entityToTrack, float trackingDuration = 0f)
        {
            var singleton = GetSingleton();
            ref var entry = ref singleton.GetPersistent(definition);
            return entry.Spawn(entityToTrack, trackingDuration);
        }

        internal TrackedEntity SpawnPersistent(VFXDefinition definition, EntityId entityIdToTrack, float trackingDuration = 0f)
        {
            var singleton = GetSingleton();
            ref var entry = ref singleton.GetPersistent(definition);
            return entry.Spawn(entityIdToTrack, trackingDuration);
        }

        internal bool IsPersistentAlive(VFXDefinition definition, TrackedEntity trackedEntity)
        {
            var singleton = GetSingleton();
            ref var entry = ref singleton.GetPersistent(definition);
            return entry.IsAlive(trackedEntity);
        }

        internal bool TryKillPersistent(VFXDefinition definition, TrackedEntity trackedEntity)
        {
            var singleton = GetSingleton();
            ref var entry = ref singleton.GetPersistent(definition);
            return entry.TryKill(trackedEntity);
        }

        public void Dispose()
        {
            for (var i = _gameObjects.Count - 1; i >= 0; i--)
            {
                if (_gameObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_gameObjects[i]);
                }
            }

            for (var i = _definitions.Count - 1; i >= 0; i--)
            {
                if (_definitions[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_definitions[i]);
                }
            }

            if (World.IsCreated)
            {
                World.EntityManager.CompleteAllTrackedJobs();
                World.DestroyAllSystemsAndLogException(out _);
                World.Dispose();
            }

            World.DefaultGameObjectInjectionWorld = _previousWorld;
        }

        private static string GetVFXAssetPath(VFXType vfxType, bool hasData, bool hasArrayData)
        {
            if (vfxType != VFXType.Persistent)
            {
                return INSTANT_VFX_ASSET_PATH;
            }

            if (hasData && hasArrayData)
            {
                return PERSISTENT_SINGLE_ARRAY_VFX_ASSET_PATH;
            }

            return hasArrayData ? PERSISTENT_ARRAY_VFX_ASSET_PATH : PERSISTENT_VFX_ASSET_PATH;
        }
    }
}

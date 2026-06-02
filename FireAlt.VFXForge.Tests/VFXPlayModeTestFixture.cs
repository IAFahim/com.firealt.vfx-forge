using System;
using System.Collections;
using System.Collections.Generic;
using FireAlt.Core.ObjectManagement;
using FireAlt.VFXForge.Data;
using Unity.Entities;
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

        internal VFXDefinition CreateDefinition(int id, VFXType vfxType, int capacity = 100, float timeoutDuration = 5f)
        {
            var definition = ScriptableObject.CreateInstance<VFXDefinition>();
            ((IUID)definition).ID = id;
#if UNITY_EDITOR
            definition.visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                vfxType == VFXType.Persistent ? PERSISTENT_VFX_ASSET_PATH : INSTANT_VFX_ASSET_PATH);
#else
            throw new InvalidOperationException("VFX Forge playmode tests require the Unity Editor to load package VFX assets.");
#endif
            definition.capacity = capacity;
            definition.timeoutDuration = timeoutDuration;
            definition.vfxType = vfxType;
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
    }
}

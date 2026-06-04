using System.Collections;
using FireAlt.VFXForge.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireAlt.VFXForge.Tests
{
    public class PersistentVFXGameObjectPathTests
    {
        [UnityTest]
        public IEnumerator SpawnGameObject_WhenCapacityAvailable_BecomesAliveLikeEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(100, VFXType.Persistent, capacity: 4);
                fixture.CreateAndRegisterVisualEffect(definition);
                var gameObject = fixture.CreateTrackedGameObject();
                var entity = fixture.CreateTrackedEntity();

                var gameObjectTrackedEntity = fixture.SpawnPersistent(definition, gameObject.GetEntityId(), trackingDuration: 1.25f);
                var entityTrackedEntity = fixture.SpawnPersistent(definition, entity, trackingDuration: 1.25f);
                fixture.UpdateSystems();

                Assert.IsTrue(gameObjectTrackedEntity.IsValid);
                Assert.IsTrue(entityTrackedEntity.IsValid);
                Assert.IsTrue(fixture.IsPersistentAlive(definition, gameObjectTrackedEntity));
                Assert.IsTrue(fixture.IsPersistentAlive(definition, entityTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnGameObjects_WhenMultipleTrackedUnderSameKey_AllRemainAlive()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(110, VFXType.Persistent, capacity: 3);
                fixture.CreateAndRegisterVisualEffect(definition);
                var first = fixture.CreateTrackedGameObject("First Tracked GameObject");
                var second = fixture.CreateTrackedGameObject("Second Tracked GameObject");

                var firstTrackedEntity = fixture.SpawnPersistent(definition, first.GetEntityId());
                var secondTrackedEntity = fixture.SpawnPersistent(definition, second.GetEntityId());
                fixture.UpdateSystems();

                Assert.IsTrue(fixture.IsPersistentAlive(definition, firstTrackedEntity));
                Assert.IsTrue(fixture.IsPersistentAlive(definition, secondTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnGameObjects_WhenMultipleKeys_AllRemainAlive()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var firstDefinition = fixture.CreateDefinition(120, VFXType.Persistent, capacity: 2);
                var secondDefinition = fixture.CreateDefinition(121, VFXType.Persistent, capacity: 2);
                fixture.CreateAndRegisterVisualEffect(firstDefinition, "First Key VFX");
                fixture.CreateAndRegisterVisualEffect(secondDefinition, "Second Key VFX");
                var first = fixture.CreateTrackedGameObject("First Key Tracked GameObject");
                var second = fixture.CreateTrackedGameObject("Second Key Tracked GameObject");

                var firstTrackedEntity = fixture.SpawnPersistent(firstDefinition, first.GetEntityId());
                var secondTrackedEntity = fixture.SpawnPersistent(secondDefinition, second.GetEntityId());
                fixture.UpdateSystems();

                Assert.IsTrue(fixture.IsPersistentAlive(firstDefinition, firstTrackedEntity));
                Assert.IsTrue(fixture.IsPersistentAlive(secondDefinition, secondTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnGameObject_WhenCapacityExceeded_ReturnsInvalidLikeEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var gameObjectDefinition = fixture.CreateDefinition(130, VFXType.Persistent, capacity: 1);
                var entityDefinition = fixture.CreateDefinition(131, VFXType.Persistent, capacity: 1);
                fixture.CreateAndRegisterVisualEffect(gameObjectDefinition, "GameObject Capacity VFX");
                fixture.CreateAndRegisterVisualEffect(entityDefinition, "Entity Capacity VFX");
                var firstGameObject = fixture.CreateTrackedGameObject("First Capacity GameObject");
                var secondGameObject = fixture.CreateTrackedGameObject("Second Capacity GameObject");
                var firstEntity = fixture.CreateTrackedEntity();
                var secondEntity = fixture.CreateTrackedEntity();

                var firstGameObjectTrackedEntity = fixture.SpawnPersistent(gameObjectDefinition, firstGameObject.GetEntityId());
                var secondGameObjectTrackedEntity = fixture.SpawnPersistent(gameObjectDefinition, secondGameObject.GetEntityId());
                var firstEntityTrackedEntity = fixture.SpawnPersistent(entityDefinition, firstEntity);
                var secondEntityTrackedEntity = fixture.SpawnPersistent(entityDefinition, secondEntity);

                Assert.IsTrue(firstGameObjectTrackedEntity.IsValid);
                Assert.IsFalse(secondGameObjectTrackedEntity.IsValid);
                Assert.IsTrue(firstEntityTrackedEntity.IsValid);
                Assert.IsFalse(secondEntityTrackedEntity.IsValid);
            });
        }

        [UnityTest]
        public IEnumerator TryKillGameObjectPersistent_WhenResolved_KillsLikeEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(140, VFXType.Persistent, capacity: 4);
                fixture.CreateAndRegisterVisualEffect(definition);
                var gameObject = fixture.CreateTrackedGameObject();
                var entity = fixture.CreateTrackedEntity();

                var gameObjectTrackedEntity = fixture.SpawnPersistent(definition, gameObject.GetEntityId());
                var entityTrackedEntity = fixture.SpawnPersistent(definition, entity);
                fixture.UpdateSystems();

                Assert.IsTrue(fixture.TryKillPersistent(definition, gameObjectTrackedEntity));
                Assert.IsTrue(fixture.TryKillPersistent(definition, entityTrackedEntity));
                fixture.UpdateSystems();

                Assert.IsFalse(fixture.IsPersistentAlive(definition, gameObjectTrackedEntity));
                Assert.IsFalse(fixture.IsPersistentAlive(definition, entityTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator TryKillGameObjectPersistent_WhenDeferred_DoesNotBecomeAliveLikeEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(150, VFXType.Persistent, capacity: 4);
                fixture.CreateAndRegisterVisualEffect(definition);
                var gameObject = fixture.CreateTrackedGameObject();
                var entity = fixture.CreateTrackedEntity();

                var gameObjectTrackedEntity = fixture.SpawnPersistent(definition, gameObject.GetEntityId());
                var entityTrackedEntity = fixture.SpawnPersistent(definition, entity);

                Assert.IsTrue(fixture.TryKillPersistent(definition, gameObjectTrackedEntity));
                Assert.IsTrue(fixture.TryKillPersistent(definition, entityTrackedEntity));
                fixture.UpdateSystems();

                Assert.IsFalse(fixture.IsPersistentAlive(definition, gameObjectTrackedEntity));
                Assert.IsFalse(fixture.IsPersistentAlive(definition, entityTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnGameObject_WhenTrackedObjectDestroyedBeforeSync_DoesNotThrowAndDoesNotBecomeAliveLikeEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(160, VFXType.Persistent, capacity: 4);
                fixture.CreateAndRegisterVisualEffect(definition);
                var gameObject = fixture.CreateTrackedGameObject();
                var entity = fixture.CreateTrackedEntity();

                var gameObjectTrackedEntity = fixture.SpawnPersistent(definition, gameObject.GetEntityId());
                var entityTrackedEntity = fixture.SpawnPersistent(definition, entity);
                Object.DestroyImmediate(gameObject);
                fixture.World.EntityManager.DestroyEntity(entity);

                Assert.DoesNotThrow(fixture.UpdateSystems);
                Assert.IsFalse(fixture.IsPersistentAlive(definition, gameObjectTrackedEntity));
                Assert.IsFalse(fixture.IsPersistentAlive(definition, entityTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnGameObject_WithData_MatchesEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(170, VFXType.Persistent, capacity: 4, hasData: true);
                fixture.CreateAndRegisterVisualEffect(definition);
                var gameObject = fixture.CreateTrackedGameObject();
                var entity = fixture.CreateTrackedEntity();
                var gameObjectData = CreateDecal(1f);
                var entityData = CreateDecal(2f);

                var singleton = fixture.GetSingleton();
                ref var entry = ref singleton.GetPersistent(definition);
                var gameObjectTrackedEntity = entry.Spawn(gameObject.GetEntityId(), gameObjectData);
                var entityTrackedEntity = entry.Spawn(entity, entityData);
                fixture.UpdateSystems();

                AssertDataMatches(ref entry, gameObjectTrackedEntity, gameObjectData);
                AssertDataMatches(ref entry, entityTrackedEntity, entityData);
                Assert.IsTrue(fixture.IsPersistentAlive(definition, gameObjectTrackedEntity));
                Assert.IsTrue(fixture.IsPersistentAlive(definition, entityTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnGameObject_WithArrayData_MatchesEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(180, VFXType.Persistent, capacity: 4, hasArrayData: true);
                fixture.CreateAndRegisterVisualEffect(definition);
                var gameObject = fixture.CreateTrackedGameObject();
                var entity = fixture.CreateTrackedEntity();
                var gameObjectArrayData = CreateDecalArray(3f);
                var entityArrayData = CreateDecalArray(4f);

                var singleton = fixture.GetSingleton();
                ref var entry = ref singleton.GetPersistent(definition);
                var gameObjectTrackedEntity = entry.Spawn(gameObject.GetEntityId(), gameObjectArrayData);
                var entityTrackedEntity = entry.Spawn(entity, entityArrayData);
                fixture.UpdateSystems();

                AssertArrayDataMatches(ref entry, gameObjectTrackedEntity, gameObjectArrayData);
                AssertArrayDataMatches(ref entry, entityTrackedEntity, entityArrayData);
                Assert.IsTrue(fixture.IsPersistentAlive(definition, gameObjectTrackedEntity));
                Assert.IsTrue(fixture.IsPersistentAlive(definition, entityTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnGameObject_WithDataAndArrayData_MatchesEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(190, VFXType.Persistent, capacity: 4, hasData: true, hasArrayData: true);
                fixture.CreateAndRegisterVisualEffect(definition);
                var gameObject = fixture.CreateTrackedGameObject();
                var entity = fixture.CreateTrackedEntity();
                var gameObjectData = CreateDecal(5f);
                var entityData = CreateDecal(6f);
                var gameObjectArrayData = CreateDecalArray(7f);
                var entityArrayData = CreateDecalArray(8f);

                var singleton = fixture.GetSingleton();
                ref var entry = ref singleton.GetPersistent(definition);
                var gameObjectTrackedEntity = entry.Spawn(gameObject.GetEntityId(), gameObjectData, gameObjectArrayData);
                var entityTrackedEntity = entry.Spawn(entity, entityData, entityArrayData);
                fixture.UpdateSystems();

                AssertDataMatches(ref entry, gameObjectTrackedEntity, gameObjectData);
                AssertDataMatches(ref entry, entityTrackedEntity, entityData);
                AssertArrayDataMatches(ref entry, gameObjectTrackedEntity, gameObjectArrayData);
                AssertArrayDataMatches(ref entry, entityTrackedEntity, entityArrayData);
                Assert.IsTrue(fixture.IsPersistentAlive(definition, gameObjectTrackedEntity));
                Assert.IsTrue(fixture.IsPersistentAlive(definition, entityTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnUnsafeGameObject_WithDataAndArrayData_MatchesEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(200, VFXType.Persistent, capacity: 4, hasData: true, hasArrayData: true);
                fixture.CreateAndRegisterVisualEffect(definition);
                var gameObject = fixture.CreateTrackedGameObject();
                var entity = fixture.CreateTrackedEntity();
                var gameObjectData = CreateDecal(9f);
                var entityData = CreateDecal(10f);
                var gameObjectArrayData = CreateDecalBytes(11f);
                var entityArrayData = CreateDecalBytes(12f);

                var singleton = fixture.GetSingleton();
                ref var entry = ref singleton.GetPersistent(definition);
                var gameObjectTrackedEntity = SpawnUnsafe(ref entry, gameObject.GetEntityId(), gameObjectData, gameObjectArrayData);
                var entityTrackedEntity = SpawnUnsafe(ref entry, entity, entityData, entityArrayData);
                fixture.UpdateSystems();

                AssertDataMatches(ref entry, gameObjectTrackedEntity, gameObjectData);
                AssertDataMatches(ref entry, entityTrackedEntity, entityData);
                AssertUnsafeArrayDataMatches(ref entry, gameObjectTrackedEntity, gameObjectArrayData);
                AssertUnsafeArrayDataMatches(ref entry, entityTrackedEntity, entityArrayData);
                Assert.IsTrue(fixture.IsPersistentAlive(definition, gameObjectTrackedEntity));
                Assert.IsTrue(fixture.IsPersistentAlive(definition, entityTrackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnUnsafeGameObject_WithArrayData_MatchesEntityPath()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(210, VFXType.Persistent, capacity: 4, hasArrayData: true);
                fixture.CreateAndRegisterVisualEffect(definition);
                var gameObject = fixture.CreateTrackedGameObject();
                var entity = fixture.CreateTrackedEntity();
                var gameObjectArrayData = CreateDecalBytes(13f);
                var entityArrayData = CreateDecalBytes(14f);

                var singleton = fixture.GetSingleton();
                ref var entry = ref singleton.GetPersistent(definition);
                var gameObjectTrackedEntity = entry.SpawnUnsafe(gameObject.GetEntityId(), gameObjectArrayData);
                var entityTrackedEntity = entry.SpawnUnsafe(entity, entityArrayData);
                fixture.UpdateSystems();

                AssertUnsafeArrayDataMatches(ref entry, gameObjectTrackedEntity, gameObjectArrayData);
                AssertUnsafeArrayDataMatches(ref entry, entityTrackedEntity, entityArrayData);
                Assert.IsTrue(fixture.IsPersistentAlive(definition, gameObjectTrackedEntity));
                Assert.IsTrue(fixture.IsPersistentAlive(definition, entityTrackedEntity));
            });
        }

        private static VFXDecal CreateDecal(float value)
        {
            return new VFXDecal
            {
                Size = new Vector3(value, value + 1f, value + 2f),
                UvAtlas = new Vector4(value + 3f, value + 4f, value + 5f, value + 6f),
                Pivot = new Vector3(value + 7f, value + 8f, value + 9f),
                Opacity = value + 10f,
                DrawDistance = value + 11f,
                StartFade = value + 12f,
                AngleFade = new Vector2(value + 13f, value + 14f),
                NormalBlend = value + 15f
            };
        }

        private static NativeArray<VFXDecal> CreateDecalArray(float value)
        {
            var array = new NativeArray<VFXDecal>(2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            array[0] = CreateDecal(value);
            array[1] = CreateDecal(value + 100f);
            return array;
        }

        private static unsafe NativeArray<byte> CreateDecalBytes(float value)
        {
            var source = CreateDecalArray(value);
            var size = source.Length * UnsafeUtility.SizeOf<VFXDecal>();
            var bytes = new NativeArray<byte>(size, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.MemCpy(bytes.GetUnsafePtr(), source.GetUnsafeReadOnlyPtr(), size);
            return bytes;
        }

        private static unsafe TrackedEntity SpawnUnsafe(
            ref PersistentVFXEntry entry,
            EntityId entityId,
            VFXDecal data,
            NativeArray<byte> arrayData)
        {
            return entry.SpawnUnsafe(entityId, (byte*)&data, arrayData);
        }

        private static unsafe TrackedEntity SpawnUnsafe(
            ref PersistentVFXEntry entry,
            Entity entity,
            VFXDecal data,
            NativeArray<byte> arrayData)
        {
            return entry.SpawnUnsafe(entity, (byte*)&data, arrayData);
        }

        private static void AssertDataMatches(ref PersistentVFXEntry entry, TrackedEntity trackedEntity, VFXDecal expected)
        {
            Assert.IsTrue(entry.TryGetUpdateDataAsRef<VFXDecal>(trackedEntity, out var dataRef));
            AssertDecalMatches(expected, dataRef.Value);
        }

        private static void AssertArrayDataMatches(
            ref PersistentVFXEntry entry,
            TrackedEntity trackedEntity,
            NativeArray<VFXDecal> expected)
        {
            Assert.IsTrue(entry.TryGetArrayData<VFXDecal>(trackedEntity, out var array));
            Assert.That(array.Length, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                AssertDecalMatches(expected[i], array[i]);
            }
        }

        private static void AssertUnsafeArrayDataMatches(
            ref PersistentVFXEntry entry,
            TrackedEntity trackedEntity,
            NativeArray<byte> expected)
        {
            Assert.IsTrue(entry.TryGetArrayDataUnsafe(trackedEntity, out var array));
            Assert.That(array.Length, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(array[i], Is.EqualTo(expected[i]));
            }
        }

        private static void AssertDecalMatches(VFXDecal expected, VFXDecal actual)
        {
            Assert.That(actual.Size, Is.EqualTo(expected.Size));
            Assert.That(actual.UvAtlas, Is.EqualTo(expected.UvAtlas));
            Assert.That(actual.Pivot, Is.EqualTo(expected.Pivot));
            Assert.That(actual.Opacity, Is.EqualTo(expected.Opacity));
            Assert.That(actual.DrawDistance, Is.EqualTo(expected.DrawDistance));
            Assert.That(actual.StartFade, Is.EqualTo(expected.StartFade));
            Assert.That(actual.AngleFade, Is.EqualTo(expected.AngleFade));
            Assert.That(actual.NormalBlend, Is.EqualTo(expected.NormalBlend));
        }
    }
}

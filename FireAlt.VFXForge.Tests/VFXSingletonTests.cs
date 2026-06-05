using System;
using System.Collections;
using FireAlt.VFXForge.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireAlt.VFXForge.Tests
{
    public class VFXSingletonTests
    {
        [UnityTest]
        public IEnumerator RuntimeInitialization_CreatesValidSingleton()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var singleton = fixture.GetSingleton();

                Assert.IsTrue(singleton.IsValid());
            });
        }

        [UnityTest]
        public IEnumerator GetInstant_WhenKeyMissing_ThrowsAssertion()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var singleton = fixture.GetSingleton();

                Assert.Throws<ArgumentException>(() => singleton.GetInstant((VFXKey)100));
            });
        }

        [UnityTest]
        public IEnumerator GetInstant_WhenRegisteredThroughRuntimePath_DoesNotThrow()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(10, VFXType.Instant);
                fixture.CreateAndRegisterVisualEffect(definition);
                var singleton = fixture.GetSingleton();

                Assert.IsTrue(singleton.ContainsInstant(definition));
                Assert.DoesNotThrow(() =>
                {
                    ref var instant = ref singleton.GetInstant(definition);
                    instant.ResetRequestsCount();
                });
            });
        }

        [UnityTest]
        public IEnumerator GetPersistent_WhenKeyMissing_ThrowsAssertion()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var singleton = fixture.GetSingleton();

                Assert.Throws<ArgumentException>(() => singleton.GetPersistent((VFXKey)100));
            });
        }

        [UnityTest]
        public IEnumerator GetPersistent_WhenRegisteredThroughRuntimePath_DoesNotThrow()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(20, VFXType.Persistent, capacity: 2);
                fixture.CreateAndRegisterVisualEffect(definition);
                var singleton = fixture.GetSingleton();

                Assert.IsTrue(singleton.ContainsPersistent(definition));
                Assert.DoesNotThrow(() =>
                {
                    ref var persistent = ref singleton.GetPersistent(definition);
                    Assert.IsFalse(persistent.HasPendingRequests);
                });
            });
        }

        [UnityTest]
        public IEnumerator ParallelWriterGetPersistent_WhenRegisteredThroughRuntimePath_DoesNotThrow()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(30, VFXType.Persistent, capacity: 2);
                fixture.CreateAndRegisterVisualEffect(definition);
                var singleton = fixture.GetSingleton();

                Assert.DoesNotThrow(() =>
                {
                    ref var persistent = ref singleton.AsParallelWriter().GetPersistent(definition);
                    Assert.IsFalse(persistent.HasPendingRequests);
                });
            });
        }

        [UnityTest]
        public IEnumerator ParallelWriterGetInstant_WhenRegisteredThroughRuntimePath_DoesNotThrow()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(31, VFXType.Instant);
                fixture.CreateAndRegisterVisualEffect(definition);
                var singleton = fixture.GetSingleton();

                Assert.DoesNotThrow(() =>
                {
                    ref var instant = ref singleton.AsParallelWriter().GetInstant(definition);
                    Assert.IsFalse(instant.HasPendingRequests);
                });
            });
        }

        [UnityTest]
        public IEnumerator ParallelWriterGetPersistent_WhenKeyMissing_ThrowsAssertion()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var singleton = fixture.GetSingleton();
                var writer = singleton.AsParallelWriter();

                Assert.Throws<ArgumentException>(() => writer.GetPersistent((VFXKey)200));
            });
        }

        [UnityTest]
        public IEnumerator SpawnInstant_WhenRequestsSubmitted_ArePendingUntilSyncThenReset()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var singleDefinition = fixture.CreateDefinition(35, VFXType.Instant);
                var arrayDefinition = fixture.CreateDefinition(36, VFXType.Instant, hasData: true, hasArrayData: true);
                var singleVisualEffect = fixture.CreateAndRegisterVisualEffect(singleDefinition, "Instant Single VFX");
                var arrayVisualEffect = fixture.CreateAndRegisterVisualEffect(arrayDefinition, "Instant Array VFX");
                var arrayData = VFXTestData.CreateDecalArray(20f);
                var singleton = fixture.GetSingleton();

                ref var singleEntry = ref singleton.GetInstant(singleDefinition);
                singleEntry.Spawn();
                ref var arrayEntry = ref singleton.GetInstant(arrayDefinition);
                arrayEntry.Spawn(VFXTestData.CreateDecal(10f), arrayData);

                Assert.IsTrue(singleEntry.HasPendingRequests);
                Assert.That(singleEntry.RequestsCount, Is.EqualTo(1));
                Assert.That(singleEntry.ArrayRequestsCount, Is.EqualTo(0));
                Assert.IsTrue(arrayEntry.HasPendingRequests);
                Assert.That(arrayEntry.RequestsCount, Is.EqualTo(1));
                Assert.That(arrayEntry.ArrayRequestsCount, Is.EqualTo(arrayData.Length));

                fixture.UpdateSystems();

                Assert.IsTrue(singleVisualEffect.gameObject.activeSelf);
                Assert.IsTrue(arrayVisualEffect.gameObject.activeSelf);
                Assert.IsFalse(singleton.GetInstant(singleDefinition).HasPendingRequests);
                Assert.IsFalse(singleton.GetInstant(arrayDefinition).HasPendingRequests);
            });
        }

        [UnityTest]
        public IEnumerator SpawnInstantUnsafe_WhenDataAndArrayRequestsSubmitted_ArePendingUntilSyncThenReset()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(37, VFXType.Instant, hasData: true, hasArrayData: true);
                fixture.CreateAndRegisterVisualEffect(definition, "Instant Unsafe VFX");
                var data = VFXTestData.CreateDecal(70f);
                var arrayData = VFXTestData.CreateDecalBytes(80f);
                var singleton = fixture.GetSingleton();
                ref var entry = ref singleton.GetInstant(definition);

                SpawnUnsafe(ref entry, data, arrayData);

                Assert.IsTrue(entry.HasPendingRequests);
                Assert.That(entry.RequestsCount, Is.EqualTo(1));
                Assert.That(entry.ArrayRequestsCount, Is.EqualTo(2));

                fixture.UpdateSystems();

                Assert.IsFalse(singleton.GetInstant(definition).HasPendingRequests);
            });
        }

        [UnityTest]
        public IEnumerator SpawnInstant_WhenDataTypeMismatchesDefinition_Throws()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(38, VFXType.Instant, hasData: true);
                fixture.CreateAndRegisterVisualEffect(definition, "Instant Type Guard VFX");
                var singleton = fixture.GetSingleton();

                Assert.Throws<InvalidOperationException>(() => singleton.GetInstant(definition).Spawn(1));
            });
        }

        [UnityTest]
        public IEnumerator SpawnPersistent_WhenCapacityAvailable_BecomesAliveAfterSystemsUpdate()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(40, VFXType.Persistent, capacity: 2);
                fixture.CreateAndRegisterVisualEffect(definition);

                var trackedEntity = fixture.SpawnPersistent(definition, Entity.Null, trackingDuration: 1.25f);
                fixture.UpdateSystems();

                Assert.IsTrue(trackedEntity.IsValid);
                Assert.IsTrue(fixture.IsPersistentAlive(definition, trackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnPersistent_WhenCapacityExceeded_ReturnsInvalidEntity()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(50, VFXType.Persistent, capacity: 1);
                fixture.CreateAndRegisterVisualEffect(definition);

                var first = fixture.SpawnPersistent(definition, Entity.Null);
                var second = fixture.SpawnPersistent(definition, Entity.Null);

                Assert.IsTrue(first.IsValid);
                Assert.IsFalse(second.IsValid);
            });
        }

        [UnityTest]
        public IEnumerator SpawnPersistent_WhenKilledAfterResolution_CapacityCanBeReused()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(55, VFXType.Persistent, capacity: 1);
                fixture.CreateAndRegisterVisualEffect(definition);
                var first = fixture.SpawnPersistent(definition, Entity.Null);
                fixture.UpdateSystems();

                Assert.IsTrue(fixture.TryKillPersistent(definition, first));
                fixture.UpdateSystems();

                var second = fixture.SpawnPersistent(definition, Entity.Null);
                fixture.UpdateSystems();

                Assert.IsTrue(first.IsValid);
                Assert.IsTrue(second.IsValid);
                Assert.IsFalse(fixture.IsPersistentAlive(definition, first));
                Assert.IsTrue(fixture.IsPersistentAlive(definition, second));
            });
        }

        [UnityTest]
        public IEnumerator SpawnPersistent_WhenTrackingDurationExpires_IsNotAliveAfterSystemsUpdate()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(56, VFXType.Persistent, capacity: 2);
                fixture.CreateAndRegisterVisualEffect(definition);
                fixture.SetTime(1.0, 0.1f);
                var trackedEntity = fixture.SpawnPersistent(definition, Entity.Null, trackingDuration: 0.25f);
                fixture.UpdateSystems();

                Assert.IsTrue(fixture.IsPersistentAlive(definition, trackedEntity));

                fixture.SetTime(1.5, 0.1f);
                fixture.UpdateSystems();

                Assert.IsFalse(fixture.IsPersistentAlive(definition, trackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator SpawnPersistent_WhenTrackingDurationNegative_ThrowsAssertion()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(60, VFXType.Persistent, capacity: 1);
                fixture.CreateAndRegisterVisualEffect(definition);

                Assert.Throws<UnityEngine.Assertions.AssertionException>(() =>
                    fixture.SpawnPersistent(definition, Entity.Null, trackingDuration: -0.01f));
            });
        }

        [UnityTest]
        public IEnumerator SyncPersistent_WhenTransformSystemRunsBeforeSync_ResolvesSpawn()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(85, VFXType.Persistent, capacity: 2);
                fixture.CreateAndRegisterVisualEffect(definition);
                var trackedEntity = fixture.SpawnPersistent(definition, Entity.Null);

                fixture.VFXTransformSystem.Update(fixture.World.Unmanaged);
                fixture.SyncVFXSystem.Update(fixture.World.Unmanaged);

                Assert.IsTrue(fixture.IsPersistentAlive(definition, trackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator TrySetUpdateData_WhenDeferredAndResolved_UpdatesVisibleData()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(86, VFXType.Persistent, capacity: 2, hasData: true);
                fixture.CreateAndRegisterVisualEffect(definition);
                var initialData = VFXTestData.CreateDecal(30f);
                var deferredData = VFXTestData.CreateDecal(40f);
                var resolvedData = VFXTestData.CreateDecal(50f);
                var singleton = fixture.GetSingleton();
                ref var entry = ref singleton.GetPersistent(definition);
                var trackedEntity = entry.Spawn(Entity.Null, initialData);

                Assert.IsTrue(entry.TrySetUpdateData(trackedEntity, deferredData));
                VFXTestData.AssertDataMatches(ref entry, trackedEntity, deferredData);
                fixture.UpdateSystems();

                VFXTestData.AssertDataMatches(ref entry, trackedEntity, deferredData);
                Assert.IsTrue(entry.TrySetUpdateData(trackedEntity, resolvedData));
                VFXTestData.AssertDataMatches(ref entry, trackedEntity, resolvedData);
            });
        }

        [UnityTest]
        public IEnumerator TryGetArrayData_WhenDeferredAndResolved_ReturnsCopiedData()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(87, VFXType.Persistent, capacity: 2, hasArrayData: true);
                fixture.CreateAndRegisterVisualEffect(definition);
                var arrayData = VFXTestData.CreateDecalArray(60f);
                var singleton = fixture.GetSingleton();
                ref var entry = ref singleton.GetPersistent(definition);
                var trackedEntity = entry.Spawn(Entity.Null, arrayData);

                VFXTestData.AssertArrayDataMatches(ref entry, trackedEntity, arrayData);
                fixture.UpdateSystems();

                VFXTestData.AssertArrayDataMatches(ref entry, trackedEntity, arrayData);
            });
        }

        [UnityTest]
        public IEnumerator TrySetUpdateData_WhenTrackedEntityKilled_ReturnsFalse()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(88, VFXType.Persistent, capacity: 2, hasData: true);
                fixture.CreateAndRegisterVisualEffect(definition);
                var singleton = fixture.GetSingleton();
                ref var entry = ref singleton.GetPersistent(definition);
                var trackedEntity = entry.Spawn(Entity.Null, VFXTestData.CreateDecal(90f));
                fixture.UpdateSystems();

                Assert.IsTrue(fixture.TryKillPersistent(definition, trackedEntity));
                fixture.UpdateSystems();

                Assert.IsFalse(entry.TrySetUpdateData(trackedEntity, VFXTestData.CreateDecal(100f)));
                Assert.IsFalse(entry.TryGetUpdateDataAsRef<VFXDecal>(trackedEntity, out _));
            });
        }

        [UnityTest]
        public IEnumerator SpawnPersistent_WhenResolvedTrackedEntityDestroyed_IsNotAliveAfterSystemsUpdate()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(89, VFXType.Persistent, capacity: 2);
                fixture.CreateAndRegisterVisualEffect(definition);
                var entity = fixture.CreateTrackedEntity();
                var trackedEntity = fixture.SpawnPersistent(definition, entity);
                fixture.UpdateSystems();

                Assert.IsTrue(fixture.IsPersistentAlive(definition, trackedEntity));

                fixture.World.EntityManager.DestroyEntity(entity);
                fixture.UpdateSystems();

                Assert.IsFalse(fixture.IsPersistentAlive(definition, trackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator TryKillPersistent_WhenResolvedEntityKilled_IsNotAliveAfterSystemsUpdate()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(70, VFXType.Persistent, capacity: 2);
                fixture.CreateAndRegisterVisualEffect(definition);

                var trackedEntity = fixture.SpawnPersistent(definition, Entity.Null);
                fixture.UpdateSystems();

                Assert.IsTrue(fixture.IsPersistentAlive(definition, trackedEntity));
                Assert.IsTrue(fixture.TryKillPersistent(definition, trackedEntity));

                fixture.UpdateSystems();

                Assert.IsFalse(fixture.IsPersistentAlive(definition, trackedEntity));
            });
        }

        [UnityTest]
        public IEnumerator TryKillPersistent_WhenDeferredEntityKilled_DoesNotBecomeAliveAfterSystemsUpdate()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(80, VFXType.Persistent, capacity: 2);
                fixture.CreateAndRegisterVisualEffect(definition);

                var trackedEntity = fixture.SpawnPersistent(definition, Entity.Null);

                Assert.IsTrue(fixture.TryKillPersistent(definition, trackedEntity));
                fixture.UpdateSystems();

                Assert.IsFalse(fixture.IsPersistentAlive(definition, trackedEntity));
            });
        }

        private static unsafe void SpawnUnsafe(ref InstantVFXEntry entry, VFXDecal data, NativeArray<byte> arrayData)
        {
            entry.SpawnUnsafe((byte*)&data, arrayData);
        }
    }
}

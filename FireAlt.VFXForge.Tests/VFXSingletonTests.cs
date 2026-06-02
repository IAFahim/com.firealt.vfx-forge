using System;
using System.Collections;
using FireAlt.VFXForge.Data;
using NUnit.Framework;
using Unity.Entities;
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
    }
}

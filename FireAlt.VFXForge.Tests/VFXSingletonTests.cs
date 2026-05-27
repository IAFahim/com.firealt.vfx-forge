using FireAlt.Core.Testing;
using FireAlt.VFXForge.Data;
using NUnit.Framework;
using Unity.Entities;

namespace FireAlt.VFXForge.Tests
{
    public class VFXSingletonTests : ECSTestsFixture
    {
        [Test]
        public void Constructor_InitializesAllMaps()
        {
            var singleton = new VFXSingleton(8);
            try
            {
                Assert.IsTrue(singleton.IsValid());
                Assert.IsTrue(singleton.IsPersistent.IsCreated);
                Assert.IsTrue(singleton.InstantVFXGraphEntries.IsCreated);
                Assert.IsTrue(singleton.PersistentVFXGraphEntries.IsCreated);
                Assert.IsTrue(singleton.InstantAliveVFX.IsCreated);
                Assert.IsTrue(singleton.PersistentAliveVFX.IsCreated);
            }
            finally
            {
                singleton.Dispose();
            }
        }

        [Test]
        public void GetInstant_WhenKeyMissing_ThrowsAssertion()
        {
            var singleton = new VFXSingleton(2);
            try
            {
                Assert.Throws<UnityEngine.Assertions.AssertionException>(() => singleton.GetInstant((VFXKey)100));
            }
            finally
            {
                singleton.Dispose();
            }
        }

        [Test]
        public void GetInstant_WhenKeyRegistered_DoesNotThrow()
        {
            var singleton = new VFXSingleton(2);
            try
            {
                var key = (VFXKey)10;
                var entry = VFXTestHelper.CreateInstantEntry();
                singleton.InstantVFXGraphEntries.Add(key, entry);

                Assert.DoesNotThrow(() =>
                {
                    ref var instant = ref singleton.GetInstant(key);
                    instant.ResetRequestsCount();
                });
            }
            finally
            {
                singleton.Dispose();
            }
        }

        [Test]
        public void GetPersistent_WhenKeyMissing_ThrowsAssertion()
        {
            var singleton = new VFXSingleton(2);
            try
            {
                Assert.Throws<UnityEngine.Assertions.AssertionException>(() => singleton.GetPersistent((VFXKey)100));
            }
            finally
            {
                singleton.Dispose();
            }
        }

        [Test]
        public void ParallelWriterGetPersistent_WhenKeyRegistered_DoesNotThrow()
        {
            var singleton = new VFXSingleton(2);
            try
            {
                var key = (VFXKey)20;
                var entry = VFXTestHelper.CreatePersistentEntry(2);
                singleton.PersistentVFXGraphEntries.Add(key, entry);

                Assert.DoesNotThrow(() =>
                {
                    ref var persistent = ref singleton.AsParallelWriter().GetPersistent(key);
                    Assert.That(persistent.Capacity, Is.EqualTo(2));
                });
            }
            finally
            {
                singleton.Dispose();
            }
        }

        [Test]
        public void ParallelWriterGetPersistent_WhenKeyMissing_ThrowsAssertion()
        {
            var singleton = new VFXSingleton(2);
            try
            {
                var writer = singleton.AsParallelWriter();
                Assert.Throws<UnityEngine.Assertions.AssertionException>(() => writer.GetPersistent((VFXKey)200));
            }
            finally
            {
                singleton.Dispose();
            }
        }

        [Test]
        public void SpawnPersistent_WhenCapacityAvailable_WritesExpectedState()
        {
            var singleton = new VFXSingleton(2);
            var entry = VFXTestHelper.CreatePersistentEntry(2);

            try
            {
                var trackedEntity = singleton.AsInternal().SpawnPersistent(ref entry, Entity.Null, default, 1.25f);

                Assert.IsTrue(trackedEntity.IsValid);
                Assert.That(trackedEntity.IndexInData, Is.EqualTo(0));
                Assert.That(trackedEntity.SystemVersion, Is.EqualTo(SyncVFXSystem.SystemVersion));
                Assert.That(entry.RequestsCount, Is.EqualTo(1));
                Assert.That(entry.ArrayRequestsCount, Is.EqualTo(0));
                Assert.That(entry.SpawnIndexBuffer.Length, Is.EqualTo(1));
                Assert.That(entry.AliveMask.IsSet(trackedEntity.IndexInData), Is.True);

                var transform = entry.TransformBuffer[trackedEntity.IndexInData];
                Assert.That(transform.IsAlive(), Is.True);
                Assert.That(transform.TrackingDuration, Is.EqualTo(1.25f));
            }
            finally
            {
                entry.Dispose();
                singleton.Dispose();
            }
        }

        [Test]
        public void SpawnPersistent_WhenCapacityExceeded_ReturnsInvalidEntity()
        {
            var singleton = new VFXSingleton(1);
            var entry = VFXTestHelper.CreatePersistentEntry(1);

            try
            {
                var first = singleton.AsInternal().SpawnPersistent(ref entry, Entity.Null, default, 0f);
                var second = singleton.AsInternal().SpawnPersistent(ref entry, Entity.Null, default, 0f);

                Assert.IsTrue(first.IsValid);
                Assert.IsFalse(second.IsValid);
                Assert.That(entry.RequestsCount, Is.EqualTo(1));
            }
            finally
            {
                entry.Dispose();
                singleton.Dispose();
            }
        }

        [Test]
        public void SpawnPersistent_WhenTrackingDurationNegative_ThrowsAssertion()
        {
            var singleton = new VFXSingleton(1);
            var entry = VFXTestHelper.CreatePersistentEntry(1);

            try
            {
                Assert.Throws<UnityEngine.Assertions.AssertionException>(() => singleton.AsInternal().SpawnPersistent(ref entry, Entity.Null, default, -0.01f));
            }
            finally
            {
                entry.Dispose();
                singleton.Dispose();
            }
        }

        [Test]
        public void KillPersistent_WhenResolvedEntityKilled_IndexGetsReused()
        {
            var singleton = new VFXSingleton(2);
            var entry = VFXTestHelper.CreatePersistentEntry(2);

            try
            {
                var first = singleton.AsInternal().SpawnPersistent(ref entry, Entity.Null, default, 0f);
                singleton.AsInternal().KillPersistent(ref entry, first);
                var second = singleton.AsInternal().SpawnPersistent(ref entry, Entity.Null, default, 0f);

                Assert.IsTrue(second.IsValid);
                Assert.That(second.IndexInData, Is.EqualTo(first.IndexInData));
            }
            finally
            {
                entry.Dispose();
                singleton.Dispose();
            }
        }

        [Test]
        public void KillPersistent_WhenDeferredEntityPassed_ThrowsAssertion()
        {
            var singleton = new VFXSingleton(2);
            var entry = VFXTestHelper.CreatePersistentEntry(2);

            try
            {
                var deferred = new TrackedEntity(Entity.Null, 0, -SyncVFXSystem.SystemVersion);
                Assert.Throws<UnityEngine.Assertions.AssertionException>(() => singleton.AsInternal().KillPersistent(ref entry, deferred));
            }
            finally
            {
                entry.Dispose();
                singleton.Dispose();
            }
        }
    }
}

using System;
using System.Collections;
using FireAlt.VFXForge.Data;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.TestTools;

namespace FireAlt.VFXForge.Tests
{
    public class InstantVFXTests
    {
        [UnityTest]
        public IEnumerator Spawn_WhenRequestsSubmitted_ArePendingUntilSyncThenReset()
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
        public IEnumerator SpawnUnsafe_WhenDataAndArrayRequestsSubmitted_ArePendingUntilSyncThenReset()
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
        public IEnumerator Spawn_WhenDataTypeMismatchesDefinition_Throws()
        {
            yield return VFXPlayModeTestFixture.Run(fixture =>
            {
                var definition = fixture.CreateDefinition(38, VFXType.Instant, hasData: true);
                fixture.CreateAndRegisterVisualEffect(definition, "Instant Type Guard VFX");
                var singleton = fixture.GetSingleton();

                Assert.Throws<InvalidOperationException>(() => singleton.GetInstant(definition).Spawn(1));
            });
        }

        private static unsafe void SpawnUnsafe(ref InstantVFXEntry entry, VFXDecal data, NativeArray<byte> arrayData)
        {
            entry.SpawnUnsafe((byte*)&data, arrayData);
        }
    }
}

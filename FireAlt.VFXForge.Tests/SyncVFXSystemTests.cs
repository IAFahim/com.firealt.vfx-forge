using KrasCore.Testing;
using NUnit.Framework;
using Unity.Entities;

namespace FireAlt.VFXForge.Tests
{
    public class SyncVFXSystemTests : ECSTestsFixture
    {
        private SystemHandle _syncVfxSystem;
        private SystemHandle _vfxTransformSystem;
        
        public override void Setup()
        {
            base.Setup();
            _syncVfxSystem = World.CreateSystem<SyncVFXSystem>();
            _vfxTransformSystem = World.CreateSystem<VFXTransformSystem>();
        }

        [Test]
        public void OnCreate_CreatesSingletonsAndGraphicsBufferRegistry()
        {
            var singletonEntity = Manager.CreateEntityQuery(typeof(VFXSingleton)).GetSingletonEntity();
            var singleton = Manager.GetComponentData<VFXSingleton>(singletonEntity);

            var graphicsEntity = Manager.CreateEntityQuery(typeof(VFXGraphicsBuffersSingleton)).GetSingletonEntity();
            var graphicsBuffers = Manager.GetComponentObject<VFXGraphicsBuffersSingleton>(graphicsEntity);

            Assert.IsTrue(singleton.IsValid());
            Assert.IsNotNull(graphicsBuffers);
            Assert.IsNotNull(graphicsBuffers.InstantVFXGraphEntries);
            Assert.IsNotNull(graphicsBuffers.PersistentVFXGraphEntries);
            Assert.That(graphicsBuffers.InstantVFXGraphEntries.Count, Is.EqualTo(0));
            Assert.That(graphicsBuffers.PersistentVFXGraphEntries.Count, Is.EqualTo(0));
        }

        [Test]
        public void Update_WhenNoRegisteredVfx_DoesNotThrowAndIncrementsVersion()
        {
            var before = SyncVFXSystem.SystemVersion;

            Assert.DoesNotThrow(UpdateSystems);
            Assert.That(SyncVFXSystem.SystemVersion, Is.EqualTo(before + 1));
        }

        private void UpdateSystems()
        {
            _vfxTransformSystem.Update(WorldUnmanaged);
            _syncVfxSystem.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }
    }
}

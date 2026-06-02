using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FireAlt.VFXForge.Data;
using JetBrains.Annotations;
using FireAlt.Core.Extensions;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;

namespace FireAlt.VFXForge
{
    public partial struct VFXSingleton : IComponentData, IDisposable
    {
        public ParallelWriter AsParallelWriter()
        {
            Assert.IsTrue(IsValid());
            return new ParallelWriter(InstantVFXGraphEntries, PersistentVFXGraphEntries);
        }

        public bool IsValid()
        {
            return IsPersistent.IsCreated;
        }

        public ref InstantVFXEntry GetInstant(in VFXKey key)
        {
            CheckContainsInstant(InstantVFXGraphEntries, key);
            ref var entry = ref InstantVFXGraphEntries.GetValueAsRef(key);
            return ref entry;
        }
        
        public ref PersistentVFXEntry GetPersistent(in VFXKey key)
        {
            CheckContainsPersistent(PersistentVFXGraphEntries, key);
            ref var entry = ref PersistentVFXGraphEntries.GetValueAsRef(key);
            return ref entry;
        }
        
        public bool ContainsInstant(in VFXKey key)
        {
            return InstantVFXGraphEntries.ContainsKey(key);
        }

        public bool ContainsPersistent(in VFXKey key)
        {
            return PersistentVFXGraphEntries.ContainsKey(key);
        }

        public void ForceTimeout(in VFXKey key)
        {
            if (IsPersistent[key])
            {
                PersistentAliveVFX.GetValueAsRef(key).InactivityTimeRemaining = 0;
            }
            else
            {
                InstantAliveVFX.GetValueAsRef(key).InactivityTimeRemaining = 0;
            }
        }
        
        public struct ParallelWriter
        {
            [ReadOnly]
            private NativeHashMap<VFXKey, InstantVFXEntry> _instantVFXGraphEntries;
            [ReadOnly]
            private NativeHashMap<VFXKey, PersistentVFXEntry> _persistentVFXGraphEntries;

            internal ParallelWriter(NativeHashMap<VFXKey, InstantVFXEntry> instantVFXGraphEntries,
                NativeHashMap<VFXKey, PersistentVFXEntry> persistentVFXGraphEntries)
            {
                _instantVFXGraphEntries = instantVFXGraphEntries;
                _persistentVFXGraphEntries = persistentVFXGraphEntries;
            }

            public ref InstantVFXEntry GetInstant(in VFXKey key)
            {
                CheckContainsInstant(_instantVFXGraphEntries, key);
                ref var entry = ref _instantVFXGraphEntries.GetValueAsRef(key);
                return ref entry;
            }
            
            public ref PersistentVFXEntry GetPersistent(in VFXKey key)
            {
                CheckContainsPersistent(_persistentVFXGraphEntries, key);
                ref var entry = ref _persistentVFXGraphEntries.GetValueAsRef(key);
                return ref entry;
            }
            
            public bool ContainsInstant(in VFXKey key)
            {
                return _instantVFXGraphEntries.ContainsKey(key);
            }

            public bool ContainsPersistent(in VFXKey key)
            {
                return _persistentVFXGraphEntries.ContainsKey(key);
            }
        }
        
        [AssertionMethod]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckContainsInstant(NativeHashMap<VFXKey, InstantVFXEntry> hashMap, VFXKey key)
        {
            if (!hashMap.ContainsKey(key))
            {
                throw new ArgumentException($"Instant VFX Graph Entries does not contain {key}. The VFX was not yet registered.");
            }
        }

        [AssertionMethod]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckContainsPersistent(NativeHashMap<VFXKey, PersistentVFXEntry> hashMap, VFXKey key)
        {
            if (!hashMap.ContainsKey(key))
            {
                throw new ArgumentException($"Persistent VFX Graph Entries does not contain {key}. The VFX was not yet registered.");
            }
        }
        
        [SuppressMessage("ReSharper", "Unity.BurstLoadingManagedType")]
        public void Dispose()
        {
            IsPersistent.Dispose();
            InstantAliveVFX.Dispose();
            PersistentAliveVFX.Dispose();
            
            foreach (var pair in InstantVFXGraphEntries)
            {
                var hve = pair.Value.HybridVisualEffect.Value;
                if (hve != null)
                    hve.VisualEffect.Reinit();
                pair.Value.Dispose();
            }
            InstantVFXGraphEntries.Dispose();
            foreach (var pair in PersistentVFXGraphEntries)
            {
                var hve = pair.Value.HybridVisualEffect.Value;
                if (hve != null)
                    hve.VisualEffect.Reinit();
                pair.Value.Dispose();
            }
            PersistentVFXGraphEntries.Dispose();
        }
    }
}

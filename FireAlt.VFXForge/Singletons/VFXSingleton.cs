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
    /// <summary>
    /// Stores the registered VFX graph entries and their runtime activity state for the current world.
    /// </summary>
    /// <remarks>
    /// This singleton owns the native containers used by VFX Forge runtime systems. Public callers use it to
    /// retrieve registered instant and persistent graph entries, while <see cref="SyncVFXSystem"/> consumes the
    /// alive maps to enable, upload, timeout, and disable backing <see cref="HybridVisualEffect"/> instances.
    /// </remarks>
    public partial struct VFXSingleton : IComponentData, IDisposable
    {
        /// <summary>
        /// Creates a job-safe writer that can retrieve registered VFX entries from parallel jobs.
        /// </summary>
        /// <returns>A parallel writer backed by this singleton's registered instant and persistent entries.</returns>
        /// <exception cref="UnityEngine.Assertions.AssertionException">
        /// Thrown when this singleton has not been initialized.
        /// </exception>
        public ParallelWriter AsParallelWriter()
        {
            Assert.IsTrue(IsValid());
            return new ParallelWriter(InstantVFXGraphEntries, PersistentVFXGraphEntries);
        }

        /// <summary>
        /// Returns whether this singleton has been initialized with its native containers.
        /// </summary>
        /// <returns><see langword="true"/> when the singleton owns created native containers; otherwise, <see langword="false"/>.</returns>
        public bool IsValid()
        {
            return IsPersistent.IsCreated;
        }

        /// <summary>
        /// Gets the registered instant VFX entry for the specified key.
        /// </summary>
        /// <param name="key">The VFX definition key to look up.</param>
        /// <returns>A mutable reference to the registered instant VFX entry.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown in collection-check builds when the key has not been registered as an instant VFX entry.
        /// </exception>
        public ref InstantVFXEntry GetInstant(in VFXKey key)
        {
            CheckContainsInstant(InstantVFXGraphEntries, key);
            ref var entry = ref InstantVFXGraphEntries.GetValueAsRef(key);
            return ref entry;
        }
        
        /// <summary>
        /// Gets the registered persistent VFX entry for the specified key.
        /// </summary>
        /// <param name="key">The VFX definition key to look up.</param>
        /// <returns>A mutable reference to the registered persistent VFX entry.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown in collection-check builds when the key has not been registered as a persistent VFX entry.
        /// </exception>
        public ref PersistentVFXEntry GetPersistent(in VFXKey key)
        {
            CheckContainsPersistent(PersistentVFXGraphEntries, key);
            ref var entry = ref PersistentVFXGraphEntries.GetValueAsRef(key);
            return ref entry;
        }
        
        /// <summary>
        /// Returns whether an instant VFX entry has been registered for the specified key.
        /// </summary>
        /// <param name="key">The VFX definition key to look up.</param>
        /// <returns><see langword="true"/> when an instant entry exists for the key; otherwise, <see langword="false"/>.</returns>
        public bool ContainsInstant(in VFXKey key)
        {
            return InstantVFXGraphEntries.ContainsKey(key);
        }

        /// <summary>
        /// Returns whether a persistent VFX entry has been registered for the specified key.
        /// </summary>
        /// <param name="key">The VFX definition key to look up.</param>
        /// <returns><see langword="true"/> when a persistent entry exists for the key; otherwise, <see langword="false"/>.</returns>
        public bool ContainsPersistent(in VFXKey key)
        {
            return PersistentVFXGraphEntries.ContainsKey(key);
        }

        /// <summary>
        /// Forces the active visual effect for the specified key to expire on the next timeout pass.
        /// </summary>
        /// <param name="key">The VFX definition key whose alive entry should be timed out.</param>
        /// <remarks>
        /// The method sets the tracked alive entry's inactivity timer to zero.
        /// </remarks>
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
        
        /// <summary>
        /// Provides read-only lookup access to registered VFX entries from parallel jobs.
        /// </summary>
        /// <remarks>
        /// The writer does not expose alive-map state. It is intended for systems that need to enqueue instant or
        /// persistent VFX work from jobs after the corresponding VFX definitions have already been registered.
        /// </remarks>
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

            /// <summary>
            /// Gets the registered instant VFX entry for the specified key.
            /// </summary>
            /// <param name="key">The VFX definition key to look up.</param>
            /// <returns>A mutable reference to the registered instant VFX entry.</returns>
            public ref InstantVFXEntry GetInstant(in VFXKey key)
            {
                CheckContainsInstant(_instantVFXGraphEntries, key);
                ref var entry = ref _instantVFXGraphEntries.GetValueAsRef(key);
                return ref entry;
            }
            
            /// <summary>
            /// Gets the registered persistent VFX entry for the specified key.
            /// </summary>
            /// <param name="key">The VFX definition key to look up.</param>
            /// <returns>A mutable reference to the registered persistent VFX entry.</returns>
            public ref PersistentVFXEntry GetPersistent(in VFXKey key)
            {
                CheckContainsPersistent(_persistentVFXGraphEntries, key);
                ref var entry = ref _persistentVFXGraphEntries.GetValueAsRef(key);
                return ref entry;
            }
            
            /// <summary>
            /// Returns whether an instant VFX entry has been registered for the specified key.
            /// </summary>
            /// <param name="key">The VFX definition key to look up.</param>
            /// <returns><see langword="true"/> when an instant entry exists for the key; otherwise, <see langword="false"/>.</returns>
            public bool ContainsInstant(in VFXKey key)
            {
                return _instantVFXGraphEntries.ContainsKey(key);
            }

            /// <summary>
            /// Returns whether a persistent VFX entry has been registered for the specified key.
            /// </summary>
            /// <param name="key">The VFX definition key to look up.</param>
            /// <returns><see langword="true"/> when a persistent entry exists for the key; otherwise, <see langword="false"/>.</returns>
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

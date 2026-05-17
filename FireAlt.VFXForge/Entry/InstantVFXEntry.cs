using System;
using BovineLabs.Core.Extensions;
using FireAlt.VFXForge.Data;
using KrasCore;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;

namespace FireAlt.VFXForge
{
    public struct InstantVFXEntry : IVFXGraphEntry, IDisposable
    {
        public struct Requests
        {
            public int RequestsCount;
            public int ArrayRequestsCount;
        }
        
        public UnityObjectRef<HybridVisualEffect> HybridVisualEffect { get; }
        public VFXKey VFXKey { get; }
        public ulong DataStableTypeHash { get; } 
        public ulong ArrayDataStableTypeHash { get; } 
        public int DataSizeInBytes { get; }
        public int ArrayDataSizeInBytes { get; }

        internal UnsafeThreadData<Requests> PendingRequestsCount;
        internal UnsafeThreadToListMapper<byte> DataBuffer;
        
        internal UnsafeThreadToListMapper<byte> ArrayDataBuffer;
        internal UnsafeThreadToListMapper<VFXArraySpawnIndex> ArraySpawnIndexBuffer;
        internal UnsafeThreadToListMapper<VFXArrayPtr> ArrayPtrBuffer;
        
        public int RequestsCount
        {
            get
            {
                var sum = 0;
                foreach (var threadCount in PendingRequestsCount)
                {
                    sum += threadCount.RequestsCount;
                }
                return sum;
            }
        }

        public int ArrayRequestsCount
        {
            get
            {
                var sum = 0;
                foreach (var threadCount in PendingRequestsCount)
                {
                    sum += threadCount.ArrayRequestsCount;
                }
                return sum;
            }
        }
        
        public bool HasPendingRequests => RequestsCount > 0 || ArrayRequestsCount > 0;
        
        public void ResetRequestsCount()
        {
            PendingRequestsCount.Clear();
        }

        internal InstantVFXEntry(HybridVisualEffect hybridVisualEffect)
        {
            this = default;
            var definition = hybridVisualEffect.VFXDefinition;
            HybridVisualEffect = hybridVisualEffect;
            DataStableTypeHash = definition.vfxDataType;
            ArrayDataStableTypeHash = definition.vfxArrayDataType;
            DataSizeInBytes = definition.DataGpuSize;
            ArrayDataSizeInBytes = definition.ArrayDataGpuSize;
            VFXKey = definition;
        }
        
        public void Spawn()
        {
            Common.CheckZeroSized(DataStableTypeHash);
            SpawnBase();
        }
        
        public void Spawn<T>(T spawnData)
            where T : unmanaged
        {
            Common.CheckStableTypeHash<T>(DataStableTypeHash);
            SpawnBase();
            SetData(spawnData);
        }
        
        public void Spawn<U>(NativeArray<U> arrayData) 
            where U : unmanaged
        {
            Common.CheckZeroSized(DataStableTypeHash);
            Common.CheckStableTypeHash<U>(ArrayDataStableTypeHash);
            SpawnBase();
            SpawnArray(arrayData.AsBytes());
        }
        
        public void Spawn<T, U>(T spawnData, NativeArray<U> arrayData)
            where T : unmanaged
            where U : unmanaged
        {
            Common.CheckStableTypeHash<T>(DataStableTypeHash);
            Common.CheckStableTypeHash<U>(ArrayDataStableTypeHash);
            SpawnBase();
            SetData(spawnData);
            SpawnArray(arrayData.AsBytes());
        }
        
        public unsafe void SpawnUnsafe(byte* spawnData, NativeArray<byte> arrayData = default)
        {
            SpawnBase();
            if (arrayData.IsCreated)
            {
                SpawnArray(arrayData);
            }
            GetThreadList(DataBuffer).AddDataUnsafe(spawnData, DataSizeInBytes);
        }
        
        public void SpawnUnsafe(NativeArray<byte> arrayData)
        {
            Common.CheckZeroSized(DataStableTypeHash);
            SpawnBase();
            if (arrayData.IsCreated)
            {
                SpawnArray(arrayData);
            }
        }
        
        private void SpawnBase()
        {
            PendingRequestsCount.GetUnsafeThreadData(JobsUtility.ThreadIndex).RequestsCount++;
        }
        
        private void SetData<T>(T spawnData)
            where T : unmanaged
        {
            GetThreadList(DataBuffer).AddData(spawnData);
        }
        
        private unsafe void SpawnArray(NativeArray<byte> arrayData)
        {
            ref var arrayDataBuffer = ref GetThreadList(ArrayDataBuffer);
            ref var arrayPtrBuffer = ref GetThreadList(ArrayPtrBuffer);
            ref var arraySpawnIndexBuffer = ref GetThreadList(ArraySpawnIndexBuffer);

            var indexInData = (uint)arrayPtrBuffer.Length;
            var arrayStartIndex = arrayDataBuffer.Length / ArrayDataSizeInBytes;
            var arrayLength = arrayData.Length / ArrayDataSizeInBytes;
            
            arrayDataBuffer.AddRange(arrayData.GetUnsafePtr(), arrayData.Length);
            arrayPtrBuffer.Add(new VFXArrayPtr(arrayStartIndex, arrayLength));
            for (uint indexInArray = 0; indexInArray < arrayLength; indexInArray++)
            {
                arraySpawnIndexBuffer.Add(new VFXArraySpawnIndex(indexInData, indexInArray));
            }
            
            PendingRequestsCount.GetUnsafeThreadData(JobsUtility.ThreadIndex).ArrayRequestsCount += arrayLength;
        }

        private ref UnsafeList<T> GetThreadList<T>(UnsafeThreadToListMapper<T> mapper) 
            where T : unmanaged
        {
            return ref mapper.ThreadList.GetUnsafeList(JobsUtility.ThreadIndex);
        }
        
        public void Dispose()
        {
            PendingRequestsCount.Dispose();
            if (DataBuffer.IsCreated) DataBuffer.Dispose();
            if (ArrayDataBuffer.IsCreated) ArrayDataBuffer.Dispose();
            if (ArrayPtrBuffer.IsCreated) ArrayPtrBuffer.Dispose();
            if (ArraySpawnIndexBuffer.IsCreated) ArraySpawnIndexBuffer.Dispose();
        }
    }
}

using FireAlt.VFXForge.Data;
using KrasCore;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.VFX;

namespace FireAlt.VFXForge
{
    public class PersistentVFXGraphicsBuffers : VFXGraphicsBuffers
    {
        private static readonly ProfilerMarker TransformMarker = new("Set TransformBuffer");
        private static readonly ProfilerMarker SpawnIndexMarker = new("Set SpawnIndexBuffer");
        private static readonly ProfilerMarker DataMarker = new("Set DataBuffer");
        private static readonly ProfilerMarker ArrayDataMarker = new("Set ArrayDataBuffer");
        private static readonly ProfilerMarker ArrayPtrMarker = new("Set ArrayPtrBuffer");
        
        private GraphicsBuffer _spawnIndexBuffer;
        private readonly GraphicsBuffer _transformBuffer;
        
        private readonly GraphicsBuffer _dataBuffer;
        private readonly GraphicsBuffer _arrayPtrBuffer;
        private GraphicsBuffer _arrayDataBuffer;
        
        public PersistentVFXGraphicsBuffers(VisualEffect target, int dataGpuSize, int arrayDataGpuSize, int doubleCapacity) 
            : base(target, dataGpuSize, arrayDataGpuSize)
        {
            CreateGraphicsBuffer(ref _spawnIndexBuffer, VFXProperties.SpawnIndexBuffer, doubleCapacity / 2, UnsafeUtility.SizeOf<VFXSpawnIndex>());
            CreateGraphicsBuffer(ref _transformBuffer, VFXProperties.TransformBuffer, doubleCapacity, UnsafeUtility.SizeOf<VFXTransform>());
            
            if (dataGpuSize != 0)
            {
                CreateGraphicsBuffer(ref _dataBuffer, VFXProperties.DataBuffer, doubleCapacity, dataGpuSize);
            }
            if (arrayDataGpuSize != 0)
            {
                CreateGraphicsBuffer(ref _arrayPtrBuffer, VFXProperties.ArrayPtrBuffer, doubleCapacity, UnsafeUtility.SizeOf<VFXArrayPtr>());
                ResizeArrayDataBuffer(4096);
            }
        }

        protected override void CheckHasSharedBuffers()
        {
            CheckHasBuffer(VFXProperties.SpawnIndexBuffer);
            CheckHasBuffer(VFXProperties.TransformBuffer);
        }
        
        protected override void CheckHasDataBuffers()
        {
            CheckHasBuffer(VFXProperties.DataBuffer);
        }

        protected override void CheckHasArrayDataBuffers()
        {
            CheckHasBuffer(VFXProperties.ArrayDataBuffer);
            CheckHasBuffer(VFXProperties.ArrayPtrBuffer);
        }

        public void SetTransformBuffer(UnsafeArray<VFXTransform> data, UploadRange uploadRange)
        {
            SetBuffer(_transformBuffer, data.AsNativeArray(), uploadRange, TransformMarker);
        }
        
        public void SetDataBuffer(UnsafeArray<byte> data, UploadRange uploadRange)
        {
            SetBuffer(_dataBuffer, data.AsNativeArray(), uploadRange.Expand(DataGpuSize), DataMarker);
        }
        
        public void SetIndexBuffer(UnsafeList<VFXSpawnIndex> data)
        {
            ResizeBuffer<VFXSpawnIndex>(Target, ref _spawnIndexBuffer, VFXProperties.SpawnIndexBuffer, data.Length);
            SetBuffer(_spawnIndexBuffer, data.AsNativeArray(), new UploadRange(0, data.Length), SpawnIndexMarker);
        }
        
        public void SetArrayDataBuffer(in UnsafeHeapMemory data, UnsafeArray<VFXArrayPtr> arrayPtrs, 
            UploadRange arrayDataRange, UploadRange ptrUploadRange)
        {
            var dataList = data.DataList;
            var arrayByteRange = arrayDataRange.Expand(ArrayDataGpuSize);
            
            ResizeArrayDataBuffer(arrayByteRange.EndIndex);
            SetBuffer(_arrayDataBuffer, dataList.AsNativeArray(), arrayByteRange, ArrayDataMarker);
            SetBuffer(_arrayPtrBuffer, arrayPtrs.AsNativeArray(), ptrUploadRange, ArrayPtrMarker);
        }
        
        private void ResizeArrayDataBuffer(int minDataCapacity)
        {
            ResizeBuffer(Target, ref _arrayDataBuffer, VFXProperties.ArrayDataBuffer, ArrayDataGpuSize, minDataCapacity);
        }
        
        public override void Dispose()
        {
            _spawnIndexBuffer.Dispose();
            _transformBuffer.Dispose();
            _dataBuffer?.Dispose();
            _arrayDataBuffer?.Dispose();
            _arrayPtrBuffer?.Dispose();
        }
    }
}
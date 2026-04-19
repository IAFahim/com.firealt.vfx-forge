using KrasCore;
using UnityEngine.VFX;

namespace FireAlt.VFXForge.Data
{
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct VFXArrayPtr
    {
        public uint StartIndex;
        public uint Count;
        
        public VFXArrayPtr(int startIndex, int count)
        {
            StartIndex = (uint)startIndex;
            Count = (uint)count;
        }

        public static implicit operator MemoryPtr(VFXArrayPtr arrayPtr)
        {
            return new MemoryPtr((int)arrayPtr.StartIndex, (int)arrayPtr.Count);
        }
        
        public static implicit operator VFXArrayPtr(MemoryPtr memoryPtr)
        {
            return new VFXArrayPtr(memoryPtr.StartIndex, memoryPtr.Count);
        }

        public override string ToString()
        {
            return $"[StartIndex: {StartIndex}, Count: {Count}]";
        }
    }
}
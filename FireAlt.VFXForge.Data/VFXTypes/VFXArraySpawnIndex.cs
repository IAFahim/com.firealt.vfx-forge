using UnityEngine.VFX;

namespace FireAlt.VFXForge.Data
{
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct VFXArraySpawnIndex
    {
        public uint IndexInData;
        public uint IndexInArray;
        
        public VFXArraySpawnIndex(uint indexInData, uint indexInArray)
        {
            IndexInData = indexInData;
            IndexInArray = indexInArray;
        }
        
        public override string ToString()
        {
            return $"[IndexInData: {IndexInData}, IndexInArray: {IndexInArray}]";
        }
    }
}
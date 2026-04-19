using Unity.Mathematics;

namespace FireAlt.VFXForge
{
    public struct UploadRange
    {
        public int StartIndex;
        public int EndIndex;

        public int Count => IsValid() ? EndIndex - StartIndex + 1 : 0;

        public UploadRange(int2 indexRange)
        {
            StartIndex = indexRange.x;
            EndIndex = indexRange.y;
        }
        
        public UploadRange(int startIndex, int count)
        {
            StartIndex = startIndex;
            EndIndex = startIndex + count - 1;
        }
        
        public bool IsValid() => StartIndex != int.MaxValue && EndIndex != int.MinValue;

        public void Encapsulate(UploadRange range)
        {
            StartIndex = math.min(StartIndex, range.StartIndex);
            EndIndex = math.max(EndIndex, range.EndIndex);
        }
        
        public void Encapsulate(int index)
        {
            if (index < StartIndex)
            {
                StartIndex = index;
            }
            if (index > EndIndex)
            {
                EndIndex = index;
            }
        }

        public UploadRange Expand(int stride)
        {
            return new UploadRange(StartIndex * stride, Count * stride);
        }
        
        public void Reset()
        {
            StartIndex = int.MaxValue;
            EndIndex = int.MinValue;
        }
    }
}
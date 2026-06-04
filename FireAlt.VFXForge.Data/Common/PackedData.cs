using System;
using System.Runtime.CompilerServices;

namespace FireAlt.VFXForge.Data
{
    public struct PackedData : IEquatable<PackedData>
    {
        private const int VALUE_BITS = 30;

        private const uint SYSTEM_VERSION_MASK = (1u << VALUE_BITS) - 1u; // 0x3FFFFFFF
        private const uint IS_DEFERRED_MASK = 1u << 30;               // 0x40000000
        private const uint IS_ENTITY_ID_MASK = 1u << 31;               // 0x80000000

        public const uint MAX_VALUE = SYSTEM_VERSION_MASK;

        private uint _data;

        public uint Raw => _data;

        public uint SystemVersion => _data & SYSTEM_VERSION_MASK;

        public bool IsDeferred => (_data & IS_DEFERRED_MASK) != 0;

        public bool IsEntityId => (_data & IS_ENTITY_ID_MASK) != 0;

        public PackedData(uint systemVersion, bool isDeferred, bool isEntityId)
        {
            _data = systemVersion & SYSTEM_VERSION_MASK;

            if (isDeferred)
                _data |= IS_DEFERRED_MASK;

            if (isEntityId)
                _data |= IS_ENTITY_ID_MASK;
        }

        private PackedData(uint raw)
        {
            _data = raw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSystemVersion(uint value)
        {
            var flags = _data & ~SYSTEM_VERSION_MASK;
            _data = flags | (value & SYSTEM_VERSION_MASK);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetIsDeferred(bool isDeferred)
        {
            _data = isDeferred
                ? _data | IS_DEFERRED_MASK
                : _data & ~IS_DEFERRED_MASK;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetIsEntityId(bool isEntityId)
        {
            _data = isEntityId
                ? _data | IS_ENTITY_ID_MASK
                : _data & ~IS_ENTITY_ID_MASK;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(PackedData other)
        {
            return _data == other._data;
        }

        public override int GetHashCode()
        {
            return _data.GetHashCode();
        }

        public override string ToString()
        {
            return $"PackedData(Value: {SystemVersion}, IsDeferred: {IsDeferred}, IsEntityId: {IsEntityId}, Raw: 0x{_data:X8})";
        }
    }
}
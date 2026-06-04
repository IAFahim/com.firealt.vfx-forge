using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace FireAlt.VFXForge.Data
{
    public static class EntityIdConverter
    {
        public static EntityId FromEntity(Entity entity)
        {
#if UNITY_6000_5_OR_NEWER
            return (EntityId)entity;
#else
            Assert.IsTrue(UnsafeUtility.SizeOf<EntityId>() == sizeof(int));
            return EntityId.FromULong(UnsafeUtility.As<Entity, ulong>(ref entity));
#endif
        }
        
        public static Entity FromEntityId(EntityId entityId)
        {
#if UNITY_6000_5_OR_NEWER
            return UnsafeUtility.As<EntityId, Entity>(ref entityId);
#else
            Assert.IsTrue(UnsafeUtility.SizeOf<EntityId>() == sizeof(int));
            SplitUlong(EntityId.ToULong(entityId), out var index, out var version);
            return new Entity { Index = index, Version = version };
#endif
        }
        
        private static void SplitUlong(ulong value, out int low, out int high)
        {
            low = unchecked((int)(value & 0xFFFFFFFFUL));
            high = unchecked((int)(value >> 32));
        }
    }
}


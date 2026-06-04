using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FireAlt.VFXForge.Data
{
    public struct TrackedEntity : IEquatable<TrackedEntity>
    {
        public Entity Entity;
        public int IndexInData;
        public int SystemVersion;

        public EntityId EntityId => EntityIdConverter.FromEntity(Entity);
        
        public TrackedEntity(Entity entity, int indexInData, int systemVersion)
        {
            Entity = entity;
            IndexInData = indexInData;
            SystemVersion = systemVersion;
        }

        public bool IsValid => SystemVersion != 0;
        public bool IsDeferred(int currentSystemVersion) => SystemVersion == -currentSystemVersion;
        
        public static TrackedEntity Null => new(Entity.Null, 0, 0);

        public bool Equals(TrackedEntity other)
        {
            return Entity.Equals(other.Entity)
                   && IndexInData.Equals(other.IndexInData)
                   && SystemVersion.Equals(other.SystemVersion);
        }

        public override int GetHashCode()
        {
            return (int)math.hash(new int4(Entity.Index, Entity.Version, IndexInData, SystemVersion));
        }

        public override string ToString()
        {
            return $"{Entity}:I={IndexInData}:SysVer={SystemVersion}";
        }
    }
}
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FireAlt.VFXForge.Data
{
    public struct RuntimeDecalLookup : IComponentData, IEnableableComponent
    {
        public DecalLookup Value;
    }
    
    public struct DecalLookup : IEquatable<DecalLookup>
    {
        public UnityObjectRef<VFXDefinition> Definition;
        public UnityObjectRef<Sprite> Sprite;
        
        // Used for comparison only
        private UnityObjectRef<Texture> _spriteTexture;
        
        public DecalLookup(VFXDefinition definition, Sprite sprite)
        {
            Definition = definition;
            Sprite = sprite;
            _spriteTexture = sprite.texture;
        }
        
        public bool Equals(DecalLookup other)
        {
            return Definition.Equals(other.Definition) && _spriteTexture.Equals(other._spriteTexture);
        }

        public override int GetHashCode()
        {
            return (int)math.hash(new int2(Definition.GetHashCode(), _spriteTexture.GetHashCode()));
        }
    }
}
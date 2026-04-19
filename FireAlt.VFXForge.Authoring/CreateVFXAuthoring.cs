using BovineLabs.Core.Authoring.LifeCycle;
using BovineLabs.Reaction.Authoring.Core;
using FireAlt.VFXForge.Data;
using Unity.Entities;
using UnityEngine;

namespace FireAlt.VFXForge.Authoring
{
    [RequireComponent(typeof(TargetsAuthoring), typeof(LifeCycleAuthoring))]
    public class CreateVFXAuthoring : MonoBehaviour
    {
        public VFXDefinition VFXDefinition;
        
        private class Baker : Baker<CreateVFXAuthoring>
        {
            public override void Bake(CreateVFXAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CreateVFXData
                {
                    Key = authoring.VFXDefinition
                });
            }
        }
    }
}
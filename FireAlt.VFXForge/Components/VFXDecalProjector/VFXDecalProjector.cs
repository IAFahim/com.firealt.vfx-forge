using BovineLabs.Core.EntityCommands;
using BovineLabs.Core.PropertyDrawers;
using FireAlt.VFXForge.Data;
using KrasCore;
using KrasCore.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;

namespace FireAlt.VFXForge
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)] // Needed for Start to be a valid place to use World
    public class VFXDecalProjector : MonoBehaviour
    {
        [Header("References")]
        public VFXDefinition VFXDecalDefinition;
        
        [Header("Data")]
        [FormerlySerializedAs("sprite")]
        [SerializeField] private Sprite _sprite;
        
        public float projectionDepth = 1f;
        public float projectionDepthPivot = 0.5f;
        [Range(0f, 1f)] public float opacity = 1f;
        public float drawDistance = 1000f;
        [Range(0f, 1f)] public float startFade = 0.9f;
        [MinMax(0f, 180f)] public Vector2 angleFade;
        [Range(0f, 1f)] public float normalBlend;
        
        private float3 _decalSize;
        private float3 _decalPivot;
        
        private Sprite _curSprite;
        private Sprite _oldSprite;
        
        private static World World => World.DefaultGameObjectInjectionWorld;
        private Entity _entity;
        
        public Sprite Sprite
        {
            get => _sprite;
            set
            {
                SetSprite(value);
                BakeComponents(_curSprite != _oldSprite || value == null);
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            VFXDecalDefinition = Authoring.VFXSettings.DefaultDecalVFX;
            Sprite = null;
        }
#endif

        private void SetSprite(Sprite sprite)
        {
            _sprite = sprite;
            _oldSprite = _curSprite;
            _curSprite = sprite;
            var spriteProps = new SpriteProperties(_sprite);
            _decalSize = new float3(spriteProps.rectScale.x, spriteProps.rectScale.y, projectionDepth);
            
            var spritePivot = spriteProps.rectScale * (0.5f - spriteProps.normalizedPivot);
            _decalPivot = new float3(spritePivot.x, spritePivot.y, projectionDepth * projectionDepthPivot);
        }
        
        private void OnValidate()
        {
            drawDistance = math.max(drawDistance, 0f);
            projectionDepth = math.max(projectionDepth, 0f);
            Sprite = _sprite;
        }
        
        private void Start()
        {
            if (Application.isPlaying)
            {
                Init();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                Init();
            }
        }
        
        private void OnDestroy()
        {
            if (World != null)
            {
                World.EntityManager.DestroyEntity(_entity);
            }
        }
        
        private void Init()
        {
#if UNITY_EDITOR
            DefaultWorldInitialization.DefaultLazyEditModeInitialize();
#endif
            
            if (World != null && _entity == Entity.Null)
            {
                Debug.Log("Bale");
                _entity = World.EntityManager.CreateEntity(typeof(LocalToWorld));
                World.EntityManager.AddComponentObject(_entity, new HybridEntitySync(this));
                BakeComponents(true);
            }
        }

        private void OnDrawGizmosSelected()
        {
            var globalCenter = (float3)transform.position + transform.rotation * _decalPivot * (float3)transform.lossyScale;
            var globalSize = _decalSize * transform.lossyScale;
            Gizmos.color = Color.white;
            GizmosEx.DrawWireCuboid(globalCenter, transform.rotation, globalSize);
        }
        
        private void BakeComponents(bool resetDecal)
        {
            if (World != null && World.EntityManager.HasComponent<HybridEntitySync>(_entity))
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                var commands = new CommandBufferCommands(ecb, _entity);

                SetupDecalProjector(ref commands, this, _entity, resetDecal);
                ecb.Playback(World.EntityManager);
            }
        }
        
        private static void SetupDecalProjector<T>(ref T commands, VFXDecalProjector authoring, Entity entity, bool resetDecal)
            where T : IEntityCommands
        {
            if (authoring.VFXDecalDefinition == null) return;
            
            commands.AddComponent(entity, new DecalProjectorData
            {
                SpriteProperties = new SpriteProperties(authoring._sprite),
                NormalBlend = authoring.normalBlend,
                AngleFade =  authoring.angleFade,
                StartFade = authoring.startFade,
                DrawDistance = authoring.drawDistance,
                Opacity = authoring.opacity,
                ProjectionDepth =  authoring.projectionDepth,
                ProjectionDepthPivot = authoring.projectionDepthPivot,
            });
            
            if (resetDecal)
            {
                var lookup = new DecalLookup(authoring.VFXDecalDefinition, authoring._sprite);
                commands.AddComponent(entity, new RuntimeDecalLookup { Value = lookup });
                commands.SetComponentEnabled<RuntimeDecalLookup>(entity, true);
                
                commands.AddComponent<DecalProjectorVFX>(entity);
            }
        }
        
#if UNITY_EDITOR
        private class HybridDecalProjectorBaker : Baker<VFXDecalProjector>
        {
            public override void Bake(VFXDecalProjector authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);

                var commands = new BovineLabs.Core.Authoring.EntityCommands.BakerCommands(this, entity);
                SetupDecalProjector(ref commands, authoring, entity, true);
            }
        }
#endif
    }
}

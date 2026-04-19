using ArtificeToolkit.Attributes;
using BovineLabs.Core.Extensions;
using UnityEngine;
using UnityEngine.VFX;
using Unity.Entities;
using Unity.Transforms;
using FireAlt.VFXForge.Data;

namespace FireAlt.VFXForge
{
    [ExecuteAlways]
    [RequireComponent(typeof(VisualEffect))]
    [DefaultExecutionOrder(-100)] // Needed for Editor World initialization to take place
    public partial class HybridVisualEffect : MonoBehaviour
    {
        private static readonly int BoundsProperty = Shader.PropertyToID("Bounds");
        private const int BOUNDS_SIZE = 1_000_000;
        
        [SerializeField, HideInInspector]
        private VisualEffect _visualEffect;

        [SerializeField, InlineObject(false), OnValueChanged("RefreshDataAndReinit")]
        private VFXDefinition _vfxDefinition;
        
        private Entity _entity;
        
        public VFXDefinition VFXDefinition
        {
            get => _vfxDefinition;
            set => _vfxDefinition = value;
        }
        public VisualEffect VisualEffect => _visualEffect;

        private static World World => World.DefaultGameObjectInjectionWorld;
        
        private void Start()
        {
            if (Application.isPlaying)
            {
                Init();
            }
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                Cleanup();
            }
        }

        internal void SetVFXActive(bool isActive)
        {
            VisualEffect.gameObject.SetActive(isActive);
        }

        public void Init()
        {
#if UNITY_EDITOR
            DefaultWorldInitialization.DefaultLazyEditModeInitialize();
            _singleton = World.EntityManager.GetSingleton<VFXSingleton>();
#endif
            if (Application.isPlaying)
            {
                transform.position = Vector3.zero;
                transform.rotation =  Quaternion.identity;
                transform.localScale = Vector3.one;
            }
            
            if (VFXDefinition == null || _entity != Entity.Null) return;
            ValidateVFXGraph();
            RegisterVFX();
        }

        public void Cleanup()
        {
            DeregisterVFX();
        }

        private void ValidateVFXGraph()
        {
            if (_visualEffect == null) _visualEffect = GetComponent<VisualEffect>();

            if (_vfxDefinition != null)
            {
                if (_visualEffect.visualEffectAsset != _vfxDefinition.visualEffectAsset)
                    _visualEffect.visualEffectAsset = _vfxDefinition.visualEffectAsset;
                
                if (_visualEffect.HasVector3(BoundsProperty))
                   _visualEffect.SetVector3(BoundsProperty, BOUNDS_SIZE * Vector3.one);
                else
                    Debug.LogError($"{gameObject} Missing <name: Bounds, type: Vector3> exposed property");
            }
        }

        private void RegisterVFX()
        {
            if (_entity != Entity.Null || World == null) return;

            var em = World.EntityManager;
            _entity = em.CreateEntity(typeof(LocalToWorld), typeof(RegisteredVFX));
            em.AddComponentData(_entity, new HybridVisualEffectData { HybridVisualEffect = this });
            em.SetComponentEnabled<HybridVisualEffectData>(_entity, true);
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                em.AddComponentObject(_entity, new KrasCore.Editor.HybridEntitySync(this));
            }
#endif
            World.GetExistingSystemManaged<InitializeVFXSystem>().Update();
        }

        private void DeregisterVFX()
        {
            if (_entity == Entity.Null || World == null) return;
            World.EntityManager.DestroyEntity(_entity);
            _entity = Entity.Null;
            _visualEffect.Reinit();
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                try
                {
                    World.GetExistingSystemManaged<CleanupVFXSystem>().Update();
                }
                catch
                {
                    // May fail during domain reload
                }

                _trackedEntity = TrackedEntity.Null;
            }
#endif
        }
    }
}

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine.Rendering;

namespace DefaultNamespace
{
    public class PurScene : MonoBehaviour
    {
        [SerializeField] private Mesh unitMesh;
        [SerializeField] private Material unitMaterial;
        [SerializeField] private int EntityCount;

        [BurstCompile]
        public struct SpawnJob : IJobParallelFor
        {
            public Entity Prototype;
            public int EntityCount;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute(int index)
            {
                // Clone the Prototype entity to create a new entity.
                var e = Ecb.Instantiate(index, Prototype);
                // Prototype has all correct components up front, can use SetComponent to
                // set values unique to the newly created entity, such as the transform.
                Ecb.SetComponent(index, e, new LocalToWorld {Value = ComputeTransform(index)});
            }

            public float4x4 ComputeTransform(int index)
            {
                return float4x4.Translate(new float3(index, 0, 0));
            }
        }

        private void UseJob()
        {
            EntityManager entityManager
                = World.DefaultGameObjectInjectionWorld.EntityManager;
            var desc = new RenderMeshDescription(unitMesh, unitMaterial);
            var entity = entityManager.CreateEntity();
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            RenderMeshUtility.AddComponents(entity, ecb, desc);
            entityManager.AddComponentData(entity, new LocalToWorld());
            var job = new SpawnJob()
            {
                Ecb = ecb.AsParallelWriter(),
                Prototype = entity,
                EntityCount = EntityCount
            };
            var handler = job.Schedule(EntityCount, 128);
            handler.Complete();
            ecb.Playback(entityManager);
            ecb.Dispose();
            entityManager.DestroyEntity(entity);
        }

        private void UseOld()
        {
            EntityManager entityManager
                = World.DefaultGameObjectInjectionWorld.EntityManager;
            var archetype = entityManager.CreateArchetype(
                typeof(Translation),
                typeof(RenderMesh),
                typeof(RenderBounds),
                typeof(LocalToWorld));

            NativeArray<Entity> entities = new NativeArray<Entity>(EntityCount, Allocator.Temp);
            entityManager.CreateEntity(archetype, entities);
            for (int i = 0; i < EntityCount; i++)
            {
                var entity = entities[i];
                entityManager.SetComponentData(entity, new RenderBounds()
                {
                    Value = new AABB() {Center = float3.zero, Extents = new float3(0.5f, 0.5f, 0.5f)}
                });
                entityManager.SetComponentData(entity, new Translation()
                {
                    Value = new float3(i, 0, 0f)
                });
                entityManager.SetSharedComponentData(entity, new RenderMesh()
                {
                    material = unitMaterial,
                    mesh = unitMesh,
                    subMesh = 0,
                    castShadows = ShadowCastingMode.Off,
                    receiveShadows = false,
                    layerMask = 1
                });
            }

            entities.Dispose();
        }

        private void Start()
        {
            UseOld();
        }
    }
}
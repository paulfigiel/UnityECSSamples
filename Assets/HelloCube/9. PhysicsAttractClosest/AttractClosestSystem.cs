using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class AttractClosestSystem : SystemBase
{
    BuildPhysicsWorld m_BuildPhysicsWorld;
    EndFramePhysicsSystem m_EndFramePhysicsSystem;
    EntityCommandBufferSystem m_EntityCommandBufferSystem;


    protected override void OnCreate()
    {
        m_BuildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        m_EndFramePhysicsSystem = World.GetOrCreateSystem<EndFramePhysicsSystem>();
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected unsafe override void OnUpdate()
    {
        CollisionWorld collisionWorld = m_BuildPhysicsWorld.PhysicsWorld.CollisionWorld;
        EntityCommandBuffer commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer();
        Dependency = JobHandle.CombineDependencies(Dependency, m_EndFramePhysicsSystem.GetOutputDependency());

        ComponentDataFromEntity<PhysicsVelocity> physicsVelocities = GetComponentDataFromEntity<PhysicsVelocity>(false);
        ComponentDataFromEntity<LocalToWorld> localToWorlds = GetComponentDataFromEntity<LocalToWorld>(false);

        Entities
            .WithName("TagClosestBodySystem")
            .WithBurst()
            .ForEach((Entity entity, in Translation position, in AttractClosestComponent component) =>
        {
            BlobAssetReference<Unity.Physics.Collider> sphere = default;
            sphere = Unity.Physics.SphereCollider.Create(new SphereGeometry
            {
                Center = float3.zero,
                Radius = component.radius
            }, CollisionFilter.Default);
            var input = new ColliderCastInput
            {
                Collider = (Unity.Physics.Collider*)sphere.GetUnsafePtr(),
                Orientation = quaternion.identity,
                Start = position.Value,
                End = position.Value
            };
            bool hasHit = collisionWorld.CastCollider(input, out ColliderCastHit colliderHit);

            if (hasHit && physicsVelocities.HasComponent(colliderHit.Entity))
            {
                physicsVelocities[colliderHit.Entity] = new PhysicsVelocity()
                {
                    Angular = physicsVelocities[colliderHit.Entity].Angular,
                    Linear = physicsVelocities[colliderHit.Entity].Linear + math.normalize(position.Value - localToWorlds[colliderHit.Entity].Position) * component.strenght
                };
            }
            sphere.Dispose();
        }).Schedule();

        m_EntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}

﻿using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Physics.Math;

public class RagdollDemo : BasePhysicsDemo
{
    public UnityEngine.Mesh torsoMesh;
    public int numberOfRagdolls = 1;

    private enum layer
    {
        Torso = (1 << 0),
        Head = (1 << 1),
        Pelvis = (1 << 2),
        UpperArm = (1 << 3),
        Forearm = (1 << 4),
        Hand = (1 << 5),
        Thigh = (1 << 6),
        Calf = (1 << 7),
        Foot = (1 << 8),
        Ground = (1 << 9)
    };

    private static CollisionFilter layerFilter(layer layer, layer disabled)
    {
        return new CollisionFilter
        {
            BelongsTo = (uint)layer,
            CollidesWith = ~(uint)disabled,
            GroupIndex = 0
        };
    }
    private static CollisionFilter groupFilter(int groupIndex)
    {
        return new CollisionFilter
        {
            BelongsTo = 4294967295,
            CollidesWith = 4294967295,
            GroupIndex = groupIndex
        };
    }

    private void CreateRagdoll(float3 positionOffset, quaternion rotationOffset, int ragdollIndex = 1, bool internalCollisions = false)
    {
        var entities = new NativeList<Entity>(Allocator.Temp);

        // Head
        float headRadius = 0.1f;
        float3 headPosition = new float3(0, 1.8f, 0);
        Entity head;
        {
            CollisionFilter filter = internalCollisions ? layerFilter(layer.Head, layer.Torso) : groupFilter(-ragdollIndex);
            BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.SphereCollider.Create(new SphereGeometry
            {
                Center = float3.zero,
                Radius = headRadius
            }, filter);
            head = CreateDynamicBody(headPosition, quaternion.identity, collider, float3.zero, float3.zero, 5.0f);
        }
        entities.Add(head);

        // Torso
        float3 torsoSize;
        float3 torsoPosition;
        Entity torso;
        {
            //UnityEngine.Mesh torsoMesh = (UnityEngine.Mesh)Resources.Load("torso", typeof(UnityEngine.Mesh));
            torsoSize = torsoMesh.bounds.size;
            torsoPosition = headPosition - new float3(0, headRadius * 3.0f / 4.0f + torsoSize.y, 0);

            CollisionFilter filter = internalCollisions ? layerFilter(layer.Torso, layer.Thigh | layer.Head | layer.UpperArm | layer.Pelvis) : groupFilter(-ragdollIndex);

            NativeArray<float3> points = new NativeArray<float3>(torsoMesh.vertices.Length, Allocator.TempJob);
            for (int i = 0; i < torsoMesh.vertices.Length; i++)
            {
                points[i] = torsoMesh.vertices[i];
            }
            BlobAssetReference<Unity.Physics.Collider> collider = ConvexCollider.Create(
                points, ConvexHullGenerationParameters.Default, CollisionFilter.Default
            );
            points.Dispose();
            collider.Value.Filter = filter;
            torso = CreateDynamicBody(torsoPosition, quaternion.identity, collider, float3.zero, float3.zero, 20.0f);
        }
        entities.Add(torso);

        // Neck
        {
            float3 pivotHead = new float3(0, -headRadius, 0);
            float3 pivotBody = math.transform(math.inverse(GetBodyTransform(torso)), math.transform(GetBodyTransform(head), pivotHead));
            float3 axis = new float3(0, 1, 0);
            float3 perpendicular = new float3(0, 0, 1);
            float coneAngle = (float)math.PI / 5.0f;
            var perpendicularAngle = new FloatRange { Max = math.PI }; // unlimited
            var twistAngle = new FloatRange(-math.PI / 3f, math.PI / 3f);

            var headFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotHead };
            var bodyFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotBody };
            PhysicsJoint.CreateRagdoll(headFrame, bodyFrame, coneAngle, perpendicularAngle, twistAngle, out var ragdoll0, out var ragdoll1);
            CreateJoint(ragdoll0, head, torso);
            CreateJoint(ragdoll1, head, torso);
        }

        // Arms
        {
            float armLength = 0.25f;
            float armRadius = 0.05f;
            CollisionFilter armUpperFilter = internalCollisions ? layerFilter(layer.UpperArm, layer.Torso | layer.Forearm) : groupFilter(-ragdollIndex);
            CollisionFilter armLowerFilter = internalCollisions ? layerFilter(layer.Forearm, layer.UpperArm | layer.Hand) : groupFilter(-ragdollIndex);

            BlobAssetReference<Unity.Physics.Collider> upperArmCollider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(-armLength / 2, 0, 0),
                Vertex1 = new float3(armLength / 2, 0, 0),
                Radius = armRadius
            }, armUpperFilter);
            BlobAssetReference<Unity.Physics.Collider> foreArmCollider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(-armLength / 2, 0, 0),
                Vertex1 = new float3(armLength / 2, 0, 0),
                Radius = armRadius
            }, armLowerFilter);

            float handLength = 0.025f;
            float handRadius = 0.055f;
            CollisionFilter handFilter = internalCollisions ? layerFilter(layer.Hand, layer.Forearm) : groupFilter(-ragdollIndex);

            BlobAssetReference<Unity.Physics.Collider> handCollider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(-handLength / 2, 0, 0),
                Vertex1 = new float3(handLength / 2, 0, 0),
                Radius = handRadius
            }, handFilter);

            for (int i = 0; i < 2; i++)
            {
                float s = i * 2 - 1.0f;

                float3 upperArmPosition = torsoPosition + new float3(s * (torsoSize.x + armLength) / 2.0f, 0.9f * torsoSize.y - armRadius, 0.0f);
                Entity upperArm = CreateDynamicBody(upperArmPosition, quaternion.identity, upperArmCollider, float3.zero, float3.zero, 10.0f);
                float3 foreArmPosition = upperArmPosition + new float3(armLength * s, 0, 0);
                Entity foreArm = CreateDynamicBody(foreArmPosition, quaternion.identity, foreArmCollider, float3.zero, float3.zero, 5.0f);
                float3 handPosition = foreArmPosition + new float3((armLength + handLength) / 2.0f * s, 0, 0);
                Entity hand = CreateDynamicBody(handPosition, quaternion.identity, handCollider, float3.zero, float3.zero, 2.0f);

                entities.Add(upperArm);
                entities.Add(foreArm);
                entities.Add(hand);

                // shoulder
                {
                    float3 pivotArm = new float3(-s * armLength / 2.0f, 0, 0);
                    float3 pivotBody = math.transform(math.inverse(GetBodyTransform(torso)), math.transform(GetBodyTransform(upperArm), pivotArm));
                    float3 axis = new float3(s, 0, 0);
                    float3 perpendicular = new float3(0, 0, 1);
                    float coneAngle = (float)math.PI / 2.0f;
                    var perpendicularAngle = new FloatRange { Max = math.PI / 2f };
                    var twistAngle = new FloatRange(-math.PI / 4f, math.PI / 4f);

                    var armFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotArm };
                    var bodyFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotBody };
                    PhysicsJoint.CreateRagdoll(armFrame, bodyFrame, coneAngle, perpendicularAngle, twistAngle, out var ragdoll0, out var ragdoll1);
                    CreateJoint(ragdoll0, upperArm, torso);
                    CreateJoint(ragdoll1, upperArm, torso);
                }

                // elbow
                {
                    float3 pivotUpper = new float3(s * armLength / 2.0f, 0, 0);
                    float3 pivotFore = -pivotUpper;
                    float3 axis = new float3(0, -s, 0);
                    float3 perpendicular = new float3(s, 0, 0);

                    var lowerArmFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotFore };
                    var upperArmFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotUpper };
                    PhysicsJoint hinge =
                        PhysicsJoint.CreateLimitedHinge(lowerArmFrame, upperArmFrame, new FloatRange { Max = 3f });
                    CreateJoint(hinge, foreArm, upperArm);
                }

                // wrist
                {
                    float3 pivotFore = new float3(s * armLength / 2.0f, 0, 0);
                    float3 pivotHand = new float3(-s * handLength / 2.0f, 0, 0);
                    float3 axis = new float3(0, -s, 0);
                    float3 perpendicular = new float3(s, 0, 0);

                    var handFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotHand };
                    var forearmFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotFore };
                    PhysicsJoint hinge =
                        PhysicsJoint.CreateLimitedHinge(handFrame, forearmFrame, new FloatRange(-0.3f, 0.6f));
                    CreateJoint(hinge, hand, foreArm);
                }
            }
        }

        // Pelvis
        float pelvisRadius = 0.08f;
        float pelvisLength = 0.22f;
        float3 pelvisPosition = torsoPosition - new float3(0, pelvisRadius * 0.75f, 0.0f);
        Entity pelvis;
        {
            CollisionFilter filter = internalCollisions ? layerFilter(layer.Pelvis, layer.Torso | layer.Thigh) : groupFilter(-ragdollIndex);
            BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(-pelvisLength / 2, 0, 0),
                Vertex1 = new float3(pelvisLength / 2, 0, 0),
                Radius = pelvisRadius
            }, filter);
            pelvis = CreateDynamicBody(pelvisPosition, quaternion.identity, collider, float3.zero, float3.zero, 15.0f);
        }
        entities.Add(pelvis);

        // Waist
        {
            float3 pivotTorso = float3.zero;
            float3 pivotPelvis = math.transform(math.inverse(GetBodyTransform(pelvis)), math.transform(GetBodyTransform(torso), pivotTorso));
            float3 axis = new float3(0, -1, 0);
            float3 perpendicular = new float3(0, 0, 1);
            float coneAngle = 0.1f;
            var perpendicularAngle = new FloatRange(-0.1f, math.PI);
            var twistAngle = new FloatRange(-0.1f, 0.1f);

            var pelvisFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotPelvis };
            var torsoFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotTorso };
            PhysicsJoint.CreateRagdoll(pelvisFrame, torsoFrame, coneAngle, perpendicularAngle, twistAngle, out var ragdoll0, out var ragdoll1);
            CreateJoint(ragdoll0, pelvis, torso);
            CreateJoint(ragdoll1, pelvis, torso);
        }

        // Legs
        {
            float thighLength = 0.32f;
            float thighRadius = 0.08f;
            CollisionFilter thighFilter = internalCollisions ? layerFilter(layer.Thigh, layer.Pelvis | layer.Calf) : groupFilter(-ragdollIndex);
            BlobAssetReference<Unity.Physics.Collider> thighCollider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(0, -thighLength / 2, 0),
                Vertex1 = new float3(0, thighLength / 2, 0),
                Radius = thighRadius
            }, thighFilter);

            float calfLength = 0.32f;
            float calfRadius = 0.06f;
            CollisionFilter calfFilter = internalCollisions ? layerFilter(layer.Calf, layer.Thigh | layer.Foot) : groupFilter(-ragdollIndex);
            BlobAssetReference<Unity.Physics.Collider> calfCollider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(0, -calfLength / 2, 0),
                Vertex1 = new float3(0, calfLength / 2, 0),
                Radius = calfRadius
            }, calfFilter);

            float footLength = 0.08f;
            float footRadius = 0.06f;
            CollisionFilter footFilter = internalCollisions ? layerFilter(layer.Foot, layer.Calf) : groupFilter(-ragdollIndex);
            BlobAssetReference<Unity.Physics.Collider> footCollider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(0),
                Vertex1 = new float3(0, 0, footLength),
                Radius = footRadius
            }, footFilter);

            for (int i = 0; i < 2; i++)
            {
                float s = i * 2 - 1.0f;

                float3 thighPosition = pelvisPosition + new float3(s * pelvisLength / 2.0f, -thighLength / 2.0f, 0.0f);
                Entity thigh = CreateDynamicBody(thighPosition, quaternion.identity, thighCollider, float3.zero, float3.zero, 10.0f);
                float3 calfPosition = thighPosition + new float3(0, -(thighLength + calfLength) / 2.0f, 0);
                Entity calf = CreateDynamicBody(calfPosition, quaternion.identity, calfCollider, float3.zero, float3.zero, 5.0f);
                float3 footPosition = calfPosition + new float3(0, -calfLength / 2.0f, 0);
                Entity foot = CreateDynamicBody(footPosition, quaternion.identity, footCollider, float3.zero, float3.zero, 2.0f);

                entities.Add(thigh);
                entities.Add(calf);
                entities.Add(foot);

                // hip
                {
                    float3 pivotThigh = new float3(0, thighLength / 2.0f, 0);
                    float3 pivotBody = math.transform(math.inverse(GetBodyTransform(torso)), math.transform(GetBodyTransform(thigh), pivotThigh));
                    float3 axis = new float3(0, -1, 0);
                    float3 perpendicular = new float3(s, 0, 0);
                    float coneAngle = (float)math.PI / 4.0f;

                    var perpendicularAngle = new FloatRange { Max = 0.2f + math.PI / 2.0f };
                    var twistAngle = new FloatRange(-0.2f, 0.2f);

                    var upperLegFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotThigh };
                    var bodyFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotBody };
                    PhysicsJoint.CreateRagdoll(upperLegFrame, bodyFrame, coneAngle, perpendicularAngle, twistAngle, out var ragdoll0, out var ragdoll1);
                    CreateJoint(ragdoll0, thigh, torso);
                    CreateJoint(ragdoll1, thigh, torso);
                }

                // knee
                {
                    float3 pivotThigh = new float3(0, -thighLength / 2.0f, 0);
                    float3 pivotCalf = math.transform(math.inverse(GetBodyTransform(calf)), math.transform(GetBodyTransform(thigh), pivotThigh));
                    float3 axis = new float3(-1, 0, 0);
                    float3 perpendicular = new float3(0, 0, 1);

                    var lowerLegFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotCalf };
                    var upperLegFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotThigh };
                    PhysicsJoint hinge =
                        PhysicsJoint.CreateLimitedHinge(lowerLegFrame, upperLegFrame, new FloatRange { Min = -1.2f });
                    CreateJoint(hinge, calf, thigh);
                }

                // ankle
                {
                    float3 pivotCalf = new float3(0, -calfLength / 2.0f, 0);
                    float3 pivotFoot = float3.zero;
                    float3 axis = new float3(-1, 0, 0);
                    float3 perpendicular = new float3(0, 0, 1);

                    var footFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotFoot };
                    var lowerLegFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicular, Position = pivotCalf };
                    PhysicsJoint hinge =
                        PhysicsJoint.CreateLimitedHinge(footFrame, lowerLegFrame, new FloatRange(-0.4f, 0.1f));
                    CreateJoint(hinge, foot, calf);
                }
            }
        }

        var entityManager = BasePhysicsDemo.DefaultWorld.EntityManager;

        // reposition with offset information
        if (entities.Length > 0)
        {
            float3 center = float3.zero;
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                center += entityManager.GetComponentData<Translation>(e).Value;
            }
            center /= entities.Length;
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                Translation positionComponent = entityManager.GetComponentData<Translation>(e);
                Rotation rotationComponent = entityManager.GetComponentData<Rotation>(e);

                float3 position = positionComponent.Value;
                quaternion rotation = rotationComponent.Value;

                float3 localPosition = position - center;
                localPosition = math.rotate(rotationOffset, localPosition);

                position = localPosition + center + positionOffset;
                rotation = math.mul(rotation, rotationOffset);

                positionComponent.Value = position;
                rotationComponent.Value = rotation;

                entityManager.SetComponentData<Translation>(e, positionComponent);
                entityManager.SetComponentData<Rotation>(e, rotationComponent);
            }
        }
    }

    protected override void Start()
    {
        init(new float3(0, -9.81f, 0));
        //base.init(float3.zero); // no gravity

        // Enable the joint viewer
        //         SetDebugDisplay(new Unity.Physics.Authoring.PhysicsDebugDisplayComponentData
        //         {
        //             DrawJoints = 1
        //         });

        // Floor
        {
            BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.BoxCollider.Create(
                new BoxGeometry
                {
                    Center = float3.zero,
                    Orientation = quaternion.identity,
                    Size = new float3(20.0f, 0.2f, 20.0f),
                    BevelRadius = 0.01f
                },
                layerFilter(layer.Ground, 0)
            );
            CreateStaticBody(new float3(0, -0.1f, 0), quaternion.identity, collider);
        }

        for (int i = 0; i < numberOfRagdolls; i++)
        {
            CreateRagdoll(new float3(0, i, 0), quaternion.Euler(math.radians(90), math.radians(90), 0), i + 1);
        }
    }
}
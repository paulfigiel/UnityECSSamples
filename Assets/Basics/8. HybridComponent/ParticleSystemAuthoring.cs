using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleSystemAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        conversionSystem.AddHybridComponent(GetComponent<ParticleSystem>());
        conversionSystem.AddHybridComponent(GetComponent<ParticleSystemRenderer>());
    }
}

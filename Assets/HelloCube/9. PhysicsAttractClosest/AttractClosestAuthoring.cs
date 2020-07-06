using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class AttractClosestAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float Radius = 10;
    public float Strenght = 1;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData<AttractClosestComponent>(entity, new AttractClosestComponent() { strenght = Strenght, radius = Radius });
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}

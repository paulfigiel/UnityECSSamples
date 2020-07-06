using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct AttractClosestComponent : IComponentData
{
    public float strenght;
    public float radius;
}

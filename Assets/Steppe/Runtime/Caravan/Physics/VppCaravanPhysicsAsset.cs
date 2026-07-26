using UnityEngine;

namespace Steppe.Caravan
{
    public sealed class VppCaravanPhysicsAsset : ScriptableObject
    {
        [SerializeField] private GameObject physicsPrefab;

        public GameObject PhysicsPrefab => physicsPrefab;
    }
}

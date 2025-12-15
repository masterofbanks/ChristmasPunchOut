using UnityEngine;

public class PlayerHitbox : MonoBehaviour
{
    private CharacterStats cStats;

    private void Awake()
    {
        cStats = GetComponentInParent<CharacterStats>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out BaseEnemy enemy))
        {
            enemy.ApplyAHit(cStats.AttackDamage);
        }
    }
}

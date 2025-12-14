using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    [SerializeField] private float damage = 10f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerHurtbox player))
        {
            player.ApplyAHit(damage);
        }
    }
}

using UnityEngine;

public class PlayerHitbox : MonoBehaviour
{
    [SerializeField] private float damage = 10f;

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out BaseEnemy enemy))
        {
            enemy.ApplyAHit(damage);
        }
    }
}

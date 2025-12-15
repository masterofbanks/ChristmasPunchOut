using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    private BaseEnemy enemyScript;

    private void Awake()
    {
        enemyScript = GetComponentInParent<BaseEnemy>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerHurtbox player))
        {
            player.ApplyAHit(enemyScript.CurrentAttack.Damage);
        }
    }
}

using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class TestHitBehavior : MonoBehaviour
{
    public LayerMask HitLayer;
    public float HitDuration = 0.5f;
    private SpriteRenderer spriteRenderer;
    private CharacterStats statsScript;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        statsScript = GetComponent<CharacterStats>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Hit"))
        {
            BaseEnemy enemyScript = other.gameObject.GetComponentInParent<BaseEnemy>();
            statsScript.DealDamage(enemyScript.CurrentAttack.Damage);
            StartCoroutine(FlashRed());
        }
    }

    IEnumerator FlashRed()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(HitDuration);
        spriteRenderer.color = Color.white;
    }
}

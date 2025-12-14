using System.Collections;
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Hit"))
        {
            BaseEnemy enemyScript = collision.gameObject.GetComponentInParent<BaseEnemy>();
            statsScript.DealDamage(enemyScript.AttackDamage);
            StartCoroutine(FlashRed());
        }
    }

    IEnumerator FlashRed()
    {
        Debug.Log("Trying to turn the red");
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(HitDuration);
        spriteRenderer.color = Color.white;
    }
}

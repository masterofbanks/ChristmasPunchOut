using System.Collections;
using UnityEngine;

public class TestHitBehavior : MonoBehaviour
{
    public LayerMask HitLayer;
    public float HitDuration = 0.5f;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Hit"))
        {
            Debug.Log("Hit by the enemy");
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

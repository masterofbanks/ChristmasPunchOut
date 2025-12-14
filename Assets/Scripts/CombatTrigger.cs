using UnityEngine;

public class CombatTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out PlayerController controller))
        {
            controller.TriggerCombatMode();
        }
    }
}

using UnityEngine;

public class CombatTrigger : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerController controller))
        {
            controller.TriggerCombatMode();
        }
    }
}

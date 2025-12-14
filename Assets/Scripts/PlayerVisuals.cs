using UnityEngine;

public class PlayerVisuals : MonoBehaviour
{
    [Header("------------------- PLAYER DATA -------------------")]
    [Header("Needs to be Set In Inspector")]
    [SerializeField] private PlayerController player;

    public void ActivateAttackHitbox()
    {
        player.ActivateAttackHitbox();
    }
}

using UnityEngine;

public class PlayerHurtbox : MonoBehaviour
{
    [Header("------------------- DODGE SETTINGS -------------------")]
    [Header("Needs to be Set In Inspector")]
    [SerializeField] private PlayerController _player;

    public void ApplyAHit(float damage)
    {
        _player.ApplyAHit(damage);
    }
}

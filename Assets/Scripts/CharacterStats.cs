using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100;
    [SerializeField] private float _currentHealth = 100;
    public float MaxHealth { get { return _maxHealth; } }
    public float Health { get { return _currentHealth; } }


}

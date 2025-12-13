using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100;
    [SerializeField] private float _currentHealth = 100;
    public float MaxHealth { get { return _maxHealth; } }
    public float Health 
    { 
        get 
        {
            float cappedToMaxHealth = Mathf.Min(_currentHealth, _maxHealth);
            float preventedFromBeingNegative = Mathf.Max(0, cappedToMaxHealth);
            return preventedFromBeingNegative; 
        } 
    }


    public enum CharacterState
    {
        Alive,
        Dead

    }

    public CharacterState cState;

    private void Awake()
    {
        cState = CharacterState.Alive;
    }
    public void DealDamage(float amountOfDamage)
    {
        _currentHealth -= amountOfDamage;
        if(_currentHealth <= 0)
        {
            Debug.Log($"{gameObject.name} is dead");
            cState = CharacterState.Dead;
        }
    }

    public bool IsAlive()
    {
        return cState == CharacterState.Alive;

    }
}

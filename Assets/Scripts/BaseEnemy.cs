using UnityEngine;

public class BaseEnemy : MonoBehaviour
{
    public enum EnemyStates
    {
        Idle,
        Approaching,
        Attacking
    }

    [Header("Key Enemy Values")]
    public EnemyStates CurrentEnemyState;


    [Header("Targeting Behavior")]
    public Transform Player;
    public float StoppingDistance = 5.0f;
    public float PursuingSpeed = 10.0f;

    [Header("Attacking Behavior")]
    public float AttackDamage = 20f;
    public float AttackDuration = 1.0f;
    public float TimeBetweenAttacks = 2.5f;
    private float _timeAttackCounter = 0f;
    private float _timeBetweenAttackCounter = 0f;

    private Vector2 _vectorToPlayer;
    
    
    //components
    private Rigidbody2D _rb2D;
    private Animator _anime;

    private void Awake()
    {
        _rb2D = GetComponent<Rigidbody2D>();
        _anime = GetComponent<Animator>();
    }

    private void Start()
    {
        CurrentEnemyState = EnemyStates.Idle;
        _vectorToPlayer = Player.position - transform.position;
    }

    private void FixedUpdate()
    {
        _vectorToPlayer = Player.position - transform.position;
        StateController();
        _anime.SetInteger("state", (int)CurrentEnemyState);
    }

    private void StateController()
    {
        if (!Player.gameObject.GetComponent<CharacterStats>().IsAlive())
        {
            CurrentEnemyState = EnemyStates.Idle;
        }
        //stop the player (for now) if the enemy is close enough to the player
        else if (_vectorToPlayer.magnitude <= StoppingDistance)
        {
            if (_timeBetweenAttackCounter > TimeBetweenAttacks)
            {
                Debug.Log("Trying to Attack");
                _timeBetweenAttackCounter = 0f;
                _timeAttackCounter = AttackDuration;
                CurrentEnemyState = EnemyStates.Attacking;
            }

            else if (_timeAttackCounter > 0)
            {
                _timeAttackCounter -= Time.fixedDeltaTime;
            }

            else
            {
                _timeAttackCounter = 0f;
                _timeBetweenAttackCounter += Time.fixedDeltaTime;
                CurrentEnemyState = EnemyStates.Idle;
            }
        }

        //else run at the enemy
        else
        {
            CurrentEnemyState = EnemyStates.Approaching;
            _rb2D.MovePosition(_rb2D.position + _vectorToPlayer.normalized * PursuingSpeed * Time.fixedDeltaTime);
        }
    }




}

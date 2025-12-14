using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
public class BaseEnemy : MonoBehaviour
{
    public enum EnemyStates
    {
        Idle,
        Approaching,
        Attacking, // to add: light jab attack, blocking behavior, hit behavior, and death anims
        Blocking,
        Hit,
        Dead,
        BlockHit
    }

    [System.Serializable]
    public class EnemyStructure 
    {
        public string Name;
        public float Damage;
        public float Duration;
        public float CooldownTime;
    }

    public List<EnemyStructure> EnemyAttacks;
    public EnemyStructure CurrentAttack;

    [Header("Key Enemy Values")]
    public EnemyStates CurrentEnemyState;
    public CharacterStats EnemyStats;

    [Header("Targeting Behavior")]
    public Transform Player;
    public float StoppingDistance = 5.0f;
    public float PursuingSpeed = 10.0f;

    [Header("Hurt Values")]
    public float HitStunDuration = 1.1f;
    public float BlockHitStunDuration = 0.7f;

    private float _timeInHitStun = 0f;
    private float _timeBetweenAttacks = 0f;
    private float _timeAttackCounter = 0f;
    private float _timeBetweenAttackCounter = 0f;
    private Vector3 _vectorToPlayer;
    
    
    //components
    private Rigidbody _rb;
    private Animator _anime;
    private System.Random rndGen;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _anime = GetComponent<Animator>();
        rndGen = new System.Random();
        EnemyStats = GetComponent<CharacterStats>();

        CurrentAttack = EnemyAttacks[0];
        _timeBetweenAttacks = CurrentAttack.CooldownTime;
        

    }

    private void Start()
    {
        CurrentEnemyState = EnemyStates.Idle;
        _vectorToPlayer = Player.position - transform.position;
    }

    private void FixedUpdate()
    {
        _vectorToPlayer = Player.position - transform.position;
        //only update states if the enemy is alive
        if(CurrentEnemyState != EnemyStates.Dead)
            StateController();
        _anime.SetInteger("state", (int)CurrentEnemyState);
    }

    private void StateController()
    {
        //if the player is in hitStun, change the state to the Hit state
        if(_timeInHitStun > 0)
        {
            HitRoutine();
        }
        
        //if the player is dead, return the enemy to its idle state
        else if (!Player.gameObject.GetComponent<CharacterStats>().IsAlive())
        {
            CurrentEnemyState = EnemyStates.Idle;
        }

        //stop the player (for now) if the enemy is close enough to the player
        else if (_vectorToPlayer.magnitude <= StoppingDistance)
        {
            //enter the attack state if enough time has passed between attacks
            if (_timeBetweenAttackCounter > _timeBetweenAttacks)
            {
                AttackingBehavior();
            }

            else if (_timeAttackCounter > 0)
            {
                _timeAttackCounter -= Time.fixedDeltaTime;
            }

            else
            {
                _timeAttackCounter = 0f;
                _timeBetweenAttackCounter += Time.fixedDeltaTime;
                CurrentEnemyState = EnemyStates.Blocking;
            }
        }

        //else run at the enemy
        else
        {
            CurrentEnemyState = EnemyStates.Approaching;
            _rb.MovePosition(_rb.position + _vectorToPlayer.normalized * PursuingSpeed * Time.fixedDeltaTime);
        }
    }

    private void AttackingBehavior()
    {
        //find a random attack to display
        int randomIndex = rndGen.Next(0, EnemyAttacks.Count);
        _anime.SetInteger("attack_type", randomIndex);
        CurrentAttack = EnemyAttacks[randomIndex];

        //perform the attack
        _timeBetweenAttackCounter = 0f;
        _timeAttackCounter = CurrentAttack.Duration;
        CurrentEnemyState = EnemyStates.Attacking;
    }

    private void HitRoutine()
    {
        if(CurrentEnemyState == EnemyStates.Blocking ||CurrentEnemyState == EnemyStates.BlockHit)
        {
            CurrentEnemyState = EnemyStates.BlockHit;
        }
        else
        {
            CurrentEnemyState = EnemyStates.Hit;
        }

        _timeInHitStun -= Time.fixedDeltaTime;
        if(_timeInHitStun < 0)
        {
            _timeInHitStun = 0f;
        }
    }

    public void ApplyAHit(float amountOfDamage)
    {
        if(CurrentEnemyState != EnemyStates.Blocking && CurrentEnemyState != EnemyStates.Hit && CurrentEnemyState != EnemyStates.BlockHit)
        {
            EnemyStats.DealDamage(amountOfDamage);
            if (EnemyStats.IsAlive())
            {
                _timeInHitStun = HitStunDuration;
            }

            else
            {
                PerformDeath();
            }
        }

        else
        {
            _timeInHitStun = BlockHitStunDuration;
        }
        
    }

    private void PerformDeath()
    {
        CurrentEnemyState = EnemyStates.Dead;
    }


}

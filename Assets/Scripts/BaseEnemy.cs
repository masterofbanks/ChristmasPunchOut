using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
public class BaseEnemy : MonoBehaviour
{
    public enum EnemyStates
    {
        Idle,
        Approaching,
        Attacking, // to add: light jab attack, blocking behavior, hit beavjor, and death anims
        Blocking
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


    [Header("Targeting Behavior")]
    public Transform Player;
    public float StoppingDistance = 5.0f;
    public float PursuingSpeed = 10.0f;


    [Header("Uppercut Values")]
    

    private float _timeBetweenAttacks = 0f;
    private float _timeAttackCounter = 0f;
    private float _timeBetweenAttackCounter = 0f;

    private Vector2 _vectorToPlayer;
    
    
    //components
    private Rigidbody2D _rb2D;
    private Animator _anime;
    private System.Random rndGen;

    private void Awake()
    {
        _rb2D = GetComponent<Rigidbody2D>();
        _anime = GetComponent<Animator>();
        rndGen = new System.Random();


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
        StateController();
        _anime.SetInteger("state", (int)CurrentEnemyState);
    }

    private void StateController()
    {
        //if the player is dead, return the enemy to its idle state
        if (!Player.gameObject.GetComponent<CharacterStats>().IsAlive())
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
            _rb2D.MovePosition(_rb2D.position + _vectorToPlayer.normalized * PursuingSpeed * Time.fixedDeltaTime);
        }
    }

    private void AttackingBehavior()
    {
        int randomIndex = rndGen.Next(0, EnemyAttacks.Count);
        _anime.SetInteger("attack_type", randomIndex);
        CurrentAttack = EnemyAttacks[randomIndex];
        _timeBetweenAttackCounter = 0f;
        _timeAttackCounter = CurrentAttack.Duration;
        CurrentEnemyState = EnemyStates.Attacking;
    }



}

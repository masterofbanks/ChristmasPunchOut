using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Camera playerCamera;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    [SerializeField] private bool canFreeMove = true;

    private Vector2 movementInput;
    private Rigidbody2D rb;

    [Space]
    [Space]
    [Header("------------------- DODGE SETTINGS -------------------")]
    [Header("Needs to be Set In Inspector")]
    [SerializeField] float _dodgeSpeed_LowerIsFaster = .3f;
    float _dodgeTimer;
    [SerializeField] float _dodgeDistance = 2;
    [SerializeField] float _dodgeInputThreshold = .2f;

    [Header("Exposed In Inspector For Debugging")]
    [SerializeField] float _dodgeTarget;
    private enum DodgeState
    {
        CanDodge,
        IsMovingToTarget,
        IsMovingBack,
        NeedsReset
    }
    [SerializeField] DodgeState _dodgeInputState = DodgeState.CanDodge;
    float _initialDodgeXPosition;

    [Space]
    [Space]
    [Header("------------------- ANIMATOR SETTINGS -------------------")]
    [Header("Needs to be Set In Inspector")]
    [SerializeField] private Animator _animator;

    [Space]
    [Space]
    [Header("------------------- ATTACKING -------------------")]
    [Header("Needs to be Set In Inspector")]
    [SerializeField] private PlayerHitbox _attackHitbox;
    
    private void Start()
    {
        playerCamera = Camera.main;
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        MovePlayer();

        if(_dodgeInputState == DodgeState.IsMovingToTarget)
        {
            if(_dodgeTimer > 0)
            {
                _dodgeTimer -= Time.deltaTime;
                float newXPosition = Mathf.Lerp(_dodgeTarget, _initialDodgeXPosition, _dodgeTimer / _dodgeSpeed_LowerIsFaster);
                transform.position = new Vector3(newXPosition, transform.position.y, transform.position.z);
            }
            else
            {
                transform.position = new Vector3(_dodgeTarget, transform.position.y, transform.position.z);
                _dodgeInputState = DodgeState.IsMovingBack;
                _dodgeTimer = _dodgeSpeed_LowerIsFaster;
            }
        }
        
        if(_dodgeInputState == DodgeState.IsMovingBack)
        {
            if(_dodgeTimer > 0)
            {
                _dodgeTimer -= Time.deltaTime;
                float newXPosition = Mathf.Lerp(_initialDodgeXPosition, _dodgeTarget, _dodgeTimer / _dodgeSpeed_LowerIsFaster);
                transform.position = new Vector3(newXPosition, transform.position.y, transform.position.z);
            }
            else
            {
                transform.position = new Vector3(_initialDodgeXPosition, transform.position.y, transform.position.z);
                _dodgeInputState = DodgeState.CanDodge;
                gameObject.layer = LayerManager.DEFAULT_LAYER;
            }
        }
    }

    public void OnMove(InputValue value)
    {
        movementInput = value.Get<Vector2>();
    }
    public void OnAttackLeft(InputValue value)
    {
        _animator.SetTrigger("AttackLeft");
    }
    public void OnAttackRight(InputValue value)
    {
        _animator.SetTrigger("AttackRight");
    }

    public void ActivateAttackHitbox()
    {
        _attackHitbox.gameObject.SetActive(true);
    }

    public void ResetDodge()
    {
        _dodgeInputState = DodgeState.CanDodge;
    }

    private void MovePlayer()
    {
        if (canFreeMove)
        {
            FreeMovementHandler();
        }
        else
        {
            CombatMovementHandler();
        }
    }

    private void FreeMovementHandler()
    {
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (right * movementInput.x + forward * movementInput.y).normalized;

        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void CombatMovementHandler()
    {
        if (_dodgeInputState == DodgeState.CanDodge && Mathf.Abs(movementInput.x) > _dodgeInputThreshold)
        {
            _dodgeInputState = DodgeState.IsMovingToTarget;
            _dodgeTimer = _dodgeSpeed_LowerIsFaster;
            _initialDodgeXPosition = transform.position.x;
            gameObject.layer = LayerManager.IFRAME_LAYER;

            if(movementInput.x < 0)
            {
                _dodgeTarget = _initialDodgeXPosition - _dodgeDistance;
                _animator.SetTrigger("DodgeLeft");
            }
            else
            {
                _dodgeTarget = _initialDodgeXPosition + _dodgeDistance;
                _animator.SetTrigger("DodgeRight");
            }
        }
    }

    public void TriggerCombatMode()
    {
        canFreeMove = false;
    }
    public void TriggerFreeMoveMode()
    {
        canFreeMove = true;
    }
}

using UnityEngine;
using System;
using ZAN.Input;
using UnityEngine.InputSystem;

public class DuelInputBehaviour : MonoBehaviour
{
    private InputSystem_Actions _inputActions;
    private bool _attackConsumed;
    
    
    public event Action OnAttack;
    
    private void Awake()
    {
        _inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _inputActions.Player.DuelInputs.performed += HandleAttack;
        _inputActions.Player.Enable();

    }

    private void OnDisable()
    {
        _inputActions.Player.DuelInputs.performed -= HandleAttack;
        _inputActions.Player.Disable();
    }

    private void OnDestroy()
    {
        _inputActions.Dispose();
    }

    private void HandleAttack(InputAction.CallbackContext context)
    {
        if (_attackConsumed) return;
        _attackConsumed = true;
        OnAttack?.Invoke();
    }

    public void ResetAttack()
    {
        _attackConsumed = false;
    }

    public void SetAttackEnabled(bool isEnabled)
    {
        var action = _inputActions.Player.DuelInputs;
        switch (isEnabled)
        {
            case true when !action.enabled:
                action.Enable();
                break;
            case false when action.enabled:
                action.Disable();
                break;
        }
    }


}

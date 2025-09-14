using UnityEngine;
using UnityEngine.InputSystem;
using ZAN.Input;

public class DuelInputBehaviour : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    private bool PressedThisFrame { get; set; }

    private InputSystem_Actions _inputActions;

    private void OnEnable()
    {
        _inputActions = new InputSystem_Actions();
        _inputActions.Player.Enable();
        _inputActions.Player.SetCallbacks(this);
    }

    private void OnDisable()
    {
        _inputActions.Player.RemoveCallbacks(this);
        _inputActions.Player.Disable();
    }

    public void OnDuelInputs(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            PressedThisFrame = true;
        }
    }

    public bool ConsumePressed()
    {
        if (!PressedThisFrame) return false;
        PressedThisFrame = false;
        return true;
    }
}

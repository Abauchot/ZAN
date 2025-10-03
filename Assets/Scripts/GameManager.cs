using Player;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private DuelInputBehaviour input;
    [SerializeField] private PlayerController player;
    
    [Header("Duel Mode")]
    [SerializeField] private bool isDuelMode = false;

    private void OnEnable()
    {

        if (!isDuelMode && input != null)
        {
            input.OnAttack += OnPlayerAttack;
        }
    }
    
    private void OnDisable()
    {
        if (input != null)
        {
            input.OnAttack -= OnPlayerAttack;
        }
    }
    
    private void OnPlayerAttack()
    {
        Debug.Log("Player Attack!");
        player.OnAttack();
        input.SetAttackEnabled(false);
        // Simulate some processing time before re-enabling attack
        Invoke(nameof(EnablePlayerAttack), 1f);
    }

    private void EnablePlayerAttack()
    {
        input.SetAttackEnabled(true);
        input.ResetAttack();
        Debug.Log("Player Attacked!");
    }
}

using Player;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private DuelInputBehaviour input;
    [SerializeField] private PlayerController player;


    private void OnEnable()
    {
        input.OnAttack += OnPlayerAttack;
        
    }
    
    private void OnDisable()
    {
        input.OnAttack -= OnPlayerAttack;
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

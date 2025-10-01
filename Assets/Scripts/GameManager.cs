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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

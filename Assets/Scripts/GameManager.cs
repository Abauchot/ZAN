using UnityEngine;
using UnityEngine.UI;

public enum DuelState 
{
    Ready,
    Waiting,
    Signal,
    Resolve,
    Result
}

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DuelInputBehaviour duelInputBehaviour;
    [SerializeField] private Text stateText;
    [SerializeField] private Text msText;
    [SerializeField] private Text hintText;
    
    [Header("Config")]
    [SerializeField] Vector2 waitRange = new Vector2(2f, 5f);
    [SerializeField] Vector2 reactRange = new Vector2(100f, 200f);
    [SerializeField, Range(0f, 1f)] float aiFalseStartChance = 0.05f;
    
    private DuelState _state;
    private float _signalAt;
    private bool _winnerLocked;
    private int _winner; // 0 = none, 1 = player, 2 = ai
    private float _reactedAt;
    
    
    //AI
    private float _aiPlannedPressAt;
    private bool _aiWillfalseStart;
    
    
    
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartRound();
    }

    void StartRound()
    {
        _state = DuelState.Waiting;
        _winnerLocked = false; 
        _winner = 0;
        msText.text = "";
        hintText.text = "Wait ....";
        
        // Determine when the signal will be given
        _signalAt = Time.unscaledDeltaTime + Random.Range(waitRange.x, waitRange.y);
        
        // Determine if the AI will false start
        _aiWillfalseStart = Random.value < aiFalseStartChance;
        
        if (_aiWillfalseStart)
        {
            _aiPlannedPressAt = Time.unscaledTime + Random.Range(0.05f, Mathf.Max(0.06f, _signalAt - Time.unscaledTime - 0.02f));
        }
        else
        {
            float aiDelay = Random.Range(reactRange.x, reactRange.y) / 1000f;
            _aiPlannedPressAt = _signalAt + aiDelay;
        }
        
        stateText.text= "READY ?";
    }

    // Update is called once per frame
    void Update()
    {
        switch (_state)
        {

        }
        {
            
        }
        
    }
}

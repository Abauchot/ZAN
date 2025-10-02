using System;
using System.Collections;
using UnityEngine;
using TMPro;

namespace DuelState
{
public class DuelController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DuelInputBehaviour input;
    [SerializeField] private DuelConfig config;

    [Header("UI (optional)")]
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private TMP_Text msText;
    [SerializeField] private TMP_Text hintText;

    private DuelStateMachine _sm;
    private Coroutine _loop;

    private void Awake()
    {
       
        if (input == null)
        {
            Debug.LogError("[DuelController] DuelInputBehaviour reference is missing!", this);
            enabled = false;
            return;
        }
        
        if (config == null)
        {
            Debug.LogError("[DuelController] DuelConfig reference is missing!", this);
            enabled = false;
            return;
        }

        _sm = new DuelStateMachine();
        _sm.OnStateChanged += HandleStateChanged;
        _sm.OnSignal += HandleSignal;
        _sm.OnDuelEnded += HandleResult; 
    }

    private void OnEnable()
    {
        if (input != null)
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
        if (_loop != null) StopCoroutine(_loop);
    }

    private void Start()
    {
        // Vérification supplémentaire avant de démarrer
        if (_sm != null && input != null && config != null)
        {
            StartRound();
        }
    }

    public void StartRound()
    {
        if (_sm == null || input == null || config == null) return;
        
        if (_loop != null) StopCoroutine(_loop);
        _loop = StartCoroutine(RunRound());
    }

    private IEnumerator RunRound()
    {
        // READY
        _sm.ResetRound();
        input.SetAttackEnabled(false);
        input.ResetAttack();     // anti-rebond côté input

        yield return null; // 1 frame

        // WAITING
        _sm.EnterWaiting();
        input.SetAttackEnabled(true);

        var wait = UnityEngine.Random.Range(config.waitingTimeRange.x, config.waitingTimeRange.y);
        var t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < wait)
        {
            if (_sm.State != DuelState.Waiting) break; 
            if (_sm.PlayerAttacked || _sm.FalseStart) break; 
            yield return null;
        }

        if (_sm.FalseStart)
            yield return ResultsThenRestart();
        else
        {
            // SIGNAL + RESOLVE
            _sm.EnterSignal(Time.realtimeSinceStartup);
            var resolveTimeout = 3f;
            var r0 = Time.realtimeSinceStartup;
            while (_sm.State == DuelState.Resolve &&
                   Time.realtimeSinceStartup - r0 < resolveTimeout)
            {
                yield return null;
            }

            if (_sm.State == DuelState.Resolve)
            {
                //No attack registered during resolve phase
                // -1f means "no attack"
                _sm.RegisterPlayerAttack(-1f); 
            }

            yield return ResultsThenRestart();
        }
    }

    private IEnumerator ResultsThenRestart()
    {
        input.SetAttackEnabled(false);
        yield return WaitRealtime(config.resultStaySeconds); 
        StartRound();
    }

    private void OnPlayerAttack()
    {
        if (_sm != null)
        {
            _sm.RegisterPlayerAttack(Time.realtimeSinceStartup);
        }
    }

    // === UI hooks ===
    private void HandleStateChanged(DuelState s)
    {
        if (stateText) stateText.text = $"State: {s}";
        if (hintText)
        {
            switch (s)
            {
                case DuelState.Ready:   hintText.text = "Get ready…"; break;
                case DuelState.Waiting: hintText.text = "Don't move!"; break;
                case DuelState.Signal:  hintText.text = "GO!"; break;
                case DuelState.Resolve: hintText.text = "Resolving…"; break;
                case DuelState.Results: hintText.text = "Result"; break;
            }
        }
        // Debug log
        Debug.Log($"[Duel] -> {s}");
    }

    private void HandleSignal()
    {
        // TODO: FX/SFX (flash, son, etc.)
    }

    private void HandleResult(DuelOutcome outcome, float? playerMs)
    {
        if (!msText) return;
        msText.text = outcome switch
        {
            DuelOutcome.FalseStart => "False Start! You lose.",
            DuelOutcome.PlayerWin =>
                $"Player reaction: {(playerMs.HasValue ? playerMs.Value * 1000 : 0):0} ms (AI WIP)",
            DuelOutcome.Draw => "Draw.",
            DuelOutcome.NoAttack => "No attack.",
            _ => ""
        };
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        var t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
    }
}

}
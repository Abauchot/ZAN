using System;
using System.Collections;
using UnityEngine;
using TMPro;


namespace DuelState
{
    /// <summary>
    /// Main controller that orchestrates the duel flow.
    /// Manages the state machine, handles player input, controls AI behavior, and updates UI.
    /// </summary>
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
        private Coroutine _aiRoutine; 

        private void Awake()
        {
            // Validate required references
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

            // Initialize state machine and subscribe to its events
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
            // Extra validation before starting
            if (_sm != null && input != null && config != null)
            {
                StartRound();
            }
        }

        /// <summary>
        /// Starts a new duel round
        /// </summary>
        public void StartRound()
        {
            if (_sm == null || input == null || config == null) return;
        
            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(RunRound());
        }

        /// <summary>
        /// Main coroutine that runs through all phases of a duel round:
        /// Ready -> Waiting -> Signal -> Resolve -> Results
        /// </summary>
        private IEnumerator RunRound()
        {
            // === READY PHASE ===
            // Reset state machine and disable input to prepare for new round
            _sm.ResetRound();
            input.SetAttackEnabled(false);
            input.ResetAttack();     // Prevent input bounce from previous round

            yield return null; // Wait 1 frame

            // === WAITING PHASE ===
            // Random waiting period - players must NOT attack yet
            _sm.EnterWaiting();
            input.SetAttackEnabled(true);

            // Randomize wait time to keep players on edge
            var wait = UnityEngine.Random.Range(config.waitingTimeRange.x, config.waitingTimeRange.y);
            var t0 = Time.realtimeSinceStartup;
            
            // Determine if AI will make a false start this round
            bool aiWillFalseStart = UnityEngine.Random.value < config.aiFalseStartChance;
            float aiFalseStartTime = aiWillFalseStart ? UnityEngine.Random.Range(0.3f, wait * 0.8f) : float.MaxValue;
            
            // Wait loop - check for player false start or AI false start
            while (Time.realtimeSinceStartup - t0 < wait)
            {
                if (_sm.State != DuelState.Waiting) break; 
                if (_sm.PlayerAttacked || _sm.FalseStart) break;
                
                // Check if AI makes a false start
                if (Time.realtimeSinceStartup - t0 >= aiFalseStartTime)
                {
                    Debug.Log("[AI] False Start!");
                    _sm.TriggerFalseStart();
                    break;
                }
                
                yield return null;
            }

            // If false start occurred, skip to results
            if (_sm.FalseStart)
            {
                yield return ResultsThenRestart();
            }
            else
            {
                // === SIGNAL PHASE ===
                // Display GO signal and record the exact time
                _sm.EnterSignal(Time.realtimeSinceStartup);
                
                // Brief delay to make signal visible (0.15 seconds)
                yield return WaitRealtime(0.15f);
                
                // === RESOLVE PHASE ===
                // Both players can now attack - determine winner
                _sm.EnterResolve();
                
                // Wait for both to attack or timeout
                var resolveTimeout = 3f;
                var r0 = Time.realtimeSinceStartup;
                while (_sm.State == DuelState.Resolve &&
                       Time.realtimeSinceStartup - r0 < resolveTimeout)
                {
                    yield return null;
                }

                // Force resolution if timeout reached
                if (_sm.State == DuelState.Resolve)
                {
                    _sm.ResolveWinner();
                }

                // === RESULTS PHASE ===
                yield return ResultsThenRestart();
            }
        }

        /// <summary>
        /// Shows results for configured duration, then starts next round
        /// </summary>
        private IEnumerator ResultsThenRestart()
        {
            input.SetAttackEnabled(false);
            yield return WaitRealtime(config.resultStaySeconds); 
            StartRound();
        }

        /// <summary>
        /// Called when player presses attack button
        /// Registers attack with state machine and resolves if both have attacked
        /// </summary>
        private void OnPlayerAttack()
        {
            if (_sm != null)
            {
                _sm.RegisterPlayerAttack(Time.realtimeSinceStartup);

                // If both player and AI have attacked, resolve winner immediately
                if (_sm.State == DuelState.Resolve && _sm.AIAttacked)
                {
                    _sm.ResolveWinner();
                }
            }
        }

        // === UI CALLBACKS ===
        
        /// <summary>
        /// Updates UI when state changes
        /// </summary>
        private void HandleStateChanged(DuelState s)
        {
            if (stateText) stateText.text = $"State: {s}";
            if (hintText)
            {
                hintText.text = s switch
                {
                    DuelState.Ready => "Get ready…",
                    DuelState.Waiting => "Don't move!",
                    DuelState.Signal => "GO!",
                    DuelState.Resolve => "Resolving…",
                    DuelState.Results => "Result",
                    _ => hintText.text
                };
            }
            // Debug log
            Debug.Log($"[Duel] -> {s}");
        }

        /// <summary>
        /// Called when signal appears - starts AI reaction coroutine
        /// </summary>
        private void HandleSignal()
        {
            Debug.Log("[Signal] GO! Starting AI reaction coroutine...");
            
            if (_aiRoutine != null) StopCoroutine(_aiRoutine);
            _aiRoutine = StartCoroutine(AIReactAfterDelay());
            
            Debug.Log($"[Signal] AI coroutine started: {_aiRoutine != null}");
        }

        /// <summary>
        /// Displays the duel outcome and timing results
        /// </summary>
        private void HandleResult(DuelOutcome outcome, float? playerMs)
        {
            Debug.Log($"[Duel] Result: {outcome} | Player: {playerMs?.ToString("F0") ?? "N/A"} ms");
            
            if (!msText) return;
            
            // Calculate AI reaction time if they attacked
            float? aiMs = _sm.AIAttacked ? (_sm.AIAttackAtRealTime - _sm.SignalAtRealTime) * 1000f : (float?)null;
            
            // Format result message based on outcome
            msText.text = outcome switch
            {
                DuelOutcome.FalseStart => _sm.PlayerFalseStart 
                    ? "False Start! You lose." 
                    : "False Start! AI loses. You win!",
                DuelOutcome.PlayerWin => 
                    $"You Win! Player: {playerMs?.ToString("0") ?? "N/A"} ms | AI: {aiMs?.ToString("0") ?? "N/A"} ms",
                DuelOutcome.AIWin => 
                    $"AI Wins! Player: {playerMs?.ToString("0") ?? "N/A"} ms | AI: {aiMs?.ToString("0") ?? "N/A"} ms",
                DuelOutcome.Draw => 
                    $"Draw! Both: ~{playerMs?.ToString("0") ?? "N/A"} ms",
                DuelOutcome.NoAttack => "No attack registered.",
                _ => ""
            };
        }

        /// <summary>
        /// Waits for a specified duration using real time (unaffected by Time.timeScale)
        /// </summary>
        private static IEnumerator WaitRealtime(float seconds)
        {
            var t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
        }

        /// <summary>
        /// AI reaction coroutine - waits for random delay then attacks
        /// Simulates AI reaction time after signal appears
        /// </summary>
        private IEnumerator AIReactAfterDelay()
        {
            // Random reaction time within configured range
            float aiMs = UnityEngine.Random.Range(config.aiReactRangeMs.x, config.aiReactRangeMs.y);
            var delay = aiMs / 1000f;
            
            Debug.Log($"[AI] Will attack in {aiMs:F0} ms (delay: {delay:F3}s)");
        
            var t0 = Time.realtimeSinceStartup;
            // Wait for delay AND ensure we're still in a valid state (Signal or Resolve)
            while (Time.realtimeSinceStartup - t0 < delay && 
                   (_sm.State == DuelState.Signal || _sm.State == DuelState.Resolve)) 
                yield return null;

            Debug.Log($"[AI] Delay finished. State: {_sm.State}, PlayerAttacked: {_sm.PlayerAttacked}");

            // Attack if still in valid state
            if (_sm.State == DuelState.Signal || _sm.State == DuelState.Resolve)
            {
                Debug.Log($"[AI] Attacking now!");
                _sm.RegisterAIAttack(Time.realtimeSinceStartup);
                Debug.Log($"[AI] Attack registered. AIAttacked: {_sm.AIAttacked}");
            }
            else
            {
                Debug.Log($"[AI] Cannot attack - invalid state: {_sm.State}");
            }
        }
    }
}


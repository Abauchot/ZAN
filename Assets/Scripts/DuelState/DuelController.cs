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
        [Header("Refs")] [SerializeField] private DuelInputBehaviour input;
        [SerializeField] private DuelConfig config;
        [SerializeField] private CharacterAnimationController playerAnimation;
        [SerializeField] private CharacterAnimationController aIAnimation;

        [Header("UI settings")] [SerializeField]
        private TMP_Text stateText;

        [SerializeField] private TMP_Text msText;
        [SerializeField] private TMP_Text hintText;

        [Header("BO5 Settings")] [SerializeField]
        private GameObject endPanel;

        [SerializeField] private TMP_Text endText;
        [SerializeField] private UnityEngine.UI.Button restartButton;

        [Header("BO5 Visuals")] [SerializeField]
        private UnityEngine.UI.Image[] playerLives;

        [SerializeField] private UnityEngine.UI.Image[] aiLives;
        [SerializeField] private Sprite fullHeart;
        [SerializeField] private Sprite emptyHeart;

        [Header("Dash Settings")] [SerializeField]
        private Transform playerTransform;

        [SerializeField] private Transform aiTransform;
        [SerializeField] private float dashStopDistanceFromTarget = 2f;
        [SerializeField] private float dashForwardDuration = 0.12f;
        [SerializeField] private float retreatDuration = 0.18f;
        [SerializeField] private float dashAttackDurationMultiplier = 1.2f;
        [SerializeField] private AnimationCurve dashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve retreatCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        private Vector3 _playerStartPos;
        private Vector3 _aiStartPos;
        private Coroutine _playerMoveRoutine;
        private Coroutine _aiMoveRoutine;

        private DuelStateMachine _sm;
        private Coroutine _loop;
        private Coroutine _aiRoutine;

        private int _playerScore;
        private int _aiScore;
        private const int MaxScore = 3;
        private bool _duelFinished;

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

            if (endPanel != null)
                endPanel.SetActive(false);
            if (restartButton != null)
                restartButton.onClick.AddListener(RestartBo5);
            UpdateLivesVisual();

            if (playerTransform != null)
            {
                _playerStartPos = playerTransform.position;
            }

            if (aiTransform != null)
            {
                _aiStartPos = aiTransform.position;
            }
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

            _playerScore = 0;
            _aiScore = 0;
            _duelFinished = false;
            UpdateLivesVisual();
        }

        /// <summary>
        /// Starts a new duel round
        /// </summary>
        private void StartRound()
        {
            if (playerTransform && _playerMoveRoutine != null)
            {
                StopCoroutine(_playerMoveRoutine);
                playerTransform.position = _playerStartPos;
            }

            if (aiTransform && _aiMoveRoutine != null)
            {
                StopCoroutine(_aiMoveRoutine);
                aiTransform.position = _aiStartPos;
            }


            if (_sm == null || !input || !config) return;

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
            input.ResetAttack(); // Prevent input bounce from previous round

            yield return null; // Wait 1 frame

            // === WAITING PHASE ===
            // Random waiting period - players must NOT attack yet
            _sm.EnterWaiting();
            input.SetAttackEnabled(true);

            // Randomize wait time to keep players on edge
            var wait = UnityEngine.Random.Range(config.waitingTimeRange.x, config.waitingTimeRange.y);
            var t0 = Time.realtimeSinceStartup;

            // Determine if AI will make a false start this round
            var aiWillFalseStart = UnityEngine.Random.value < config.aiFalseStartChance;
            var aiFalseStartTime = aiWillFalseStart ? UnityEngine.Random.Range(0.3f, wait * 0.8f) : float.MaxValue;

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
            if (playerAnimation)
                playerAnimation.ResetAttack();
            yield return WaitRealtime(config.resultStaySeconds);
            if (!_duelFinished)
                StartRound();
        }

        /// <summary>
        /// Called when player presses attack button
        /// Registers attack with state machine and resolves if both have attacked
        /// </summary>
        private void OnPlayerAttack()
        {
            if (_sm == null) return;
            _sm.RegisterPlayerAttack(Time.realtimeSinceStartup);
            Debug.Log($"[DuelController] OnPlayerAttack called. playerAnimation assigned: {playerAnimation != null}");


            // if (playerAnimation != null)
            // {
            //     playerAnimation.PlayAttack();
            // }

            // var validAttack = _sm.State is DuelState.Signal or DuelState.Resolve;
            // if (validAttack && playerTransform != null && aiTransform != null)
            // {
            //     if (_playerMoveRoutine != null)
            //         StopCoroutine(_playerMoveRoutine);
            //
            //     float forwardDuration = dashForwardDuration;
            //
            //     if (playerAnimation != null && playerAnimation.AttackDuration > 0f)
            //         forwardDuration = playerAnimation.AttackDuration * dashAttackDurationMultiplier;
            //
            //     _playerMoveRoutine = StartCoroutine(PlayerDashAndRetreat(forwardDuration));
            // }

            if (_sm.State == DuelState.Resolve && _sm.AIAttacked)
            {
                _sm.ResolveWinner();
            }

            Debug.Log("Player AttackDuration = " + playerAnimation.AttackDuration);
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
            if (_aiRoutine != null) StopCoroutine(_aiRoutine);
            _aiRoutine = StartCoroutine(AIReactAfterDelay());
        }

        /// <summary>
        /// Displays the duel outcome and timing results
        /// </summary>
        private void HandleResult(DuelOutcome outcome, float? playerMs)
        {
            Debug.Log($"[Duel] Result: {outcome} | Player: {playerMs?.ToString("F0") ?? "N/A"} ms");

            if (!msText) return;

            float? aiMs = _sm.AIAttacked ? (_sm.AIAttackAtRealTime - _sm.SignalAtRealTime) * 1000f : null;

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

            switch (outcome)
            {
                case DuelOutcome.PlayerWin:
                    _playerScore++;
                    break;

                case DuelOutcome.AIWin:
                    _aiScore++;
                    break;

                case DuelOutcome.FalseStart:
                    if (_sm.PlayerFalseStart)
                        _aiScore++;
                    else
                        _playerScore++;
                    break;

                case DuelOutcome.None:
                case DuelOutcome.Draw:
                case DuelOutcome.NoAttack:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }

            UpdateLivesVisual();

            switch (outcome)
            {
                case DuelOutcome.PlayerWin:
                    StartCoroutine(PlayerWinSequence());
                    break;
                case DuelOutcome.AIWin:
                    StartCoroutine(AIWinSequence());
                    break;
                case DuelOutcome.FalseStart:
                    StartCoroutine(_sm.PlayerFalseStart ? AIWinSequence() : PlayerWinSequence());
                    break;
                case DuelOutcome.None:
                case DuelOutcome.Draw:
                case DuelOutcome.NoAttack:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }

            if (_playerScore < MaxScore && _aiScore < MaxScore) return;
            _duelFinished = true;
            ShowEndPanel();
        }


        private void UpdateLivesVisual()
        {
            if (playerLives == null || !fullHeart || !emptyHeart)
                return;

            for (var i = 0; i < playerLives.Length; i++)
            {
                playerLives[i].sprite = i < (MaxScore - _aiScore) ? fullHeart : emptyHeart;
            }

            for (var i = 0; i < aiLives.Length; i++)
            {
                aiLives[i].sprite = i < (MaxScore - _playerScore) ? fullHeart : emptyHeart;
            }
        }


        private void ShowEndPanel()
        {
            if (!endPanel || !endText) return;
            endPanel.SetActive(true);
            endText.text = _playerScore > _aiScore ? "You win !" : "AI win !";
        }

        private void RestartBo5()
        {
            _playerScore = 0;
            _aiScore = 0;
            _duelFinished = false;
            if (endPanel != null)
                endPanel.SetActive(false);
            UpdateLivesVisual();
            StartRound();
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
            var aiMs = UnityEngine.Random.Range(config.aiReactRangeMs.x, config.aiReactRangeMs.y);
            var delay = aiMs / 1000f;
            var t0 = Time.realtimeSinceStartup;
            // Wait for delay AND ensure we're still in a valid state (Signal or Resolve)
            while (Time.realtimeSinceStartup - t0 < delay &&
                   _sm.State is DuelState.Signal or DuelState.Resolve)
                yield return null;


            if (_sm.State is DuelState.Signal or DuelState.Resolve)
            {
                _sm.RegisterAIAttack(Time.realtimeSinceStartup);
                //
                // if (aIAnimation)
                //     aIAnimation.PlayAttack();
                //
                // if (_aiMoveRoutine != null)
                //     StopCoroutine(_aiMoveRoutine);
                //
                // float forwardDuration = dashForwardDuration;
                // if (aIAnimation && aIAnimation.AttackDuration > 0f)
                //     forwardDuration = aIAnimation.AttackDuration * dashAttackDurationMultiplier;
                //
                // _aiMoveRoutine = StartCoroutine(AIDashAndRetreat(forwardDuration));
            }

            else
            {
                Debug.Log($"[AI] Cannot attack - invalid state: {_sm.State}");
            }
        }

        private IEnumerator PlayerDashAndRetreat(float forwardDuration)
        {
            if (!playerTransform || !aiTransform)
                yield break;

            Vector3 start = _playerStartPos;
            Vector3 dir = aiTransform.position - start;
            if (dir.sqrMagnitude < 0.0001f) yield break;
            dir.Normalize();

            Vector3 dashEnd = aiTransform.position - dir * dashStopDistanceFromTarget;

            if (playerAnimation)
            {
                playerAnimation.PlayAttack();
            }

            // PHASE 1 : dash towards AI
            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / forwardDuration;
                var k = dashCurve?.Evaluate(t) ?? t;
                playerTransform.position = Vector3.Lerp(start, dashEnd, k);
                yield return null;
            }

            playerTransform.position = dashEnd;

            // small contact time at the end of the dash
            yield return new WaitForSeconds(0.05f);

            // PHASE 2 : return to start with run animation
            if (playerAnimation)
                playerAnimation.PlayRun();

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / retreatDuration;
                float k = retreatCurve?.Evaluate(t) ?? t;
                playerTransform.position = Vector3.Lerp(dashEnd, start, k);
                yield return null;
            }

            playerTransform.position = start;

            if (playerAnimation)
                playerAnimation.BackToIdleFromAttack();
        }

        private IEnumerator AIDashAndRetreat(float forwardDuration)
        {
            if (!aiTransform || !playerTransform)
                yield break;

            Vector3 start = _aiStartPos;
            Vector3 dir = playerTransform.position - start;
            if (dir.sqrMagnitude < 0.0001f) yield break;
            dir.Normalize();

            Vector3 dashEnd = playerTransform.position - dir * dashStopDistanceFromTarget;

            if (aIAnimation)
            {
                aIAnimation.PlayAttack();
            }

            // PHASE 1 : dash towards Player
            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / forwardDuration;
                var k = dashCurve?.Evaluate(t) ?? t;
                aiTransform.position = Vector3.Lerp(start, dashEnd, k);
                yield return null;
            }

            aiTransform.position = dashEnd;

            // small contact time at the end of the dash
            yield return new WaitForSeconds(0.05f);

            // PHASE 2 : return to start with run animation
            if (aIAnimation)
                aIAnimation.PlayRun();

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / retreatDuration;
                float k = retreatCurve?.Evaluate(t) ?? t;
                aiTransform.position = Vector3.Lerp(dashEnd, start, k);
                yield return null;
            }

            aiTransform.position = start;

            if (aIAnimation)
                aIAnimation.BackToIdleFromAttack();
        }

        // private IEnumerator PlayerWinSequence()
        // {
        //     // petit délai pour laisser le texte GO / le résultat se poser
        //     yield return new WaitForSeconds(0.05f);
        //
        //     float forwardDuration = dashForwardDuration;
        //     if (playerAnimation != null && playerAnimation.AttackDuration > 0f)
        //         forwardDuration = playerAnimation.AttackDuration * dashAttackDurationMultiplier;
        //
        //     // gagnant : attaque + dash
        //     if (playerAnimation)
        //         playerAnimation.PlayAttack();
        //
        //     if (aIAnimation)
        //         aIAnimation.PlayHurt();
        //
        //     if (_playerMoveRoutine != null)
        //         StopCoroutine(_playerMoveRoutine);
        //
        //     yield return PlayerDashAndRetreat(forwardDuration);
        //
        //     // retour idle propre
        //     if (playerAnimation)
        //         playerAnimation.BackToIdleFromAttack();
        //
        //     if (aIAnimation)
        //         aIAnimation.EndHurt();
        // }

        private IEnumerator PlayerWinSequence()
        {
            // Par sécurité : on stoppe d’éventuels dash en cours
            if (_aiMoveRoutine != null) StopCoroutine(_aiMoveRoutine);
            if (_playerMoveRoutine != null) StopCoroutine(_playerMoveRoutine);

            // Remise à l'état neutre
            if (aIAnimation)
            {
                aIAnimation.ResetAttack();
                aIAnimation.PlayIdle();
            }

            if (playerAnimation) playerAnimation.ResetAttack();

            // Léger délai pour laisser le texte de résultat s'afficher
            yield return null;

            // ATTACK + DASH côté joueur
            float forwardDuration = dashForwardDuration;
            if (playerAnimation && playerAnimation.AttackDuration > 0f)
                forwardDuration = playerAnimation.AttackDuration * dashAttackDurationMultiplier;

            // Le joueur attaque
            if (playerAnimation) playerAnimation.PlayAttack();
            // L'IA prend le coup
            if (aIAnimation) aIAnimation.PlayHurt();

            // Dash + retour
            _playerMoveRoutine = StartCoroutine(PlayerDashAndRetreat(forwardDuration));
        }

        // private IEnumerator AIWinSequence()
        // {
        //     yield return new WaitForSeconds(0.05f);
        //
        //     float forwardDuration = dashForwardDuration;
        //     if (aIAnimation != null && aIAnimation.AttackDuration > 0f)
        //         forwardDuration = aIAnimation.AttackDuration * dashAttackDurationMultiplier;
        //
        //     if (aIAnimation)
        //         aIAnimation.PlayAttack();
        //
        //     if (playerAnimation)
        //         playerAnimation.PlayHurt();
        //
        //     if (_aiMoveRoutine != null)
        //         StopCoroutine(_aiMoveRoutine);
        //
        //     yield return AIDashAndRetreat(forwardDuration);
        //
        //     if (aIAnimation)
        //         aIAnimation.BackToIdleFromAttack();
        //
        //     if (playerAnimation)
        //         playerAnimation.EndHurt();
        // }

        private IEnumerator AIWinSequence()
        {
            if (_aiMoveRoutine != null) StopCoroutine(_aiMoveRoutine);
            if (_playerMoveRoutine != null) StopCoroutine(_playerMoveRoutine);

            if (playerAnimation)
            {
                playerAnimation.ResetAttack();
                playerAnimation.PlayIdle();
            }

            if (aIAnimation) aIAnimation.ResetAttack();

            yield return null;

            float forwardDuration = dashForwardDuration;
            if (aIAnimation && aIAnimation.AttackDuration > 0f)
                forwardDuration = aIAnimation.AttackDuration * dashAttackDurationMultiplier;

            // L'IA attaque
            if (aIAnimation) aIAnimation.PlayAttack();
            // Le joueur prend le coup
            if (playerAnimation) playerAnimation.PlayHurt();

            _aiMoveRoutine = StartCoroutine(AIDashAndRetreat(forwardDuration));
        }
    }
}
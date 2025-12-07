using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

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

        [SerializeField] private TMP_Text endMessage;
        [SerializeField] private UnityEngine.UI.Button restartButton;
        [SerializeField] private UnityEngine.UI.Button menuButton;

        [Header("BO5 Visuals")] [SerializeField]
        private UnityEngine.UI.Image[] playerLives;

        [SerializeField] private UnityEngine.UI.Image[] aiLives;
        [SerializeField] private Sprite fullHeart;
        [SerializeField] private Sprite emptyHeart;

        [Header("Dash Settings")] [SerializeField]
        private Transform playerTransform;
        
        [Header("SFX")]
        [SerializeField] private AudioClip attackSfx;
        [SerializeField] private AudioClip hitSfx;
        [SerializeField] private AudioClip gameOverSfx;

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
            if (input == null || config == null)
            {
                enabled = false;
                return;
            }

            // Initialize state machine and subscribe to its events
            _sm = new DuelStateMachine();
            _sm.OnStateChanged += HandleStateChanged;
            _sm.OnSignal += HandleSignal;
            _sm.OnDuelEnded += HandleResult;

            if (endPanel != null)
            {
                endPanel.SetActive(false);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(RestartBo5);
            }

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(() => SceneManager.LoadScene("Scenes/Menu"));
            }

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
            if (_sm != null && input != null && config != null)
            {
                StartRound();
            }

            _playerScore = 0;
            _aiScore = 0;
            _duelFinished = false;
            UpdateLivesVisual();
            
            MusicManager.Instance?.PlayMusic(MusicTrack.Duel);
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

            ResetCharactersToIdle();

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

            if (_sm.State is DuelState.Signal or DuelState.Resolve)
            {
                MusicManager.PlaySfxStatic(attackSfx);
            }

            if (_sm.State == DuelState.Resolve && _sm.AIAttacked)
            {
                _sm.ResolveWinner();
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

            bool playerReachedMax = _playerScore >= MaxScore;
            bool aiReachedMax = _aiScore >= MaxScore;

            if (playerReachedMax || aiReachedMax)
            {
                _duelFinished = true;

                if (_loop != null)
                {
                    StopCoroutine(_loop);
                    _loop = null;
                }

                input.SetAttackEnabled(false);
                
                MusicManager.Instance?.PlayMusic(MusicTrack.GameOver);

                StartCoroutine(playerReachedMax
                    ? PlayerWinSequence(true)
                    : AIWinSequence(true));

                ShowEndPanel();
                return;
            }

            switch (outcome)
            {
                case DuelOutcome.PlayerWin:
                    StartCoroutine(PlayerWinSequence(false));
                    break;
                case DuelOutcome.AIWin:
                    StartCoroutine(AIWinSequence(false));
                    break;
                case DuelOutcome.FalseStart:
                    StartCoroutine(_sm.PlayerFalseStart ? AIWinSequence(false) : PlayerWinSequence(false));
                    break;
                case DuelOutcome.None:
                case DuelOutcome.Draw:
                case DuelOutcome.NoAttack:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }
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
            if (!endPanel || !endMessage) return;

            endPanel.SetActive(true);
            bool playerwon = _playerScore > _aiScore;

            endMessage.text = playerwon
                ? "YOU WIN ! \nYou're a true duel master !"
                : "AI WINS ! \nYou should have trained more !";
        }

        private void RestartBo5()
        {
            _playerScore = 0;
            _aiScore = 0;
            _duelFinished = false;

            if (endPanel != null)
            {
                endPanel.SetActive(false);
            }

            ResetCharactersToIdle();
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
                MusicManager.PlaySfxStatic(attackSfx);
                _sm.RegisterAIAttack(Time.realtimeSinceStartup);
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

        private IEnumerator PlayerWinSequence(bool isFinalRound)
        {
            if (_aiMoveRoutine != null) StopCoroutine(_aiMoveRoutine);
            if (_playerMoveRoutine != null) StopCoroutine(_playerMoveRoutine);


            if (aIAnimation)
            {
                aIAnimation.ResetAttack();
                aIAnimation.PlayIdle();
            }

            if (playerAnimation)
                playerAnimation.ResetAttack();


            yield return null;


            float forwardDuration = dashForwardDuration;
            if (playerAnimation && playerAnimation.AttackDuration > 0f)
                forwardDuration = playerAnimation.AttackDuration * dashAttackDurationMultiplier;


            if (playerAnimation) playerAnimation.PlayAttack();
            if (aIAnimation)
            {
                MusicManager.PlaySfxStatic(hitSfx);
                aIAnimation.PlayHurt();
            }


            _playerMoveRoutine = StartCoroutine(PlayerDashAndRetreat(forwardDuration));


            if (!isFinalRound)
                yield break;


            if (aIAnimation && aIAnimation.HurtDuration > 0f)
                yield return new WaitForSeconds(aIAnimation.HurtDuration * 0.9f);

            if (aIAnimation) aIAnimation.PlayDeath();
        }

        private IEnumerator AIWinSequence(bool isFinalRound)
        {
            if (_aiMoveRoutine != null) StopCoroutine(_aiMoveRoutine);
            if (_playerMoveRoutine != null) StopCoroutine(_playerMoveRoutine);

            if (playerAnimation)
            {
                playerAnimation.ResetAttack();
                playerAnimation.PlayIdle();
            }

            if (aIAnimation)
                aIAnimation.ResetAttack();

            yield return null;

            float forwardDuration = dashForwardDuration;
            if (aIAnimation && aIAnimation.AttackDuration > 0f)
                forwardDuration = aIAnimation.AttackDuration * dashAttackDurationMultiplier;


            if (aIAnimation) aIAnimation.PlayAttack();
            if (playerAnimation)
            {
                MusicManager.PlaySfxStatic(hitSfx);
                playerAnimation.PlayHurt();
            }


            _aiMoveRoutine = StartCoroutine(AIDashAndRetreat(forwardDuration));

            if (!isFinalRound)
                yield break;

            if (playerAnimation && playerAnimation.HurtDuration > 0f)
                yield return new WaitForSeconds(playerAnimation.HurtDuration * 0.9f);

            if (playerAnimation) playerAnimation.PlayDeath();
        }

        private void ResetCharactersToIdle()
        {
            playerAnimation?.ForceIdleState();
            aIAnimation?.ForceIdleState();
        }
    }
}
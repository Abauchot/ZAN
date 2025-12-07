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

        [Header("UI settings")]
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text playerMsText;
        [SerializeField] private TMP_Text aiMsText;
        
        [Header("Pause UI")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private UnityEngine.UI.Button resumePauseButton;
        [SerializeField] private UnityEngine.UI.Button menuPauseButton;

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

        [Header("Flash FX")] 
        [SerializeField] private CanvasGroup flashCanvas;
        [SerializeField] private float flashDuration = 0.15f;
        [SerializeField] private float flashMaxAlpha = 0.2f;
        
        [Header("SFX")]
        [SerializeField] private AudioClip attackSfx;
        [SerializeField] private AudioClip hitSfx;
        [SerializeField] private AudioClip gameOverSfx;
        [SerializeField] private AudioClip signalSfx;

        [SerializeField] private Transform aiTransform;
        [SerializeField] private float dashStopDistanceFromTarget = 2f;
        [SerializeField] private float dashForwardDuration = 0.12f;
        [SerializeField] private float retreatDuration = 0.18f;
        [SerializeField] private float dashAttackDurationMultiplier = 1.2f;
        [SerializeField] private AnimationCurve dashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve retreatCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        private Vector3 _playerStartPos;
        private Vector3 _aiStartPos;
        private Vector3 _playerStartScale;
        private Vector3 _aiStartScale;
        private Coroutine _playerMoveRoutine;
        private Coroutine _aiMoveRoutine;

        private DuelStateMachine _sm;
        private Coroutine _loop;
        private Coroutine _aiRoutine;
        
        private bool _isPaused;

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
            
            if (pausePanel != null)
                pausePanel.SetActive(false);

            if (resumePauseButton != null)
                resumePauseButton.onClick.AddListener(ResumeFromPause);

            if (menuPauseButton != null)
                menuPauseButton.onClick.AddListener(ReturnToMenuFromPause);

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
                _playerStartScale = playerTransform.localScale;
            }

            if (aiTransform != null)
            {
                _aiStartPos = aiTransform.position;
                _aiStartScale = aiTransform.localScale;
            }
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.OnAttack += OnPlayerAttack;
                input.OnPause += HandlePause;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.OnAttack -= OnPlayerAttack;
                input.OnPause -= HandlePause;
            }

            if (_loop != null) StopCoroutine(_loop);
        }

        private void HandlePause()
        {
            if (_duelFinished) return;        
            if (_isPaused) return;           

            _isPaused = true;

       
            input.SetAttackEnabled(false);
            
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }

            if (_aiRoutine != null)
            {
                StopCoroutine(_aiRoutine);
                _aiRoutine = null;
            }
            
            ResetCharactersToIdle();
            
            if (pausePanel)
                pausePanel.SetActive(true);
            
        }
        
        private void ResumeFromPause()
        {
            if (!_isPaused) return;

            _isPaused = false;

            if (pausePanel)
                pausePanel.SetActive(false);
            
            StartRound();
        }
        
        private void ReturnToMenuFromPause()
        {
            _isPaused = false;

            if (pausePanel)
                pausePanel.SetActive(false);
            Time.timeScale = 1f;

            SceneManager.LoadScene("Menu");
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
            
            HideRoundResultUI();
            
            MusicManager.Instance?.PlayMusic(MusicTrack.Duel);
        }

        /// <summary>
        /// Starts a new duel round
        /// </summary>
        private void StartRound()
        {
            HideRoundResultUI();
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
                    DuelState.Ready   => "GET READY",
                    DuelState.Waiting => "HOLD...",
                    DuelState.Signal  => "DRAW!",
                    _                 => string.Empty
                };
            }
        }

        /// <summary>
        /// Called when signal appears - starts AI reaction coroutine
        /// </summary>
        private void HandleSignal()
        {
            if (signalSfx)
            {
                MusicManager.PlaySfxStatic(signalSfx);
            }

            StartCoroutine(FlashEffect());
            
            if (_aiRoutine != null) StopCoroutine(_aiRoutine);
            _aiRoutine = StartCoroutine(AIReactAfterDelay());
        }

        /// <summary>
        /// Displays the duel outcome and timing results
        /// </summary>
        private void HandleResult(DuelOutcome outcome, float? playerMs)
        {
            float? aiMs = _sm.AIAttacked ? (_sm.AIAttackAtRealTime - _sm.SignalAtRealTime) * 1000f : null;

            if (playerMsText)
            {
                playerMsText.text = string.Empty;
                playerMsText.gameObject.SetActive(false);
            }
            
            if (aiMsText)
            {
                aiMsText.text = string.Empty;
                aiMsText.gameObject.SetActive(false);
            }
            
            string resultLabel = string.Empty;

            switch (outcome)
            {
                case DuelOutcome.PlayerWin:
                    resultLabel = "YOU WIN!";
                    if (playerMsText && playerMs.HasValue)
                    {
                        playerMsText.text = $"{playerMs.Value:0} ms";
                        playerMsText.gameObject.SetActive(true);
                    }
                    break;

                case DuelOutcome.AIWin:
                    resultLabel = "AI WINS!";
                    if (aiMsText && aiMs.HasValue)
                    {
                        aiMsText.text = $"{aiMs.Value:0} ms";
                        aiMsText.gameObject.SetActive(true);
                    }
                    break;

                case DuelOutcome.Draw:
                    resultLabel = "DRAW!";
                    if (playerMsText && playerMs.HasValue)
                    {
                        playerMsText.text = $"{playerMs.Value:0} ms";
                        playerMsText.gameObject.SetActive(true);
                    }
                    if (aiMsText && aiMs.HasValue)
                    {
                        aiMsText.text = $"{aiMs.Value:0} ms";
                        aiMsText.gameObject.SetActive(true);
                    }
                    break;

                case DuelOutcome.FalseStart:
                    resultLabel = _sm.PlayerFalseStart
                        ? "FALSE START! You lose"
                        : "FALSE START! AI loses";
                    if (attackSfx)
                    {
                        MusicManager.PlaySfxStatic(attackSfx);
                    }
                    break;

                case DuelOutcome.NoAttack:
                    resultLabel = "No attack registered.";
                    break;
            }

            if (resultText)
            {
                resultText.gameObject.SetActive(true);
                resultText.text = resultLabel;
            }

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
            if (resultText)
            {
                resultText.gameObject.SetActive(false);
            }
            
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
            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / forwardDuration;
                var k = dashCurve?.Evaluate(t) ?? t;
                playerTransform.position = Vector3.Lerp(start, dashEnd, k);
                yield return null;
            }

            playerTransform.position = dashEnd;
            
            yield return new WaitForSeconds(0.05f);

          
            if (playerAnimation)
            {
                playerAnimation.ResetAttack();
                playerAnimation.PlayRun();
            }

           
            playerTransform.localScale = new Vector3(
                -_playerStartScale.x,
                _playerStartScale.y,
                _playerStartScale.z
            );

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / retreatDuration;
                float k = retreatCurve?.Evaluate(t) ?? t;
                playerTransform.position = Vector3.Lerp(dashEnd, start, k);
                yield return null;
            }

            playerTransform.position = start;
            
            playerTransform.localScale = _playerStartScale;

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
            
            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / forwardDuration;
                var k = dashCurve?.Evaluate(t) ?? t;
                aiTransform.position = Vector3.Lerp(start, dashEnd, k);
                yield return null;
            }

            aiTransform.position = dashEnd;

            yield return new WaitForSeconds(0.05f);
            
            if (aIAnimation)
            {
                aIAnimation.ResetAttack();
                aIAnimation.PlayRun();
            }

            aiTransform.localScale = new Vector3(
                -_aiStartScale.x,
                _aiStartScale.y,
                _aiStartScale.z
            );

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / retreatDuration;
                float k = retreatCurve?.Evaluate(t) ?? t;
                aiTransform.position = Vector3.Lerp(dashEnd, start, k);
                yield return null;
            }

            aiTransform.position = start;
            aiTransform.localScale = _aiStartScale;

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

        private IEnumerator FlashEffect()
        {
            if (!flashCanvas)
                yield break;

            flashCanvas.gameObject.SetActive(true);
            flashCanvas.alpha = 0f;

            float half = flashDuration * 0.5f;
            float t = 0f;
            
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                flashCanvas.alpha = Mathf.Lerp(0f, flashMaxAlpha, k);
                yield return null;
            }
            
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                flashCanvas.alpha = Mathf.Lerp(flashMaxAlpha, 0f, k);
                yield return null;
            }

            flashCanvas.alpha = 0f;
            flashCanvas.gameObject.SetActive(false);
        }
        
        private void HideRoundResultUI()
        {
            if (resultText)
                resultText.gameObject.SetActive(false);

            if (playerMsText)
                playerMsText.gameObject.SetActive(false);

            if (aiMsText)
                aiMsText.gameObject.SetActive(false);
        }


        private void ResetCharactersToIdle()
        {
            playerAnimation?.ForceIdleState();
            aIAnimation?.ForceIdleState();
        }
    }
}
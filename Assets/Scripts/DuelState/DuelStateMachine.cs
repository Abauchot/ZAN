using System;


namespace DuelState
{
    /// <summary>
    /// Core state machine managing the duel logic and transitions.
    /// Tracks attack timing, determines winner, and handles false starts.
    /// </summary>
    public sealed class DuelStateMachine
    {
        // Current state of the duel
        public DuelState State { get; private set; } = DuelState.Ready;
        
        // Attack flags
        public bool PlayerAttacked { get; private set; }
        public bool AIAttacked { get; private set; }
        
        // Timing data (-1f means "not happened yet")
        public float AIAttackAtRealTime { get; private set; } = -1f;
        public float SignalAtRealTime { get; private set; } = -1f;
        public float PlayerAttackAtRealTime { get; private set; } = -1f;
        
        // False start flag (attacking before signal)
        public bool FalseStart { get; private set; }
        public bool PlayerFalseStart { get; private set; }
        
        // Event handlers
        public event Action<DuelState> OnStateChanged;
        public event Action OnSignal;
        // float? is the time difference in seconds, null if no attack
        public event Action<DuelOutcome, float?> OnDuelEnded;
        
        /// <summary>
        /// Changes state and notifies listeners
        /// </summary>
        private void SetState(DuelState newState)
        {
            State = newState;
            OnStateChanged?.Invoke(newState);
        }
        
        /// <summary>
        /// Resets all round data to prepare for a new duel
        /// </summary>
        public void ResetRound()
        {
            PlayerAttacked = false;
            AIAttacked = false;
            FalseStart = false;
            PlayerFalseStart = false;
            SignalAtRealTime = -1f;
            PlayerAttackAtRealTime = -1f;
            AIAttackAtRealTime = -1f;
            SetState(DuelState.Ready);
        }

        /// <summary>
        /// Transitions to Waiting state - players must not attack yet
        /// </summary>
        public void EnterWaiting()
        {
            SetState(DuelState.Waiting);
        }
        
        /// <summary>
        /// Transitions to Signal state and records when signal appeared.
        /// Controller is responsible for transitioning to Resolve after brief delay.
        /// </summary>
        public void EnterSignal(float signalAtRealTime)
        {
            SignalAtRealTime = signalAtRealTime;
            SetState(DuelState.Signal);
            OnSignal?.Invoke();
        }

        /// <summary>
        /// Transitions to Resolve state where attacks can be processed
        /// </summary>
        public void EnterResolve()
        {
            SetState(DuelState.Resolve);
        }

        /// <summary>
        /// Registers a player attack. If during Waiting phase, triggers false start.
        /// </summary>
        public void RegisterPlayerAttack(float realTimeNow)
        {
            // Attacking during waiting phase = false start (instant loss)
            if (State == DuelState.Waiting)
            {
                FalseStart = true;
                PlayerFalseStart = true;
                SetState(DuelState.Results);
                OnDuelEnded?.Invoke(DuelOutcome.FalseStart, null);
                return;
            }
            
            // Valid attack during Signal or Resolve phase
            if (State is not (DuelState.Signal or DuelState.Resolve) || PlayerAttacked) return;
            PlayerAttacked = true;
            PlayerAttackAtRealTime = realTimeNow;
        }
        
        /// <summary>
        /// Registers an AI attack and resolves immediately if in Resolve state.
        /// </summary>
        public void RegisterAIAttack(float realTimeNow, float drawEpsilonMs = 1f)
        {
            // Allow AI attack in Signal or Resolve state (like player)
            if (State != DuelState.Signal && State != DuelState.Resolve) return;
            if (AIAttacked) return;
            AIAttacked = true;
            AIAttackAtRealTime = realTimeNow;
            
            // Resolve immediately after AI attack only if in Resolve state
            if (State == DuelState.Resolve)
            {
                ResolveWinner(drawEpsilonMs);
            }
        }

        /// <summary>
        /// Determines the winner based on attack timing.
        /// Calculates time difference from signal and applies draw threshold.
        /// </summary>
        public void ResolveWinner(float drawEpsilonMs = 1f)
        {
            if(SignalAtRealTime < 0f) return;
            
            // Calculate time difference from signal in milliseconds
            var playerMs = PlayerAttacked ? (PlayerAttackAtRealTime - SignalAtRealTime) * 1000f : (float?)null;
            var aiMs = AIAttacked ? (AIAttackAtRealTime - SignalAtRealTime) * 1000f : (float?)null;

            DuelOutcome outcome;

            // Determine outcome based on attacks and timing
            if (FalseStart)
            {
                outcome = DuelOutcome.FalseStart;
            } else if (PlayerAttacked && AIAttacked)
            {
                // Both attacked - compare times (draw if within epsilon threshold)
                var  diff = Math.Abs(playerMs.Value - aiMs.Value);
                if(diff <= drawEpsilonMs) outcome = DuelOutcome.Draw;
                else outcome = playerMs.Value < aiMs.Value ? DuelOutcome.PlayerWin : DuelOutcome.AIWin;
            }
            else if (PlayerAttacked && !AIAttacked) outcome = DuelOutcome.PlayerWin;
            else if (!PlayerAttacked && AIAttacked) outcome = DuelOutcome.AIWin;
            else outcome = DuelOutcome.NoAttack; // Nobody attacked
            
            SetState(DuelState.Results);
            OnDuelEnded?.Invoke(outcome, playerMs);
        }

        /// <summary>
        /// Forces a false start (used when AI attacks before signal)
        /// </summary>
        public void TriggerFalseStart()
        {
            FalseStart = true;
            SetState(DuelState.Results);
            OnDuelEnded?.Invoke(DuelOutcome.FalseStart, null);
        }
    }
}
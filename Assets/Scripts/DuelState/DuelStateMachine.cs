using System;
using UnityEngine;

namespace DuelState
{
    public sealed class DuelStateMachine
    {
        public DuelState State { get; private set; } = DuelState.Ready;
        
        public bool PlayerAttacked { get; private set; }
        public bool FalseStart { get; private set; }
        
        
        //-1f means "not happened yet"
        public float SignalAtRealTime { get; private set; } = -1f;
        public float PlayerAttackAtRealTime { get; private set; } = -1f;
        
        //event handlers
        public event Action<DuelState> OnStateChanged;
        public event Action OnSignal;
        //float? is the time difference in seconds, null if no attack
        public event Action<DuelOutcome, float?> OnDuelEnded;
        
        private void SetState(DuelState newState)
        {
            State = newState;
            OnStateChanged?.Invoke(newState);
        }
        
        public void ResetRound()
        {
            PlayerAttacked = false;
            FalseStart = false;
            SignalAtRealTime = -1f;
            PlayerAttackAtRealTime = -1f;
            SetState(DuelState.Ready);
        }

        public void EnterWaiting()
        {
            SetState(DuelState.Waiting);
        }
        
        public void EnterSignal(float signalAtRealTime)
        {
            SignalAtRealTime = signalAtRealTime;
            SetState(DuelState.Signal);
            OnSignal?.Invoke();
            //Passing in Resolve state immediately, waiting for attacks
            SetState(DuelState.Resolve);
        }

        public void RegisterPlayerAttack(float realTimeNow)
        {
            //If player attacks during waiting phase, it's a false start
            if (State == DuelState.Waiting)
            {
                FalseStart = true;
                SetState(DuelState.Results);
                OnDuelEnded?.Invoke(DuelOutcome.FalseStart, null);
                return;
            }
            
            //If player attacks during signal or resolve phase, it's a valid attack
            if (State is not (DuelState.Signal or DuelState.Resolve) || PlayerAttacked) return;
            PlayerAttacked = true;
            PlayerAttackAtRealTime = realTimeNow;

            
            if (SignalAtRealTime >= -0f)
            {
                var ms = (PlayerAttackAtRealTime - SignalAtRealTime) * 1000f;
                SetState(DuelState.Results);
                OnDuelEnded?.Invoke(DuelOutcome.PlayerWin, ms);
            }
            else
            {
                SetState(DuelState.Results);
                OnDuelEnded?.Invoke(DuelOutcome.NoAttack, null);
            }
        }


    }
}
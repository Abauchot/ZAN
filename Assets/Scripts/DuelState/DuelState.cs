namespace DuelState
{
    /// <summary>
    /// Represents the different states of a duel round.
    /// Flow: Ready -> Waiting -> Signal -> Resolve -> Results
    /// </summary>
    public enum DuelState
    {
        Ready,      // Initial state, preparing for the next round
        Waiting,    // Waiting for the signal - players must NOT attack yet (false start if they do)
        Signal,     // GO signal displayed - brief moment before resolve phase
        Resolve,    // Both players can attack - winner determined when both attack or timeout
        Results     // Duel ended, showing the outcome
    }
}
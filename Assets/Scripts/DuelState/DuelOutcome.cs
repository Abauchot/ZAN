namespace DuelState
{
    /// <summary>
    /// Represents the possible outcomes of a duel round
    /// </summary>
    public enum DuelOutcome
    {
        None,        // No outcome yet
        FalseStart,  // Player or AI attacked before the signal - instant loss for attacker
        PlayerWin,   // Player attacked faster than AI (or AI didn't attack)
        AIWin,       // AI attacked faster than player (or player didn't attack)
        Draw,        // Both attacked within the draw threshold (very close timing)
        NoAttack     // Neither player nor AI attacked within the timeout
    }
}

using UnityEngine;

namespace DuelState
{
    /// <summary>
    /// ScriptableObject containing configuration parameters for the duel system
    /// </summary>
    [CreateAssetMenu(fileName = "DuelConfig", menuName = "Samurai/Duel Config")]
    public class DuelConfig : ScriptableObject
    {
        [Header("Timings")]
        // Random duration range for the Waiting phase (before signal appears)
        public Vector2 waitingTimeRange = new Vector2(2f, 5f);
        // How long to display results before starting the next round
        public float resultStaySeconds = 1.0f;
        
        [Header("AI Settings")]
        // AI reaction time range in milliseconds (after signal appears)
        public Vector2 aiReactRangeMs = new Vector2(100f, 200f);
        // Probability (0-1) that AI will make a false start and attack before signal
        [Range(0f, 1f)] public float aiFalseStartChance = 0.05f;
        
    }
}
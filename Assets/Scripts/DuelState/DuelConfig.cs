using UnityEngine;

namespace DuelState
{
    [CreateAssetMenu(fileName = "DuelConfig", menuName = "Samurai/Duel Config")]
    public class DuelConfig : ScriptableObject
    {
        [Header("Timings")]
        public Vector2 waitingTimeRange = new Vector2(2f, 5f);
        public float resultStaySeconds = 1.0f;
        
        [Header("AI Settings")]
        public Vector2 aiReactRangeMs = new Vector2(100f, 200f);
        [Range(0f, 1f)] public float aiFalseStartChance = 0.05f;
        
    }
}
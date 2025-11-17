using System;
using System.Linq;
using UnityEngine;

namespace DuelState
{
    /// <summary>
    /// Handles player animation logic, particularly attack animations.
    /// Provides fallback mechanisms to ensure animations play reliably.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int IsAttackHash = Animator.StringToHash("isAttack");
        private static readonly int AttackTriggerHash = Animator.StringToHash("attack");
        private static readonly int AttackStateHash = Animator.StringToHash("Player_Attack");
        private static readonly int RunStateHash = Animator.StringToHash("Player_Run");
        private static readonly int IdleStateHash = Animator.StringToHash("Player_Idle");

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogError("[PlayerAnimationController] Animator component not found!", this);
            }
        }
        
        private bool HasParameter(int paramHash)
        {
            return _animator && _animator.parameters.Any(p => Animator.StringToHash(p.name) == paramHash);
        }

        /// <summary>
        /// Triggers the attack animation using multiple fallback strategies.
        /// </summary>
        public void PlayAttack()
        {
            if (_animator == null) return;
            TrySetBool(IsAttackHash, true);
            TrySetTrigger(AttackTriggerHash);
            TryPlayAttackState();
        }
        
        public void PlayRun()
        {
            if (!_animator) return;

            // Option bool/trigger si tu en as, sinon on force le state
            if (!TryPlayState("Player_Run", 0))
                TryPlayState("Run", 0);
        }

        public void BackToIdleFromAttack()
        {
            if (!_animator) return;

            // On reset le bool d’attaque si tu l’utilises
            TrySetBool(IsAttackHash, false);

            // Et on force l’Idle
            if (!TryPlayState("Player_Idle", 0))
                TryPlayState("Idle", 0);
        }


        /// <summary>
        /// Resets the attack animation state (useful for bool-based setups).
        /// </summary>
        public void ResetAttack()
        {
            if (!_animator) return;
            TrySetBool(IsAttackHash, false);
        }

        private void TrySetBool(int paramHash, bool value)
        {
            if (!_animator) return;
            // Evite d'appeler SetBool si le paramètre n'existe pas (sinon Unity logge un warning).
            if (!HasParameter(paramHash)) return;
            try
            {
                _animator.SetBool(paramHash, value);
            }
            catch (Exception)
            {
                // Si une exception survient, on l'ignore -- comportement précédent conservé.
            }
        }

        private void TrySetTrigger(int paramHash)
        {
            if (_animator == null) return;
            // Evite d'appeler SetTrigger si le paramètre n'existe pas (sinon Unity logge un warning).
            if (!HasParameter(paramHash)) return;
            try
            {
                _animator.SetTrigger(paramHash);
            }
            catch (Exception)
            {
                // Parameter doesn't exist or other error, ignore
            }
        }

        private void TryPlayAttackState()
        {
            if (_animator == null) return;
            var layers = _animator.layerCount;
            
            // Common state name variants to try
            var stateNames = new[]
            {
                "Player_Attack",
                "Attack",
                "Base Layer.Player_Attack",
                "Base Layer.Attack"
            };

            // Try by name first
            foreach (var stateName in stateNames)
            {
                for (var layer = 0; layer < layers; layer++)
                {
                    if (TryPlayState(stateName, layer))
                        return;
                }
            }

            // Try by hash as final fallback
            for (var layer = 0; layer < layers; layer++)
            {
                if (!_animator.HasState(layer, AttackStateHash)) continue;
                try
                {
                    _animator.Play(AttackStateHash, layer, 0f);
                    return;
                }
                catch (Exception)
                {
                    // Continue to next layer
                }
            }
        }

        private bool TryPlayState(string stateName, int layer)
        {
            if (!_animator) return false;
            try
            {
                // Vérifier l'existence de l'état par son hash avant d'appeler Play pour éviter les warnings.
                var stateHash = Animator.StringToHash(stateName);
                if (!_animator.HasState(layer, stateHash)) return false;
                _animator.Play(stateName, layer, 0f);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
     }
 }

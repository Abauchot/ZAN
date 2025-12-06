using UnityEngine;

namespace DuelState
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationController : MonoBehaviour
    {
        [SerializeField] private AnimationClip attackClip;
        [SerializeField] private AnimationClip hurtClip;
        public float AttackDuration => attackClip != null ? attackClip.length : 0.2f;
        public float HurtDuration => hurtClip != null ? hurtClip.length : 0.2f;

        private Animator _animator;

        private static readonly int IsRunHash    = Animator.StringToHash("isRun");
        private static readonly int IsAttackHash = Animator.StringToHash("isAttack");
        private static readonly int IsHurtHash   = Animator.StringToHash("isHurt");

        private void Start()
        {
            PlayIdle();
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }
        
        public void PlayIdle()
        {
            if (!_animator) return;
            _animator.SetBool(IsRunHash, false);
            _animator.SetBool(IsAttackHash, false);
            _animator.SetBool(IsHurtHash, false);
        }

        public void PlayRun()
        {
            if (!_animator) return;
            _animator.SetBool(IsRunHash, true);
            _animator.SetBool(IsAttackHash, false);
            _animator.SetBool(IsHurtHash, false);
        }

        public void PlayAttack()
        {
            if (!_animator) return;
            _animator.SetBool(IsAttackHash, true);
            _animator.SetBool(IsRunHash, false);
            _animator.SetBool(IsHurtHash, false);
            
            CancelInvoke(nameof(EndAttack));
            Invoke(nameof(EndAttack), AttackDuration * 0.9f);
        }

        private void EndAttack()
        {
            if (!_animator) return;
            _animator.SetBool(IsAttackHash, false);
        }

        public void PlayHurt()
        {
            if (!_animator) return;
            Debug.Log("[Anim] PlayHurt sur " + gameObject.name);
            _animator.SetBool(IsHurtHash, true);
            _animator.SetBool(IsAttackHash, false);
            _animator.SetBool(IsRunHash, false);
            CancelInvoke(nameof(EndHurt));
            Invoke(nameof(EndHurt), HurtDuration * 0.9f);
        }

        public void EndHurt()
        {
            if (!_animator) return;
            _animator.SetBool(IsHurtHash, false);
        }
        
        public void ResetAttack()
        {
            EndAttack();
        }
        
        public void BackToIdleFromAttack()
        {
            EndAttack();
            PlayIdle();
        }
    }
}

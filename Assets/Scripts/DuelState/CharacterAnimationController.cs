using UnityEngine;

namespace DuelState
{
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimationController : MonoBehaviour
    {
        [SerializeField] private AnimationClip attackClip;
        [SerializeField] private AnimationClip hurtClip;
        [SerializeField] private AnimationClip deathClip;

        public float AttackDuration => attackClip ? attackClip.length : 0.2f;
        public float HurtDuration   => hurtClip   ? hurtClip.length   : 0.2f;
        public float DeathDuration  => deathClip  ? deathClip.length  : 0.8f;

        private Animator _animator;

        private static readonly int IsRunHash    = Animator.StringToHash("isRun");
        private static readonly int IsAttackHash = Animator.StringToHash("isAttack");
        private static readonly int IsHurtHash   = Animator.StringToHash("isHurt");
        private static readonly int IsDeathHash  = Animator.StringToHash("isDeath");

        // hashes des states du layer 0
        private static readonly int IdleStateHash   = Animator.StringToHash("Base_Idle");
        private static readonly int RunStateHash    = Animator.StringToHash("Base_Run");
        private static readonly int AttackStateHash = Animator.StringToHash("Base_Attack");
        private static readonly int HurtStateHash   = Animator.StringToHash("Base_Hurt");
        private static readonly int DeathStateHash  = Animator.StringToHash("Base_Death");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            PlayIdle();
        }

        public void PlayIdle()
        {
            if (!_animator) return;

            _animator.SetBool(IsRunHash, false);
            _animator.SetBool(IsAttackHash, false);
            _animator.SetBool(IsHurtHash, false);

            // force le passage immédiat en Idle
            _animator.CrossFade(IdleStateHash, 0.05f, 0, 0f);
        }

        public void PlayRun()
        {
            if (!_animator) return;

            _animator.SetBool(IsRunHash, true);
            _animator.SetBool(IsAttackHash, false);
            _animator.SetBool(IsHurtHash, false);

            // force le passage immédiat en Run
            _animator.CrossFade(RunStateHash, 0.05f, 0, 0f);
        }

        public void PlayAttack()
        {
            if (!_animator) return;

            _animator.SetBool(IsAttackHash, true);
            _animator.SetBool(IsRunHash, false);
            _animator.SetBool(IsHurtHash, false);

            // attaque tout de suite
            _animator.CrossFade(AttackStateHash, 0.05f, 0, 0f);

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

            _animator.SetBool(IsHurtHash, true);
            _animator.SetBool(IsAttackHash, false);
            _animator.SetBool(IsRunHash, false);

            _animator.CrossFade(HurtStateHash, 0.05f, 0, 0f);

            CancelInvoke(nameof(EndHurt));
            Invoke(nameof(EndHurt), HurtDuration * 0.9f);
        }

        private void EndHurt()
        {
            if (!_animator) return;
            _animator.SetBool(IsHurtHash, false);
        }

        public void PlayDeath()
        {
            if (!_animator) return;

            CancelInvoke(); // on coupe les Invokes d'attack / hurt

            _animator.SetBool(IsRunHash, false);
            _animator.SetBool(IsAttackHash, false);
            _animator.SetBool(IsHurtHash, false);

            _animator.ResetTrigger(IsDeathHash);
            _animator.SetTrigger(IsDeathHash);

            _animator.CrossFade(DeathStateHash, 0.05f, 0, 0f);
        }

        public void ResetAttack()
        {
            CancelInvoke(nameof(EndAttack));
            EndAttack();
        }

        public void BackToIdleFromAttack()
        {
            ResetAttack();
            PlayIdle();
        }

        public void ForceIdleState()
        {
            if (!_animator) return;

            _animator.Rebind();
            _animator.Update(0f);
            _animator.Play(IdleStateHash, 0, 0f);
            _animator.Update(0f);
        }
    }
}

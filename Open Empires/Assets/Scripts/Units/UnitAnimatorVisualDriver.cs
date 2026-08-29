using System.Collections;
using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// View-only bridge between deterministic unit state and an Animator. This component never
    /// writes to UnitData and never participates in movement, combat, damage, or networking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitAnimatorVisualDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int InCombatId = Animator.StringToHash("InCombat");
        private static readonly int IsChargingId = Animator.StringToHash("IsCharging");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DeathId = Animator.StringToHash("Death");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform spearAttachment;
        [SerializeField, Min(0f)] private float deathPresentationDuration = 1.35f;
        [SerializeField, Min(0f)] private float spearReleaseTime = 0.65f;

        private bool initialized;
        private bool deathTriggered;
        private Coroutine spearReleaseRoutine;

        public Animator Animator => animator;
        public bool IsInitialized => initialized;
        public bool IsDeathPresentationActive => deathTriggered;
        public float DeathPresentationDuration => deathPresentationDuration;

        public void Initialize()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                Debug.LogError($"{nameof(UnitAnimatorVisualDriver)} on {name} requires an Animator.", this);
                enabled = false;
                return;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            CacheSpearAttachment();
            initialized = true;
        }

        private void Awake()
        {
            if (!initialized)
                Initialize();
        }

        public void UpdatePresentation(float normalizedSpeed, bool isCharging, bool inCombat)
        {
            if (!initialized || deathTriggered) return;
            animator.SetFloat(SpeedId, Mathf.Clamp01(normalizedSpeed), 0.1f, Time.deltaTime);
            animator.SetBool(IsChargingId, isCharging);
            // InCombat selects only the visual guard/Block presentation.
            animator.SetBool(InCombatId, inCombat);
        }

        public void PlayAttack()
        {
            if (!initialized || deathTriggered) return;
            animator.ResetTrigger(HitId);
            animator.SetTrigger(AttackId);
        }

        public void PlayHit()
        {
            if (!initialized || deathTriggered) return;
            animator.ResetTrigger(AttackId);
            animator.SetTrigger(HitId);
        }

        public void PlayDeath()
        {
            if (!initialized || deathTriggered) return;
            deathTriggered = true;
            animator.ResetTrigger(AttackId);
            animator.ResetTrigger(HitId);
            animator.SetBool(InCombatId, false);
            animator.SetBool(IsChargingId, false);
            animator.SetFloat(SpeedId, 0f);
            animator.SetTrigger(DeathId);

            if (spearAttachment != null && isActiveAndEnabled)
                spearReleaseRoutine = StartCoroutine(ReleaseSpearVisual());
        }

        private void CacheSpearAttachment()
        {
            if (spearAttachment != null) return;
            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == "SpearAttachment")
                {
                    spearAttachment = transforms[i];
                    return;
                }
            }
        }

        private IEnumerator ReleaseSpearVisual()
        {
            yield return new WaitForSeconds(spearReleaseTime);
            if (spearAttachment == null) yield break;

            spearAttachment.SetParent(transform, true);
            Vector3 startPosition = spearAttachment.position;
            Quaternion startRotation = spearAttachment.rotation;
            const float duration = 0.55f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                spearAttachment.position = startPosition + Vector3.down * (0.28f * t * t);
                spearAttachment.rotation = startRotation * Quaternion.Euler(0f, 0f, 78f * t);
                yield return null;
            }
            spearReleaseRoutine = null;
        }

        private void OnDisable()
        {
            if (spearReleaseRoutine != null)
            {
                StopCoroutine(spearReleaseRoutine);
                spearReleaseRoutine = null;
            }
        }
    }
}

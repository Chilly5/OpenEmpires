using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenEmpires
{
    /// <summary>Keyboard-only presentation controls for SpearmanAnimationTest.unity.</summary>
    public sealed class SpearmanAnimationTestController : MonoBehaviour
    {
        private UnitAnimatorVisualDriver[] drivers;

        private void Start()
        {
            drivers = FindObjectsByType<UnitAnimatorVisualDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void Update()
        {
            if (drivers == null) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) Play("Idle");
            if (keyboard.digit2Key.wasPressedThisFrame) Play("Walk");
            if (keyboard.digit3Key.wasPressedThisFrame) Play("RunCharge");
            if (keyboard.digit4Key.wasPressedThisFrame) TriggerAttack();
            if (keyboard.digit5Key.wasPressedThisFrame) Play("Block");
            if (keyboard.digit6Key.wasPressedThisFrame) TriggerHit();
            if (keyboard.digit7Key.wasPressedThisFrame) TriggerDeath();
        }

        private void Play(string state)
        {
            for (int i = 0; i < drivers.Length; i++)
            {
                var animator = drivers[i] != null ? drivers[i].Animator : null;
                if (animator != null) animator.Play(state, 0, 0f);
            }
        }

        private void TriggerAttack()
        {
            for (int i = 0; i < drivers.Length; i++) drivers[i]?.PlayAttack();
        }

        private void TriggerHit()
        {
            for (int i = 0; i < drivers.Length; i++) drivers[i]?.PlayHit();
        }

        private void TriggerDeath()
        {
            for (int i = 0; i < drivers.Length; i++) drivers[i]?.PlayDeath();
        }
    }
}

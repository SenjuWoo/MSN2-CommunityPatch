using System;
using System.Reflection;
using _Game.Scripts;
using DG.Tweening;
using Game.Input;
using HarmonyLib;
using UnityEngine;

namespace MSN2CommunityPatch.Fixes
{
    // Vanilla PlayerAnimationsController.StepInsideAndRotate (hide in / leave a wardrobe) moves the player with a placeholder
    // DOMove(Vector3.zero) tween and starts a second tween from its OnPlay to override it. At low frame rates the two tweens
    // fight over the transform: measured player jumps of 4 m per frame at ~9 FPS and 11 m at ~3 FPS, and the player can end up
    // off target (the "doors throw me off the map" reports). Same steps, timing and easing, one tween per movement.
    public static class WardrobeTweenFix
    {
        private static readonly AccessTools.FieldRef<PlayerAnimationsController, FirstPersonController> Fpc =
            AccessTools.FieldRefAccess<PlayerAnimationsController, FirstPersonController>("firstPersonController");
        private static MethodInfo _setBorders;

        public static void Apply(Harmony h)
        {
            var m = AccessTools.Method(typeof(PlayerAnimationsController), "StepInsideAndRotate");
            _setBorders = AccessTools.Method(typeof(PlayerAnimationsController), "SetRotateBorders");
            if (m == null || _setBorders == null) { Plugin.Log.LogWarning("[fix] WardrobeTween: StepInsideAndRotate/SetRotateBorders not found; skipped"); return; }
            h.Patch(m, prefix: new HarmonyMethod(typeof(WardrobeTweenFix), nameof(Prefix)));
            Plugin.Log.LogInfo("[fix] WardrobeTween active");
        }

        private static void Borders(PlayerAnimationsController c, bool move, float top = 0f, float bottom = 0f, float left = 0f, float right = 0f) =>
            _setBorders.Invoke(c, new object[] { move, top, bottom, left, right });

        private static bool Prefix(PlayerAnimationsController __instance, Transform interactionPoint, Transform lookAtPoint, bool isHidden)
        {
            FirstPersonController fpc;
            try { fpc = Fpc(__instance); } catch (Exception e) { Plugin.Log.LogError($"[fix] WardrobeTween: vanilla kept ({e.Message})"); return true; }
            if (!fpc || !interactionPoint) return true;

            Borders(__instance, false, 15f, -15f, -25f, 25f);
            fpc.LookAtDirection = lookAtPoint;
            var tr = __instance.transform;
            var seq = DOTween.Sequence();
            if (isHidden)
            {
                // leaving: face out of the wardrobe, then step 1 m forward
                seq.Append(tr.DORotate(new Vector3(0f, 0f - Vector3.SignedAngle(-interactionPoint.forward, tr.forward, Vector3.up), 0f), 0.5f, RotateMode.WorldAxisAdd).SetEase(Ease.Linear));
                AppendStep(seq, tr, null, 0.5f);
            }
            else
            {
                // entering: walk to the wardrobe's point, face in, step 1 m forward, turn round
                var horizontal = new Vector3(interactionPoint.position.x, tr.position.y, interactionPoint.position.z);
                seq.Append(tr.DOMove(horizontal, 0.25f).SetEase(Ease.Linear));
                seq.Append(tr.DORotate(new Vector3(0f, 0f - Vector3.SignedAngle(interactionPoint.forward, tr.forward, Vector3.up), 0f), 0.25f, RotateMode.WorldAxisAdd).SetEase(Ease.Linear));
                AppendStep(seq, tr, horizontal, 0.25f);
                seq.Append(tr.DORotate(new Vector3(0f, 180f, 0f), 0.25f, RotateMode.WorldAxisAdd).SetEase(Ease.InOutSine));
            }
            seq.Play().OnComplete(() => { if (isHidden) Borders(__instance, true); });
            return false;
        }

        // One forward step of 1 m, evaluated when the step starts (after the preceding rotation), as vanilla's inner tween did.
        private static void AppendStep(Sequence seq, Transform tr, Vector3? anchor, float seconds)
        {
            Vector3 from = default, to = default;
            seq.AppendCallback(() => { from = tr.position; to = (anchor ?? tr.position) + tr.forward; });
            seq.Append(DOVirtual.Float(0f, 1f, seconds, t => tr.position = Vector3.LerpUnclamped(from, to, t)).SetEase(Ease.InOutSine));
        }
    }
}

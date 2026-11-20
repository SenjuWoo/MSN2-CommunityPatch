using HarmonyLib;
using UnityEngine;

namespace MSN2Expansion.Systems
{
    // Difficulty presets (MSN1 DifficultyLevelSettingsSO), layered on top of the game's own Easy/Normal/Hard setting:
    // NPC visual detection (EyesSensor alert time), the player's noise (NoiseController.SendNoise) and police detection
    // (CopActorEntity.DetectionValueModifier). "Vanilla" leaves all three untouched.
    public static class Difficulty
    {
        public static float Detection => Mult();
        public static float Noise => Mult();
        public static float Police => Mult();

        public static float Mult()
        {
            switch (SystemsConfig.Difficulty?.Value)
            {
                case "Easy": return 0.7f;
                case "Hard": return 1.4f;
                default: return 1f;
            }
        }

        public static void Patch(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(EyesSensor), "GetAlertTime"), postfix: new HarmonyMethod(typeof(Difficulty), nameof(AlertTimePostfix)));
            h.Patch(AccessTools.PropertyGetter(typeof(Game.Entities.CopActorEntity), nameof(Game.Entities.CopActorEntity.DetectionValueModifier)), postfix: new HarmonyMethod(typeof(Difficulty), nameof(PolicePostfix)));
            // noise: Rain.NoisePrefix applies Difficulty.Noise as well (one prefix on SendNoise)
        }

        // alert time is how long the NPC must see the player: faster detection = shorter time
        private static void AlertTimePostfix(ref float __result) { var m = Detection; if (m != 1f) __result /= m; }

        private static void PolicePostfix(ref float __result) { var m = Police; if (m != 1f) __result *= m; }
    }
}

using System.Collections.Generic;
using System.Linq;
using Game.MapPoints;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSN2CommunityPatch.Fixes
{
    // Vanilla theft tasks put the item on a random free TheftMapPoint. 12 of the 148 points in build 21130045 sit inside furniture colliders
    // (armchairs, a utility cart, a gym shelf, a corner cabinet, kitchen blocker colliders): the player's interaction ray can never reach an item
    // there, even with every drawer/door opened (measured from 100+ standing/crouching positions per point, containers opened as a player would).
    // Vanilla treats a point with a child object as "in use" (TheftChallengeSO.PrepareChallengeForPlay), so an empty marker child is enough to
    // keep both vanilla tasks and the Expansion's souvenirs off these points. shortcut: relies on that childCount test; if the game stops using it, switch to a transpiler.
    public static class TheftSpotFix
    {
        public const string Marker = "MSN2_UnreachableSpot";

        private static readonly HashSet<string> Unreachable = new HashSet<string>
        {
            "TP_EmmaKitchenSpot5", "TP_EmmaKitchenSpot6", "TP_EmmaKitchenSpot9", "TP_EmmaKitchenSpot10",
            "TP_EmmaGarageSpot1", "TP_EmmaGarageSpot2", "TP_EmmaBedroomSpot3", "TP_EmmaLivingRoomSpot4",
            "TP_CamilaGymSpot2", "TP_CamilaBedroomSpot1", "TP_CamilaKitchenSpot4", "TP_ChloeCorridorSpot1",
        };

        public static bool IsUnreachable(string pointId) => Unreachable.Contains(pointId);

        public static int Mark(MapPointsController mpc)
        {
            int n = 0;
            foreach (var p in mpc.MapPoints.OfType<TheftMapPoint>())
            {
                if (!Unreachable.Contains(p.ID.ToString()) || p.transform.Find(Marker)) continue;
                new GameObject(Marker).transform.SetParent(p.transform, false);
                n++;
            }
            return n;
        }
    }

    // Per-scene one-shot jobs for the Game scene, run once its controllers have initialised.
    public class SceneRuntime : MonoBehaviour
    {
        private int _doneScene = -1;
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "Game") Controls.HintSync.Tick();
            if (scene.name != "Game" || _doneScene == scene.handle) return;
            var mpc = FindFirstObjectByType<MapPointsController>();
            if (mpc == null || !mpc.IsInitialized || mpc.MapPoints == null || mpc.MapPoints.Length == 0) return;
            _doneScene = scene.handle;
            if (Plugin.TheftSpots.Value)
                Plugin.Log.LogInfo($"[fix] TheftSpots: {TheftSpotFix.Mark(mpc)} unreachable theft spots taken out of rotation ({mpc.MapPoints.OfType<TheftMapPoint>().Count()} total)");
        }
    }
}

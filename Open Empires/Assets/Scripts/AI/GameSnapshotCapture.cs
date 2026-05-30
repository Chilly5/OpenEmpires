using UnityEngine;

namespace OpenEmpires
{
    // Renders an off-screen, top-down "tactical" snapshot of the battlefield centered on
    // the AI teammate's action (its army, or its base if it has no army yet) and returns
    // PNG bytes for the LLM's image input.
    //
    // Owner-client only and READ-ONLY on sim state — it only reads unit/building positions
    // to aim the camera, never mutates anything, so it cannot affect lockstep determinism.
    // Self-bootstrapping: access via Instance; it creates its own GameObject + camera.
    public class GameSnapshotCapture : MonoBehaviour
    {
        private static GameSnapshotCapture instance;
        public static GameSnapshotCapture Instance
        {
            get
            {
                // `== null` also catches Unity's "destroyed object" fake-null on scene reload.
                if (instance == null)
                {
                    var go = new GameObject("GameSnapshotCapture");
                    instance = go.AddComponent<GameSnapshotCapture>();
                }
                return instance;
            }
        }

        private Camera cam;
        private RenderTexture rt;
        private Texture2D readTex;
        private int builtResolution;

        // Captures a top-down PNG framed on the AI's action. resolution = square pixel size;
        // worldHalfSize = orthographic size (half the vertical world extent shown).
        // Returns null if there is nothing to render (no sim).
        public byte[] CapturePng(GameSimulation sim, int aiPlayerId, int resolution, float worldHalfSize)
        {
            if (sim == null) return null;
            resolution = Mathf.Clamp(resolution, 128, 2048);

            EnsureResources(resolution);
            Vector3 center = ComputeCenter(sim, aiPlayerId);

            cam.transform.position = new Vector3(center.x, 200f, center.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(5f, worldHalfSize);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;

            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render(); // manual render; cam.enabled stays false so it never draws to screen
            RenderTexture.active = rt;
            readTex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            readTex.Apply(false);
            RenderTexture.active = prevActive;
            cam.targetTexture = null;

            return readTex.EncodeToPNG();
        }

        private void EnsureResources(int resolution)
        {
            if (cam == null)
            {
                cam = gameObject.GetComponent<Camera>();
                if (cam == null) cam = gameObject.AddComponent<Camera>();
                // Copy the player camera's layer mask / clear flags / background so the
                // snapshot shows the same world the human sees, then override to top-down.
                var main = Camera.main;
                if (main != null) cam.CopyFrom(main);
                cam.enabled = false; // we drive it with manual Render() only
                cam.depth = -100;    // never composite over the real view even if enabled
            }

            if (rt == null || builtResolution != resolution)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(resolution, resolution, 24);
                readTex = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
                builtResolution = resolution;
            }
        }

        // Centroid of the AI's living military units; falls back to its town center, then
        // world origin. Mirrors the unit-class filtering used by LlmStateExtractor.
        private static Vector3 ComputeCenter(GameSimulation sim, int aiPlayerId)
        {
            var units = sim.UnitRegistry.GetAllUnits();
            double sx = 0, sz = 0;
            int n = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.PlayerId != aiPlayerId) continue;
                if (u.State == UnitState.Dead) continue;
                if (u.UnitType == 0 || u.UnitType == 4 || u.IsSheep) continue; // skip villagers, scouts, sheep
                var p = u.SimPosition.ToVector3();
                sx += p.x;
                sz += p.z;
                n++;
            }
            if (n > 0) return new Vector3((float)(sx / n), 0f, (float)(sz / n));

            if (sim.FirstTownCenterIds.TryGetValue(aiPlayerId, out int tcId))
            {
                var tc = sim.BuildingRegistry.GetBuilding(tcId);
                if (tc != null && !tc.IsDestroyed) return tc.SimPosition.ToVector3();
            }
            return Vector3.zero;
        }

        private void OnDestroy()
        {
            if (rt != null) rt.Release();
        }
    }
}

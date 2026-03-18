using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class MeteorVisualManager : MonoBehaviour
    {
        private Dictionary<int, GameObject> warningMarks = new Dictionary<int, GameObject>();
        private int nextWarningId;
        private Dictionary<int, UnitView> unitViews;

        public void Initialize(Dictionary<int, UnitView> unitViews)
        {
            this.unitViews = unitViews;
        }

        public void HandleMeteorWarning(int playerId, FixedVector3 position, int impactTick)
        {
            Vector3 worldPos = position.ToVector3();
            worldPos.y = GetTerrainHeight(worldPos) + 0.05f;

            int id = nextWarningId++;
            var sim = GameBootstrapper.Instance?.Simulation;
            float radius = sim != null ? sim.Config.MeteorRadius : 5f;
            float warningSeconds = sim != null ? sim.Config.MeteorWarningTicks / (float)sim.Config.TickRate : 3f;

            var mark = CreateWarningMark(worldPos, radius, new Color(1f, 0.15f, 0.1f, 0.35f));
            warningMarks[id] = mark;

            StartCoroutine(WarningPulse(id, mark, warningSeconds));
        }

        public void HandleMeteorImpact(FixedVector3 position, List<int> knockedUnitIds)
        {
            Vector3 worldPos = position.ToVector3();
            worldPos.y = GetTerrainHeight(worldPos);

            StartCoroutine(MeteorFall(worldPos));

            // Trigger knockback arcs on affected units
            if (unitViews != null)
            {
                for (int i = 0; i < knockedUnitIds.Count; i++)
                {
                    int unitId = knockedUnitIds[i];
                    if (unitViews.TryGetValue(unitId, out var view) && view != null)
                    {
                        var sim = GameBootstrapper.Instance?.Simulation;
                        if (sim != null)
                        {
                            var unitData = sim.UnitRegistry.GetUnit(unitId);
                            if (unitData != null)
                            {
                                Vector3 startPos = view.transform.position;
                                Vector3 endPos = unitData.SimPosition.ToVector3();
                                endPos.y = GetTerrainHeight(endPos);
                                view.TriggerMeteorKnockback(startPos, endPos);
                            }
                        }
                    }
                }
            }
        }

        private IEnumerator WarningPulse(int id, GameObject mark, float duration)
        {
            float elapsed = 0f;
            var renderer = mark.GetComponent<Renderer>();
            Vector3 baseScale = mark.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Pulse effect: scale oscillates and alpha increases
                float pulse = 1f + 0.1f * Mathf.Sin(t * Mathf.PI * 8f);
                mark.transform.localScale = baseScale * pulse;

                if (renderer != null)
                {
                    var col = renderer.material.color;
                    col.a = Mathf.Lerp(0.2f, 0.6f, t);
                    renderer.material.color = col;
                }

                yield return null;
            }

            // Clean up warning mark
            if (warningMarks.ContainsKey(id))
                warningMarks.Remove(id);
            if (mark != null)
                Destroy(mark);
        }

        private IEnumerator MeteorFall(Vector3 targetPos)
        {
            // Create meteor sphere
            var meteor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteor.name = "Meteor";
            meteor.transform.localScale = Vector3.one * 1.5f;

            var collider = meteor.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = meteor.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(1f, 0.4f, 0.1f, 1f);
                renderer.material = mat;
            }

            Vector3 startPos = targetPos + Vector3.up * 50f;
            float fallDuration = 0.3f;
            float elapsed = 0f;

            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                // Accelerating fall
                float eased = t * t;
                meteor.transform.position = Vector3.Lerp(startPos, targetPos, eased);
                yield return null;
            }

            meteor.transform.position = targetPos;

            // Camera shake
            var cam = Object.FindFirstObjectByType<RTSCameraController>();
            if (cam != null)
            {
                StartCoroutine(CameraShake(cam, 0.4f, 0.5f));
            }

            // Spawn explosion particles
            SpawnExplosion(targetPos);

            // Play SFX
            SFXManager.Instance?.Play(SFXType.ArrowImpact, targetPos, 1.0f);

            Destroy(meteor);
        }

        private IEnumerator CameraShake(RTSCameraController cam, float duration, float magnitude)
        {
            float elapsed = 0f;
            Vector3 originalPos = cam.transform.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / duration);
                float x = Random.Range(-1f, 1f) * magnitude * t;
                float z = Random.Range(-1f, 1f) * magnitude * t;
                cam.transform.localPosition = originalPos + new Vector3(x, 0f, z);
                yield return null;
            }

            cam.transform.localPosition = originalPos;
        }

        private void SpawnExplosion(Vector3 position)
        {
            // Create a burst of small cubes as particles
            for (int i = 0; i < 20; i++)
            {
                var particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                particle.name = "MeteorDebris";
                particle.transform.position = position;
                particle.transform.localScale = Vector3.one * Random.Range(0.15f, 0.4f);

                var col = particle.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var renderer = particle.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    mat.color = new Color(
                        Random.Range(0.8f, 1f),
                        Random.Range(0.2f, 0.5f),
                        Random.Range(0f, 0.15f),
                        1f);
                    renderer.material = mat;
                }

                StartCoroutine(AnimateDebris(particle, position));
            }
        }

        private IEnumerator AnimateDebris(GameObject particle, Vector3 origin)
        {
            Vector3 velocity = new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(4f, 10f),
                Random.Range(-5f, 5f));

            float lifetime = Random.Range(0.5f, 1.2f);
            float elapsed = 0f;

            while (elapsed < lifetime && particle != null)
            {
                elapsed += Time.deltaTime;
                velocity.y -= 15f * Time.deltaTime; // gravity
                particle.transform.position += velocity * Time.deltaTime;
                particle.transform.Rotate(velocity * 100f * Time.deltaTime);

                // Fade out by shrinking
                float t = elapsed / lifetime;
                particle.transform.localScale *= (1f - Time.deltaTime * 2f);

                yield return null;
            }

            if (particle != null) Destroy(particle);
        }

        // ========== HEALING RAIN ==========

        private Dictionary<int, GameObject> healingRainZones = new Dictionary<int, GameObject>();
        private int nextHealingRainId;

        public void HandleHealingRainWarning(int playerId, FixedVector3 position, int startTick, int endTick)
        {
            Vector3 worldPos = position.ToVector3();
            worldPos.y = GetTerrainHeight(worldPos) + 0.05f;

            var sim = GameBootstrapper.Instance?.Simulation;
            float radius = sim != null ? sim.Config.HealingRainRadius : 6f;
            float warningSeconds = sim != null ? sim.Config.HealingRainWarningTicks / (float)sim.Config.TickRate : 1f;
            float durationSeconds = sim != null ? sim.Config.HealingRainDurationTicks / (float)sim.Config.TickRate : 10f;

            int id = nextHealingRainId++;
            var mark = CreateWarningMark(worldPos, radius, new Color(0.1f, 0.8f, 0.2f, 0.35f));
            healingRainZones[id] = mark;

            StartCoroutine(HealingRainLifecycle(id, mark, worldPos, radius, warningSeconds, durationSeconds));
        }

        public void HandleHealingRainEnd(FixedVector3 position)
        {
            // Cleanup handled by coroutine
        }

        private IEnumerator HealingRainLifecycle(int id, GameObject mark, Vector3 worldPos, float radius, float warningSeconds, float durationSeconds)
        {
            // Warning pulse phase
            float elapsed = 0f;
            var renderer = mark.GetComponent<Renderer>();
            Vector3 baseScale = mark.transform.localScale;

            while (elapsed < warningSeconds)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / warningSeconds;
                float pulse = 1f + 0.1f * Mathf.Sin(t * Mathf.PI * 8f);
                mark.transform.localScale = baseScale * pulse;
                if (renderer != null)
                {
                    var col = renderer.material.color;
                    col.a = Mathf.Lerp(0.2f, 0.5f, t);
                    renderer.material.color = col;
                }
                yield return null;
            }

            // Active healing phase - green zone with rising particles
            if (renderer != null)
            {
                renderer.material.color = new Color(0.1f, 0.8f, 0.2f, 0.3f);
            }
            mark.transform.localScale = baseScale;

            elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.deltaTime;

                // Spawn occasional rising green cubes
                if (Random.value < 0.3f)
                {
                    Vector3 particlePos = worldPos + new Vector3(
                        Random.Range(-radius, radius), 0f, Random.Range(-radius, radius));
                    particlePos.y = GetTerrainHeight(particlePos);
                    StartCoroutine(HealingParticle(particlePos));
                }

                yield return null;
            }

            // Cleanup
            if (healingRainZones.ContainsKey(id))
                healingRainZones.Remove(id);
            if (mark != null)
                Destroy(mark);
        }

        private IEnumerator HealingParticle(Vector3 startPos)
        {
            var particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            particle.name = "HealParticle";
            particle.transform.position = startPos;
            particle.transform.localScale = Vector3.one * Random.Range(0.1f, 0.25f);

            var col = particle.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = particle.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(0.2f, 1f, 0.3f, 0.8f);
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                rend.material = mat;
            }

            float lifetime = Random.Range(0.8f, 1.5f);
            float elapsed = 0f;
            float riseSpeed = Random.Range(1.5f, 3f);

            while (elapsed < lifetime && particle != null)
            {
                elapsed += Time.deltaTime;
                particle.transform.position += Vector3.up * riseSpeed * Time.deltaTime;
                particle.transform.localScale *= (1f - Time.deltaTime * 1.5f);
                yield return null;
            }

            if (particle != null) Destroy(particle);
        }

        // ========== LIGHTNING STORM ==========

        public void HandleLightningStormWarning(int playerId, FixedVector3 position)
        {
            Vector3 worldPos = position.ToVector3();
            worldPos.y = GetTerrainHeight(worldPos) + 0.05f;

            var sim = GameBootstrapper.Instance?.Simulation;
            float radius = sim != null ? sim.Config.LightningStormRadius : 8f;
            float warningSeconds = sim != null ? sim.Config.LightningStormWarningTicks / (float)sim.Config.TickRate : 2f;

            int id = nextWarningId++;
            var mark = CreateWarningMark(worldPos, radius, new Color(0.6f, 0.4f, 1f, 0.35f));
            warningMarks[id] = mark;

            StartCoroutine(WarningPulse(id, mark, warningSeconds));
        }

        public void HandleLightningBolt(FixedVector3 position, List<int> knockedUnitIds)
        {
            Vector3 worldPos = position.ToVector3();
            worldPos.y = GetTerrainHeight(worldPos);

            StartCoroutine(LightningBoltVisual(worldPos));

            // Trigger knockback arcs on affected units
            if (unitViews != null)
            {
                for (int i = 0; i < knockedUnitIds.Count; i++)
                {
                    int unitId = knockedUnitIds[i];
                    if (unitViews.TryGetValue(unitId, out var view) && view != null)
                    {
                        var sim = GameBootstrapper.Instance?.Simulation;
                        if (sim != null)
                        {
                            var unitData = sim.UnitRegistry.GetUnit(unitId);
                            if (unitData != null)
                            {
                                Vector3 startPos = view.transform.position;
                                Vector3 endPos = unitData.SimPosition.ToVector3();
                                endPos.y = GetTerrainHeight(endPos);
                                view.TriggerMeteorKnockback(startPos, endPos);
                            }
                        }
                    }
                }
            }
        }

        public void HandleLightningStormEnd(FixedVector3 position)
        {
            // Cleanup handled by bolt coroutines
        }

        private IEnumerator LightningBoltVisual(Vector3 targetPos)
        {
            // Create lightning bolt as a tall thin cylinder from sky to ground
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bolt.name = "LightningBolt";
            float boltHeight = 30f;
            bolt.transform.position = targetPos + Vector3.up * (boltHeight / 2f);
            bolt.transform.localScale = new Vector3(0.3f, boltHeight / 2f, 0.3f);

            var col = bolt.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = bolt.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(0.9f, 0.9f, 1f, 1f);
                rend.material = mat;
            }

            // Flash at impact point
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "LightningFlash";
            flash.transform.position = targetPos;
            flash.transform.localScale = Vector3.one * 3f;

            var flashCol = flash.GetComponent<Collider>();
            if (flashCol != null) Destroy(flashCol);

            var flashRend = flash.GetComponent<Renderer>();
            if (flashRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(1f, 1f, 1f, 0.8f);
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                flashRend.material = mat;
            }

            // Camera micro-shake
            var cam = Object.FindFirstObjectByType<RTSCameraController>();
            if (cam != null)
                StartCoroutine(CameraShake(cam, 0.15f, 0.2f));

            SFXManager.Instance?.Play(SFXType.ArrowImpact, targetPos, 0.7f);

            // Fade out
            float fadeTime = 0.15f;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeTime;

                if (rend != null)
                    rend.material.color = new Color(0.9f, 0.9f, 1f, 1f - t);
                if (flashRend != null)
                    flashRend.material.color = new Color(1f, 1f, 1f, 0.8f * (1f - t));

                flash.transform.localScale = Vector3.one * (3f + t * 2f);

                yield return null;
            }

            Destroy(bolt);
            Destroy(flash);
        }

        // ========== TSUNAMI ==========

        public void HandleTsunamiWarning(int playerId, FixedVector3 origin, FixedVector3 direction, int impactTick)
        {
            Vector3 worldOrigin = origin.ToVector3();
            worldOrigin.y = GetTerrainHeight(worldOrigin) + 0.05f;
            Vector3 worldDir = direction.ToVector3().normalized;

            var sim = GameBootstrapper.Instance?.Simulation;
            float width = sim != null ? sim.Config.TsunamiWidth : 10f;
            float length = sim != null ? sim.Config.TsunamiLength : 15f;
            float warningSeconds = sim != null ? sim.Config.TsunamiWarningTicks / (float)sim.Config.TickRate : 2f;

            // Create rectangular warning zone
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "TsunamiWarning";

            // Position at center of rectangle
            Vector3 center = worldOrigin + worldDir * (length / 2f);
            mark.transform.position = center;
            mark.transform.localScale = new Vector3(width, 0.02f, length);

            // Rotate to face wave direction
            if (worldDir.sqrMagnitude > 0.01f)
                mark.transform.rotation = Quaternion.LookRotation(worldDir, Vector3.up);

            var collider = mark.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = mark.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(0.1f, 0.3f, 1f, 0.35f);
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                renderer.material = mat;
            }

            int id = nextWarningId++;
            warningMarks[id] = mark;
            StartCoroutine(WarningPulse(id, mark, warningSeconds));
        }

        public void HandleTsunamiImpact(FixedVector3 origin, FixedVector3 direction, List<int> hitUnitIds)
        {
            Vector3 worldOrigin = origin.ToVector3();
            worldOrigin.y = GetTerrainHeight(worldOrigin);
            Vector3 worldDir = direction.ToVector3().normalized;

            var sim = GameBootstrapper.Instance?.Simulation;
            float width = sim != null ? sim.Config.TsunamiWidth : 10f;
            float length = sim != null ? sim.Config.TsunamiLength : 15f;

            StartCoroutine(TsunamiWaveSweep(worldOrigin, worldDir, width, length));

            // Trigger knockback arcs on hit units
            if (unitViews != null)
            {
                for (int i = 0; i < hitUnitIds.Count; i++)
                {
                    int unitId = hitUnitIds[i];
                    if (unitViews.TryGetValue(unitId, out var view) && view != null)
                    {
                        if (sim != null)
                        {
                            var unitData = sim.UnitRegistry.GetUnit(unitId);
                            if (unitData != null)
                            {
                                Vector3 startPos = view.transform.position;
                                Vector3 endPos = unitData.SimPosition.ToVector3();
                                endPos.y = GetTerrainHeight(endPos);
                                view.TriggerMeteorKnockback(startPos, endPos);
                            }
                        }
                    }
                }
            }
        }

        private IEnumerator TsunamiWaveSweep(Vector3 origin, Vector3 direction, float width, float length)
        {
            // Create a wave wall that sweeps forward
            var wave = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wave.name = "TsunamiWave";
            wave.transform.localScale = new Vector3(width, 3f, 0.5f);

            if (direction.sqrMagnitude > 0.01f)
                wave.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            var col = wave.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = wave.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(0.1f, 0.4f, 0.9f, 0.7f);
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                rend.material = mat;
            }

            // Camera shake
            var cam = Object.FindFirstObjectByType<RTSCameraController>();
            if (cam != null)
                StartCoroutine(CameraShake(cam, 0.5f, 0.4f));

            SFXManager.Instance?.Play(SFXType.ArrowImpact, origin, 1.0f);

            float sweepDuration = 0.5f;
            float elapsed = 0f;

            while (elapsed < sweepDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / sweepDuration;
                Vector3 pos = origin + direction * (length * t);
                pos.y = GetTerrainHeight(pos) + 1.5f;
                wave.transform.position = pos;

                // Fade out near end
                if (rend != null && t > 0.7f)
                {
                    float fadeT = (t - 0.7f) / 0.3f;
                    rend.material.color = new Color(0.1f, 0.4f, 0.9f, 0.7f * (1f - fadeT));
                }

                yield return null;
            }

            Destroy(wave);
        }

        // ========== SHARED HELPERS ==========

        private GameObject CreateWarningMark(Vector3 position, float radius, Color color)
        {
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mark.name = "GodPowerWarning";
            mark.transform.position = position;
            mark.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            var collider = mark.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = mark.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = color;
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                renderer.material = mat;
            }

            return mark;
        }

        private float GetTerrainHeight(Vector3 pos)
        {
            if (Physics.Raycast(new Vector3(pos.x, 100f, pos.z), Vector3.down, out RaycastHit hit, 200f, LayerMask.GetMask("Ground")))
                return hit.point.y;
            return 0f;
        }
    }
}

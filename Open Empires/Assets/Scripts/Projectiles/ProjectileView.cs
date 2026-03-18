using UnityEngine;

namespace OpenEmpires
{
    public class ProjectileView : MonoBehaviour
    {
        private FixedVector3 previousPos;
        private FixedVector3 currentPos;
        private FixedVector3 startPos;
        private MapData mapData;
        private float heightScale;
        private float initialFlatDistance;
        private float arcHeight;
        private bool isBolt;

        public void Initialize(FixedVector3 startPos, FixedVector3 targetPos, MapData mapData, float heightScale, bool isBolt = false)
        {
            this.startPos = startPos;
            previousPos = startPos;
            currentPos = startPos;
            this.mapData = mapData;
            this.heightScale = heightScale;
            this.isBolt = isBolt;

            // Compute arc parameters from flat XZ distance
            Vector3 startFlat = new Vector3(startPos.x.ToFloat(), 0, startPos.z.ToFloat());
            Vector3 targetFlat = new Vector3(targetPos.x.ToFloat(), 0, targetPos.z.ToFloat());
            initialFlatDistance = (targetFlat - startFlat).magnitude;
            if (initialFlatDistance < 0.1f) initialFlatDistance = 0.1f;
            arcHeight = isBolt ? 0f : Mathf.Clamp(initialFlatDistance * 0.25f, 0.5f, 4f);

            Vector3 pos = startPos.ToVector3();
            if (mapData != null)
                pos.y = mapData.SampleHeight(pos.x, pos.z) * heightScale + 0.8f;
            else
                pos.y += 0.8f;
            transform.position = pos;
        }

        public void UpdatePositions(FixedVector3 prev, FixedVector3 curr)
        {
            previousPos = prev;
            currentPos = curr;
        }

        private void Update()
        {
            float alpha = GameBootstrapper.Instance != null ? GameBootstrapper.Instance.InterpolationAlpha : 1f;
            Vector3 prev = previousPos.ToVector3();
            Vector3 curr = currentPos.ToVector3();
            Vector3 pos = Vector3.Lerp(prev, curr, alpha);

            // Terrain base height
            float baseY = (mapData != null) ? mapData.SampleHeight(pos.x, pos.z) * heightScale + 0.8f : pos.y + 0.8f;

            if (isBolt)
            {
                // Flat trajectory — bolts fly straight
                pos.y = baseY;
            }
            else
            {
                // Arc progress from flat distance traveled
                Vector3 startFlat = new Vector3(startPos.x.ToFloat(), 0, startPos.z.ToFloat());
                Vector3 posFlat = new Vector3(pos.x, 0, pos.z);
                float distFromStart = (posFlat - startFlat).magnitude;
                float t = Mathf.Clamp01(distFromStart / initialFlatDistance);

                // Parabolic arc offset: peaks at midpoint
                float arcOffset = arcHeight * 4f * t * (1f - t);
                pos.y = baseY + arcOffset;
            }
            transform.position = pos;

            // Orient along travel direction
            Vector3 dir = curr - prev;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                if (isBolt)
                {
                    // Bolts fly flat — no arc slope
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
                else
                {
                    Vector3 horizDir = dir.normalized;
                    Vector3 startFlat2 = new Vector3(startPos.x.ToFloat(), 0, startPos.z.ToFloat());
                    Vector3 posFlat2 = new Vector3(pos.x, 0, pos.z);
                    float distFromStart2 = (posFlat2 - startFlat2).magnitude;
                    float t2 = Mathf.Clamp01(distFromStart2 / initialFlatDistance);
                    float arcSlope = arcHeight * 4f * (1f - 2f * t2) / initialFlatDistance;
                    Vector3 flyDir = new Vector3(horizDir.x, arcSlope, horizDir.z);
                    transform.rotation = Quaternion.LookRotation(flyDir);
                }
            }
        }

        public void OnHit()
        {
            Destroy(gameObject);
        }
    }
}

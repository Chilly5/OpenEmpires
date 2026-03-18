using UnityEngine;
using System.Collections.Generic;

namespace OpenEmpires
{
    public class SpinningAttackRangeRing : MonoBehaviour
    {
        private float range;
        private List<LineRenderer> lineSegments = new List<LineRenderer>();
        private float rotationSpeed = 15f; // degrees per second - reduced from 30f for slower rotation
        private int segmentCount = 36; // Number of separate line segments
        private float segmentLength = 0.8f; // Length of each segment relative to available arc
        private float currentRotation = 0f;
        private Material segmentMaterial;

        public void Initialize(float attackRange)
        {
            range = attackRange;
            CreateSegmentedRing();
        }

        private void CreateSegmentedRing()
        {
            // Create shared material for all segments
            segmentMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            segmentMaterial.color = Color.white;

            // Create individual LineRenderer components for each segment
            for (int i = 0; i < segmentCount; i++)
            {
                GameObject segmentObj = new GameObject($"RangeSegment_{i}");
                segmentObj.transform.SetParent(transform);
                segmentObj.transform.localPosition = Vector3.zero;

                LineRenderer lineRenderer = segmentObj.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.startWidth = 0.08f;
                lineRenderer.endWidth = 0.08f;
                lineRenderer.positionCount = 2; // Each segment has start and end points
                lineRenderer.material = segmentMaterial;

                lineSegments.Add(lineRenderer);
            }

            UpdateSegmentedRing();
        }

        private void Update()
        {
            if (lineSegments.Count == 0) return;

            // Rotate the pattern
            currentRotation += rotationSpeed * Time.deltaTime;
            if (currentRotation >= 360f)
                currentRotation -= 360f;

            UpdateSegmentedRing();
        }

        private void UpdateSegmentedRing()
        {
            if (lineSegments.Count == 0 || range <= 0) return;

            float angleStep = 360f / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                if (i >= lineSegments.Count) break;

                // Calculate the center angle for this segment (with rotation offset)
                float centerAngle = (i * angleStep + currentRotation) * Mathf.Deg2Rad;
                
                // Calculate the segment's angular span
                float segmentAngleSpan = (angleStep * segmentLength) * Mathf.Deg2Rad;
                float halfSpan = segmentAngleSpan * 0.5f;

                // Calculate start and end angles for this segment
                float startAngle = centerAngle - halfSpan;
                float endAngle = centerAngle + halfSpan;

                // Calculate start and end positions
                Vector3 startPos = new Vector3(
                    Mathf.Cos(startAngle) * range,
                    0.1f,
                    Mathf.Sin(startAngle) * range
                );

                Vector3 endPos = new Vector3(
                    Mathf.Cos(endAngle) * range,
                    0.1f,
                    Mathf.Sin(endAngle) * range
                );

                // Set the positions for this segment
                lineSegments[i].SetPosition(0, startPos);
                lineSegments[i].SetPosition(1, endPos);
            }
        }

        public void SetRotationSpeed(float speed)
        {
            rotationSpeed = speed;
        }

        public void SetSegmentCount(int count)
        {
            segmentCount = Mathf.Max(4, count);
            
            // Destroy existing segments and recreate
            foreach (var segment in lineSegments)
            {
                if (segment != null)
                    DestroyImmediate(segment.gameObject);
            }
            lineSegments.Clear();
            
            CreateSegmentedRing();
        }

        public void SetSegmentLength(float length)
        {
            segmentLength = Mathf.Clamp01(length);
            UpdateSegmentedRing();
        }

        private void OnDestroy()
        {
            if (segmentMaterial != null)
                DestroyImmediate(segmentMaterial);
        }
    }
}
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstAid
{
    public sealed class FirstAidWaterEffect : MonoBehaviour
    {
        [SerializeField] private Transform spout;
        [SerializeField, Min(0.01f)] private float maximumDistance = 1.4f;
        [SerializeField, Range(0.002f, 0.02f)] private float streamWidth = 0.006f;

        private const int PointCount = 10;
        private const int HighlightCount = 4;
        private const float RequestLifetime = 0.15f;
        private readonly Vector3[] points = new Vector3[PointCount];
        private readonly LineRenderer[] highlights = new LineRenderer[HighlightCount];
        private Transform visualRoot;
        private LineRenderer stream;
        private Material waterMaterial;
        private Material highlightMaterial;
        private Vector3 origin;
        private Vector3 destination;
        private float lastRequest;
        private bool initializationAttempted;
        private bool flowing;
        private bool visible;

        private void Awake() => EnsureVisuals();

        // Call each frame while water should flow. The endpoints are world positions.
        public void SetFlow(Vector3 from, Vector3 to, bool active)
        {
            if (!active || !isActiveAndEnabled || !IsFinite(from) || !IsFinite(to))
            {
                StopFlow();
                return;
            }

            float distance = Vector3.Distance(from, to);
            if (distance < 0.015f || distance > maximumDistance || !EnsureVisuals())
            {
                StopFlow();
                return;
            }

            origin = from;
            destination = to;
            lastRequest = Time.unscaledTime;
            flowing = true;
            SetVisible(true);
        }

        // Optional convenience overload for an assigned sink or bottle spout.
        public void SetFlow(Vector3 to, bool active) =>
            SetFlow(spout != null ? spout.position : transform.position, to, active);

        private void LateUpdate()
        {
            if (!flowing)
                return;

            if (Time.unscaledTime - lastRequest > RequestLifetime)
            {
                StopFlow();
                return;
            }

            float sag = Mathf.Min(0.055f, Vector3.Distance(origin, destination) * 0.08f);
            for (int index = 0; index < PointCount; index++)
                points[index] = Position(index / (PointCount - 1f), sag);

            stream.startWidth = streamWidth;
            stream.endWidth = streamWidth * 0.62f;
            stream.SetPositions(points);

            float travel = Time.unscaledTime * 1.55f;
            for (int index = 0; index < HighlightCount; index++)
            {
                float start = Mathf.Repeat(travel - index / (float)HighlightCount, 1f);
                var line = highlights[index];
                line.startWidth = streamWidth * 0.32f;
                line.endWidth = streamWidth * 0.20f;
                line.SetPosition(0, Position(start, sag));
                line.SetPosition(1, Position(Mathf.Min(1f, start + 0.075f), sag));
            }
        }

        private Vector3 Position(float progress, float sag) =>
            Vector3.Lerp(origin, destination, progress) +
            Vector3.down * (sag * 4f * progress * (1f - progress));

        private bool EnsureVisuals()
        {
            if (initializationAttempted)
                return stream != null;

            initializationAttempted = true;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                return false;

            waterMaterial = CreateMaterial(shader, new Color(0.25f, 0.76f, 0.96f, 0.84f));
            highlightMaterial = CreateMaterial(shader, new Color(0.80f, 0.96f, 1f, 0.94f));
            visualRoot = new GameObject("Flowing water visuals").transform;
            visualRoot.SetParent(transform, false);
            stream = CreateLine("Water stream", waterMaterial, PointCount);
            for (int index = 0; index < HighlightCount; index++)
                highlights[index] = CreateLine("Flow highlight " + (index + 1), highlightMaterial, 2);

            return true;
        }

        private static Material CreateMaterial(Shader shader, Color color)
        {
            var material = new Material(shader) { name = "Runtime clean water" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private LineRenderer CreateLine(string lineName, Material material, int count)
        {
            var line = new GameObject(lineName).AddComponent<LineRenderer>();
            line.transform.SetParent(visualRoot, false);
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = count;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.enabled = false;
            return line;
        }

        private void SetVisible(bool value)
        {
            if (visible == value)
                return;

            visible = value;
            if (stream != null) stream.enabled = value;
            foreach (var line in highlights)
                if (line != null) line.enabled = value;
        }

        private void StopFlow()
        {
            flowing = false;
            SetVisible(false);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void OnDisable() => StopFlow();

        private void OnDestroy()
        {
            if (visualRoot != null) Destroy(visualRoot.gameObject);
            if (waterMaterial != null) Destroy(waterMaterial);
            if (highlightMaterial != null) Destroy(highlightMaterial);
        }
    }
}

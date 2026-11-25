using TMPro;
using UnityEngine;

namespace FirstAid
{
    internal sealed class FirstAidToolLabel : MonoBehaviour
    {
        private const float Width = 440f;
        private const float Height = 76f;
        private Canvas canvas;
        private TextMeshProUGUI label;
        private Camera view;
        private Transform target;
        private Renderer[] renderers;
        private string shownText;
        private bool visible;

        public static FirstAidToolLabel Create(Transform parent, Camera view)
        {
            var canvas = FirstAidPracticeUi.Canvas("Hovered tool label", parent, Width, Height, 0.001f);
            var controller = canvas.gameObject.AddComponent<FirstAidToolLabel>();
            controller.canvas = canvas;
            controller.view = view;
            canvas.worldCamera = view;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 45;
            controller.label = FirstAidPracticeUi.Text(canvas.transform, "Tool name", 0f, 0f,
                Width, Height, 30f, Color.white, TextAlignmentOptions.Center);
            controller.label.textWrappingMode = TextWrappingModes.NoWrap;
            controller.label.overflowMode = TextOverflowModes.Overflow;
            controller.label.raycastTarget = false;

            // TMP's fontMaterial getter creates a material owned by this text object.
            // Take that instance before changing outlines so the paper stays untouched.
            var instance = controller.label.fontMaterial;
            if (instance != null)
            {
                instance.name = "Hovered tool text outline";
                instance.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                controller.label.outlineColor = Color.black;
                instance.EnableKeyword("OUTLINE_ON");
                controller.label.outlineWidth = 0.25f;
                controller.label.UpdateMeshPadding();
            }

            controller.Hide();
            return controller;
        }

        public void SetCamera(Camera view)
        {
            this.view = view;
            canvas.worldCamera = view;
            if (visible)
                UpdatePlacement();
        }

        public void Show(Transform target, string text)
        {
            if (target == null || view == null || string.IsNullOrWhiteSpace(text))
            {
                Hide();
                return;
            }

            if (this.target != target)
            {
                this.target = target;
                renderers = target.GetComponentsInChildren<Renderer>(false);
            }

            if (shownText != text)
            {
                shownText = text;
                label.text = text;
            }

            visible = true;
            canvas.enabled = true;
            UpdatePlacement();
        }

        public void Hide()
        {
            visible = false;
            target = null;
            if (canvas != null)
                canvas.enabled = false;
        }

        private void LateUpdate()
        {
            if (!visible)
                return;

            if (target == null || !target.gameObject.activeInHierarchy || view == null)
            {
                Hide();
                return;
            }

            UpdatePlacement();
        }

        private void UpdatePlacement()
        {
            if (target == null || view == null)
                return;

            var bounds = new Bounds(target.position, Vector3.zero);
            bool found = false;
            if (renderers != null)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                        renderer is LineRenderer || renderer is TrailRenderer || renderer is ParticleSystemRenderer)
                        continue;

                    if (found)
                        bounds.Encapsulate(renderer.bounds);
                    else
                    {
                        bounds = renderer.bounds;
                        found = true;
                    }
                }
            }

            transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.065f, bounds.center.z);
            var forward = view.orthographic ? view.transform.forward : transform.position - view.transform.position;
            if (forward.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.LookRotation(forward, view.transform.up);

            float scale = view.orthographic
                ? Mathf.Clamp(2f * view.orthographicSize / Mathf.Max(360, view.pixelHeight) * 0.90f, 0.0012f, 0.004f)
                : Mathf.Clamp(Vector3.Distance(view.transform.position, transform.position) * 0.0012f, 0.0008f, 0.0026f);
            Vector3 inherited = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(scale / Mathf.Max(0.0001f, Mathf.Abs(inherited.x)),
                scale / Mathf.Max(0.0001f, Mathf.Abs(inherited.y)),
                scale / Mathf.Max(0.0001f, Mathf.Abs(inherited.z)));
        }
    }
}

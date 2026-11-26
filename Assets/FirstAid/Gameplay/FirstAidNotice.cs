using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FirstAid
{
    internal sealed class FirstAidNotice : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform card;
        private Image background;
        private TextMeshProUGUI text;
        private Camera camera;
        private Transform anchor;
        private Vector3 lastPoint;
        private float end;
        private float height;
        private bool visible;
        private bool errorVisible;

        public static FirstAidNotice Create(Transform parent)
        {
            var host = new GameObject("Wound and Hand Feedback");
            host.transform.SetParent(parent, false);
            var result = host.AddComponent<FirstAidNotice>();
            result.canvas = FirstAidPracticeUi.Canvas("Action Message", host.transform, 400, 102, 0.00088f);
            result.canvas.sortingOrder = 15;
            result.card = FirstAidPracticeUi.Rect(result.canvas.transform, "Notice Card", 0, 0, 400, 102);
            result.card.anchorMin = new Vector2(0.5f, 0.5f);
            result.card.anchorMax = result.card.anchorMin;
            result.card.pivot = result.card.anchorMin;
            result.card.anchoredPosition = Vector2.zero;
            result.background = FirstAidPracticeUi.Fill(result.card, "Notice Background", 0, 0, 400, 102, Color.black);
            result.text = FirstAidPracticeUi.Text(result.card, "Action Message", 16, 6,
                368, 90, 23, Color.white, TextAlignmentOptions.Midline);
            result.Hide();
            return result;
        }

        public void SetCamera(Camera view)
        {
            camera = view;
            canvas.renderMode = view.orthographic ? RenderMode.ScreenSpaceOverlay : RenderMode.WorldSpace;
            canvas.worldCamera = view;
            if (!view.orthographic)
            {
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(400, 102);
                canvas.transform.localScale = Vector3.one * 0.00088f;
                card.localScale = Vector3.one;
                card.anchoredPosition = Vector2.zero;
            }
        }

        public void Show(string message, Transform follow, Vector3 point, float offset, bool error, float seconds)
        {
            anchor = follow;
            lastPoint = point;
            height = offset;
            end = Time.unscaledTime + seconds;
            text.text = message;
            errorVisible = error;
            background.color = error ? new Color(0.59f, 0.105f, 0.12f, 0.97f) :
                new Color(0.07f, 0.29f, 0.235f, 0.96f);
            if (!visible)
            {
                visible = true;
                canvas.gameObject.SetActive(true);
            }
            SetPose();
        }

        public void Hide()
        {
            visible = false;
            canvas.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!visible)
                return;

            if (Time.unscaledTime >= end)
                Hide();
            else
                SetPose();
        }

        private void SetPose()
        {
            if (camera == null)
                return;

            if (anchor != null)
                lastPoint = anchor.position;

            Vector3 point = lastPoint + Vector3.up * height;
            if (camera.orthographic)
            {
                Vector3 screen = camera.WorldToScreenPoint(point);
                float scale = Mathf.Min(Screen.width / 830f, Screen.height / 640f);
                card.localScale = Vector3.one * scale;
                float xPad = 200f * scale + 8f;
                float yPad = 51f * scale + 8f;
                card.position = new Vector3(Mathf.Clamp(screen.x, xPad, Screen.width - xPad),
                    Mathf.Clamp(screen.y + 35f * scale, yPad, Screen.height - yPad), 0f);
                return;
            }

            if (errorVisible)
            {
                Vector3 viewport = camera.WorldToViewportPoint(point);
                if (viewport.z <= 0f)
                    viewport = new Vector3(0.5f, 0.39f, 0.58f);

                viewport.x = Mathf.Clamp(viewport.x, 0.30f, 0.70f);
                viewport.y = Mathf.Clamp(viewport.y, 0.35f, 0.54f);
                viewport.z = Mathf.Clamp(viewport.z, 0.49f, 0.64f);
                float frustumWidth = 2f * viewport.z * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * camera.aspect;
                canvas.transform.localScale = Vector3.one * Mathf.Min(0.00088f, frustumWidth * 0.46f / 400f);
                canvas.transform.SetPositionAndRotation(camera.ViewportToWorldPoint(viewport), camera.transform.rotation);
                return;
            }

            canvas.transform.localScale = Vector3.one * 0.00088f;
            Vector3 direction = point - camera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
                canvas.transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction, camera.transform.up));
        }
    }
}

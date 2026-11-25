using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace FirstAid
{
    internal sealed class FirstAidClipboard : MonoBehaviour
    {
        private static readonly Color Ink = new(0.10f, 0.16f, 0.15f);
        private static readonly Color Muted = new(0.28f, 0.34f, 0.30f);
        private static readonly Color Line = new(0.64f, 0.66f, 0.58f);
        private static readonly Color ButtonPaper = new(0.86f, 0.87f, 0.80f);
        private static readonly Color Selected = new(0.17f, 0.36f, 0.31f);
        private static readonly Vector3 HomePosition = new(-0.91f, 0.908f, -0.46f);
        private static readonly Quaternion HomeRotation = Quaternion.Euler(-90f, 180f, 0f);

        private const float PaperHeight = 0.264f;
        private const int ReadingLayer = 31;
        private readonly Button[] choices = new Button[3];
        private readonly TextMeshProUGUI[] choiceLabels = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] statuses = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] taskLines = new TextMeshProUGUI[3];
        private Canvas paper;
        private Canvas desktopGuide;
        private GraphicRaycaster mouseRaycaster;
        private TrackedDeviceGraphicRaycaster xrRaycaster;
        private Rigidbody body;
        private XRGrabInteractable grab;
        private Camera view;
        private Camera readingCamera;
        private int viewMask;
        private bool returnPending;
        private bool mouseOpen;
        private bool desktop;
        private bool simulated;
        private Button pressedButton;
        private Button pendingButton;
        private int buttonClickCount;
        private int pressedAtClickCount;
        private int pendingAtClickCount;
        private int pendingFrame;
        private PointerEventData simulatedPointer;
        private readonly List<RaycastResult> simulatedHits = new();
        private TextMeshProUGUI heading;
        private TextMeshProUGUI location;
        private TextMeshProUGUI phase;
        private TextMeshProUGUI instruction;
        private TextMeshProUGUI tally;
        private TextMeshProUGUI siteLabel;
        private TextMeshProUGUI inspectLabel;
        private TextMeshProUGUI replayLabel;
        private TextMeshProUGUI closeLabel;
        private TextMeshProUGUI guideLabel;
        private TextMeshProUGUI reference;
        private Button closeButton;
        private Button inspectButton;
        private Image bar;

        public bool IsOpen => mouseOpen;
        public bool IsHeld => grab != null && grab.isSelected;
        public Collider HitBox { get; private set; }

        public static FirstAidClipboard Create(FirstAidSession session, Transform parent)
        {
            var host = new GameObject("Practice Clipboard");
            host.transform.SetParent(parent, false);
            host.transform.SetPositionAndRotation(HomePosition, HomeRotation);
            var controller = host.AddComponent<FirstAidClipboard>();
            controller.Build(session);
            return controller;
        }

        private void Build(FirstAidSession session)
        {
            var model = Resources.Load<GameObject>("FirstAidClipboardModel");
            if (model != null)
                Instantiate(model, transform);
            else
                Debug.LogError("The FirstAidClipboardModel resource is missing.");

            body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var box = gameObject.AddComponent<BoxCollider>();
            // The paper is on +Z. Keep the grab collider behind the buttons so the
            // XR Simulator can identify a click on the paper as UI rather than a grab.
            box.center = new Vector3(0f, 0.004f, -0.003f);
            box.size = new Vector3(0.240f, 0.342f, 0.012f);
            HitBox = box;
            grab = gameObject.AddComponent<XRGrabInteractable>();
            if (!grab.colliders.Contains(box))
                grab.colliders.Add(box);
            var grip = new GameObject("Clipboard Grip").transform;
            grip.SetParent(transform, false);
            grip.localPosition = new Vector3(0f, -0.123f, 0f);
            grip.localRotation = Quaternion.Euler(0f, 180f, 0f);
            grab.attachTransform = grip;
            grab.useDynamicAttach = false;
            grab.attachEaseInTime = 0f;
            grab.farAttachMode = InteractableFarAttachMode.Near;
            grab.throwOnDetach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.selectExited.AddListener(_ => returnPending = true);

            paper = FirstAidPracticeUi.Canvas("Printed Checklist", transform, 400, 552, PaperHeight / 552f);
            paper.transform.localPosition = new Vector3(0f, -0.009f, 0.0053f);
            paper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            mouseRaycaster = paper.gameObject.AddComponent<GraphicRaycaster>();
            xrRaycaster = paper.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            xrRaycaster.checkFor3DOcclusion = false;
            xrRaycaster.checkFor2DOcclusion = false;

            heading = Text("Checklist title", 9, 6, 275, 43, 29);
            AddButton("EN", 290, 10, 47, 32, () => session.SetThai(false), out var english);
            AddButton("ไทย", 344, 10, 48, 32, () => session.SetThai(true), out var thai);
            english.fontSize = 18;
            thai.fontSize = 18;
            Rule(52);
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                float top = 66 + 44 * i;
                choices[i] = AddButton("Case " + (i + 1), 9, top, 382, 38,
                    () => session.SelectCase(index), out choiceLabels[i]);
                statuses[i] = Text("Case progress", 320, top, 64, 38, 16, TextAlignmentOptions.MidlineRight);
            }

            AddButton("Change wound site", 9, 201, 161, 40, session.SwitchSite, out siteLabel);
            inspectButton = AddButton("Inspect button", 182, 201, 209, 40, session.InspectWound, out inspectLabel);
            location = Text("Wound location", 9, 257, 382, 30, 26);
            phase = Text("Current step", 9, 292, 382, 26, 19);
            for (int i = 0; i < taskLines.Length; i++)
                taskLines[i] = Text("Checklist item " + (i + 1), 9, 323 + 29 * i, 382, 28, 19);

            instruction = Text("Instruction", 9, 415, 382, 49, 18, TextAlignmentOptions.TopLeft);
            FirstAidPracticeUi.Fill(paper.transform, "Progress track", 9, 469, 382, 4, Line);
            bar = FirstAidPracticeUi.Fill(paper.transform, "Current action progress", 9, 469, 0, 4, Selected);
            AddButton("Replay button", 9, 484, 158, 36, session.RestartCase, out replayLabel);
            closeButton = AddButton("Close clipboard", 180, 484, 136, 36, ToggleMouse, out closeLabel);
            tally = Text("Total sites", 9, 526, 280, 23, 15);
            reference = Text("Reference", 299, 526, 91, 23, 14, TextAlignmentOptions.MidlineRight);

            desktopGuide = FirstAidPracticeUi.Canvas("Clipboard Controls", transform.parent, 600, 52, 1f);
            desktopGuide.renderMode = RenderMode.ScreenSpaceOverlay;
            desktopGuide.sortingOrder = 10;
            var scaler = desktopGuide.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 810);
            var guide = FirstAidPracticeUi.Fill(desktopGuide.transform, "Guide", 0, 0, 525, 40,
                new Color(0.05f, 0.11f, 0.10f, 0.92f));
            var guideRect = guide.rectTransform;
            guideRect.anchorMin = Vector2.zero;
            guideRect.anchorMax = Vector2.zero;
            guideRect.pivot = Vector2.zero;
            guideRect.anchoredPosition = new Vector2(18, 18);
            guideLabel = FirstAidPracticeUi.Text(guideRect, "Controls", 13, 0, 498, 40, 18, Color.white);
            ReturnHome();
        }

        public void SetMode(Camera camera, bool mouse, bool simulatorActive)
        {
            if (mouseOpen)
                ToggleMouse();

            view = camera;
            desktop = mouse;
            simulated = simulatorActive;
            pressedButton = null;
            pendingButton = null;
            mouseOpen = false;
            if (IsHeld)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            ReturnHome();
            grab.enabled = !mouse;
            HitBox.enabled = true;
            paper.worldCamera = camera;
            mouseRaycaster.enabled = simulated;
            xrRaycaster.enabled = !mouse;
            closeButton.gameObject.SetActive(mouse);
            desktopGuide.gameObject.SetActive(mouse);
        }

        public void ToggleMouse()
        {
            if (!desktop)
                return;

            mouseOpen = !mouseOpen;
            HitBox.enabled = !mouseOpen;
            mouseRaycaster.enabled = mouseOpen;
            desktopGuide.gameObject.SetActive(!mouseOpen);
            if (mouseOpen)
            {
                EnableReadingView();
                HoldInView();
            }
            else
            {
                DisableReadingView();
                ReturnHome();
            }
        }

        private void EnableReadingView()
        {
            var stack = view.GetUniversalAdditionalCameraData().cameraStack;
            if (stack == null)
                return;

            if (readingCamera == null)
            {
                var overlay = new GameObject("Clipboard Reading Camera");
                overlay.transform.SetParent(view.transform, false);
                readingCamera = overlay.AddComponent<Camera>();
                readingCamera.CopyFrom(view);
                var data = readingCamera.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Overlay;
                data.renderPostProcessing = false;
            }

            viewMask = view.cullingMask;
            int layerMask = 1 << ReadingLayer;
            view.cullingMask = viewMask & ~layerMask;
            readingCamera.cullingMask = layerMask;
            foreach (var item in GetComponentsInChildren<Transform>(true))
                item.gameObject.layer = ReadingLayer;

            readingCamera.enabled = true;
            stack.Add(readingCamera);
        }

        private void DisableReadingView()
        {
            if (readingCamera == null || !readingCamera.enabled)
                return;

            view.cullingMask = viewMask;
            view.GetUniversalAdditionalCameraData().cameraStack?.Remove(readingCamera);
            readingCamera.enabled = false;
            foreach (var item in GetComponentsInChildren<Transform>(true))
                item.gameObject.layer = 0;
        }

        public void UpdatePose()
        {
            if (mouseOpen)
                HoldInView();
            else if (returnPending && !IsHeld)
                ReturnHome();

            if (simulated)
                UpdateSimulatorPointer();
        }

        private void UpdateSimulatorPointer()
        {
            if (pendingButton != null && Time.frameCount >= pendingFrame)
            {
                var button = pendingButton;
                pendingButton = null;
                // The stock simulator may have sent a virtual trigger already.
                // Use a direct click only when the tracked pointer could not deliver it.
                if (buttonClickCount == pendingAtClickCount && button.IsActive() && button.IsInteractable())
                    button.onClick.Invoke();
            }

            var mouse = Mouse.current;
            if (mouse == null || mouse.rightButton.isPressed)
            {
                pressedButton = null;
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressedButton = FindPaperButton(mouse.position.ReadValue());
                pressedAtClickCount = buttonClickCount;
            }

            if (mouse.leftButton.wasReleasedThisFrame && pressedButton != null)
            {
                if (pressedButton == FindPaperButton(mouse.position.ReadValue()))
                {
                    pendingButton = pressedButton;
                    pendingAtClickCount = pressedAtClickCount;
                    pendingFrame = Time.frameCount + 2;
                }

                pressedButton = null;
            }
        }

        private Button FindPaperButton(Vector2 point)
        {
            var events = EventSystem.current;
            if (events == null)
                return null;

            simulatedPointer ??= new PointerEventData(events);
            simulatedPointer.position = point;
            events.RaycastAll(simulatedPointer, simulatedHits);
            if (simulatedHits.Count == 0)
                return null;

            var hit = simulatedHits[0];
            if (!hit.gameObject.transform.IsChildOf(paper.transform))
                return null;

            var ray = view.ScreenPointToRay(point);
            if (Physics.Raycast(ray, out var blocker, Mathf.Max(0f, hit.distance - 0.001f),
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                !blocker.collider.transform.IsChildOf(transform))
                return null;

            return hit.gameObject.GetComponentInParent<Button>();
        }

        private void HoldInView()
        {
            var camera = view.transform;
            if (readingCamera != null)
            {
                readingCamera.orthographicSize = view.orthographicSize;
                readingCamera.aspect = view.aspect;
            }

            float fitHeight = 2f * view.orthographicSize * 0.78f;
            float fitWidth = 2f * view.orthographicSize * view.aspect * 0.80f;
            transform.localScale = Vector3.one * Mathf.Min(fitHeight / 0.35f, fitWidth / 0.24f);
            transform.SetPositionAndRotation(camera.position + camera.forward * 0.78f,
                Quaternion.LookRotation(-camera.forward, camera.up));
        }

        private void ReturnHome()
        {
            returnPending = false;
            transform.localScale = Vector3.one;
            body.isKinematic = true;
            body.position = HomePosition;
            body.rotation = HomeRotation;
            transform.SetPositionAndRotation(HomePosition, HomeRotation);
        }

        public void Refresh(bool thai, int selected, int site, string wound, string[] checklist,
            bool inspected, int step, string task, int completed)
        {
            heading.text = thai ? "เช็กลิสต์การทำแผล" : "Treatment checklist";
            location.text = FirstAidLanguage.Get(wound, thai);
            bool done = step == checklist.Length;
            phase.text = !inspected ? FirstAidLanguage.Get("INSPECT THE INJURY", thai) :
                done ? FirstAidLanguage.Get("SITE COMPLETE", thai) :
                FirstAidLanguage.Format("STEP {0} OF {1}", thai, step + 1, checklist.Length);
            instruction.text = FirstAidLanguage.Get(task, thai);
            siteLabel.text = FirstAidLanguage.Format("Site {0} / 2", thai, site + 1);
            inspectLabel.text = FirstAidLanguage.Get("Inspect wound", thai);
            replayLabel.text = FirstAidLanguage.Get("Replay", thai);
            closeLabel.text = thai ? "วางคลิปบอร์ด" : "Put back";
            inspectButton.interactable = !inspected;
            reference.text = "NHS";
            guideLabel.text = thai ? "Tab หรือคลิกคลิปบอร์ด    |    Shift ค้างเพื่อเทน้ำ" :
                "Tab or click the clipboard    |    Hold Shift to pour";

            int count = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                bool active = i == selected;
                choices[i].image.color = active ? Selected : ButtonPaper;
                choiceLabels[i].color = active ? Color.white : Ink;
                choiceLabels[i].text = $"0{i + 1}   {FirstAidLanguage.CaseName(i, thai)}";
                int mask = (completed >> (i * 2)) & 3;
                int sites = (mask & 1) + (mask >> 1);
                count += sites;
                statuses[i].text = $"{sites}/2";
                statuses[i].color = active ? Color.white : Muted;
            }

            tally.text = FirstAidLanguage.Format("{0} / {1} SITES COMPLETE", thai, count, 6);
            for (int i = 0; i < taskLines.Length; i++)
            {
                taskLines[i].gameObject.SetActive(i < checklist.Length);
                if (i >= checklist.Length)
                    continue;

                string tick = i < step ? "[x]" : $"0{i + 1}";
                taskLines[i].text = tick + "   " + FirstAidLanguage.Get(checklist[i], thai);
                taskLines[i].color = inspected && i <= step ? Selected : Muted;
            }
        }

        public void SetProgress(float progress)
        {
            var rect = bar.rectTransform;
            rect.sizeDelta = new Vector2(382f * Mathf.Clamp01(progress), 4f);
        }

        private TextMeshProUGUI Text(string name, float x, float y, float width, float height, float size,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft) =>
            FirstAidPracticeUi.Text(paper.transform, name, x, y, width, height, size, Ink, align);

        private void Rule(float y) =>
            FirstAidPracticeUi.Fill(paper.transform, "Paper rule", 9, y, 382, 1.5f, Line);

        private Button AddButton(string name, float x, float y, float width, float height, Action callback,
            out TextMeshProUGUI label)
        {
            var fill = FirstAidPracticeUi.Fill(paper.transform, name, x, y, width, height, ButtonPaper);
            fill.raycastTarget = true;
            var button = fill.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.77f, 0.95f, 0.85f);
            colors.pressedColor = new Color(0.67f, 0.85f, 0.72f);
            colors.disabledColor = new Color(0.77f, 0.77f, 0.74f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                buttonClickCount++;
                callback();
            });
            label = FirstAidPracticeUi.Text(fill.transform, name + " Label", 9, 0, width - 18, height, 19, Ink);
            if (name == "EN" || name == "ไทย")
                label.alignment = TextAlignmentOptions.Midline;
            label.text = name;
            return button;
        }
    }
}

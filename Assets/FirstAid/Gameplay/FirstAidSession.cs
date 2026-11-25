using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace FirstAid
{
    [Serializable]
    public sealed class PatientCase
    {
        public GameObject patient;
        public GameObject woundGroup;
        public Transform target;
        public Collider targetCollider;
        public Renderer marker;
        public GameObject covering;
        public GameObject debris;
    }

    [DefaultExecutionOrder(100)]
    public sealed class FirstAidSession : MonoBehaviour
    {
        [SerializeField] private PatientCase[] patients;
        [SerializeField] private PatientCase[] alternateSites;
        [SerializeField] private FirstAidTool[] tools;
        [SerializeField] private Camera desktopCamera;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private Canvas desktopCanvas;
        [SerializeField] private Canvas xrCanvas;
        [SerializeField] private XRSimpleInteractable coolingTap;
        [SerializeField] private Collider tapCollider;
        [SerializeField] private GameObject tapMarker;
        [SerializeField] private Transform spout;
        [SerializeField] private Transform sinkBowl;
        [SerializeField] private FirstAidWaterEffect sinkWater;
        [SerializeField] private FirstAidWaterEffect rinseWater;
#if UNITY_EDITOR
        [SerializeField] private GameObject simulatorPrefab;
        private GameObject simulator;
#endif

        private readonly List<XRDisplaySubsystem> displays = new();
        private FirstAidClipboard clipboard;
        private FirstAidNotice notice;
        private FirstAidWashStation washStation;
        private FirstAidXRHands xrHands;
        private FirstAidToolLabel toolLabel;
        private FirstAidTool highlightedTool;
        private Transform highlightedFixture;
        private string highlightedFixtureText;
        private FirstAidTool mouseTool;
        private FirstAidTool contactTool;
        private FirstAidTool wrongTool;
        private MaterialPropertyBlock markerProperties;
        private float contactTime;
        private float coolingTime;
        private float nextAimHint;
        private float errorUntil;
        private int currentCase;
        private int siteIndex;
        private int stepIndex;
        private int completedSites;
        private bool desktopMode;
        private bool simulatorMode;
        private bool inspected;
        private bool cooling;
        private bool contactHandled;
        private bool checklistDirty;
        private bool thai;
        private bool pouring;
        private Vector3 streamStart;
        private Vector3 streamEnd;

        private const float ContactRadius = 0.115f;
        private const float HoldSeconds = 0.75f;
        private const string LanguagePreference = "FirstAidVR.Thai";
        private static readonly Color InspectColor = new(1f, 0.71f, 0.31f);
        private static readonly Color ReadyColor = new(0.13f, 0.86f, 0.73f);
        private static readonly Color ErrorColor = new(1f, 0.34f, 0.29f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private sealed class Step
        {
            public readonly ToolKind tool;
            public readonly string title;
            public readonly string instruction;
            public readonly string completion;
            public readonly float holdTime;

            public Step(ToolKind tool, string title, string instruction, string completion,
                float holdTime = HoldSeconds)
            {
                this.tool = tool;
                this.title = title;
                this.instruction = instruction;
                this.completion = completion;
                this.holdTime = holdTime;
            }

            public bool Accepts(ToolKind item) =>
                item == tool || tool == ToolKind.Water && item == ToolKind.Saline;
        }

        private static readonly string[,] Locations =
        {
            { "Left knee", "Right elbow" },
            { "Right forearm", "Left shin" },
            { "Left forearm", "Back of right hand" }
        };

        private static readonly string[] Observations =
        {
            "The skin looks scraped and may have dirt on the surface.",
            "A narrow break in the skin is bleeding.",
            "The skin is red with small blisters after contact with hot water."
        };

        private static readonly Step[][] Steps =
        {
            new[]
            {
                new Step(ToolKind.Water, "Rinse the graze", "Rinse the marked spot with clean water or saline.",
                    "Rinse complete.", 1.0f),
                new Step(ToolKind.Dressing, "Cover the graze", "Place a sterile dressing over the wound.",
                    "Wound covered.")
            },
            new[]
            {
                new Step(ToolKind.Gauze, "Control the bleeding", "Hold clean gauze against the marked cut.",
                    "Bleeding controlled.", 1.25f),
                new Step(ToolKind.Water, "Rinse the cut", "Use clean water or saline once the bleeding stops.",
                    "Rinse complete.", 1.0f),
                new Step(ToolKind.Dressing, "Cover the cut", "Place a sterile dressing over the cleaned cut.",
                    "Wound covered.")
            },
            new[]
            {
                new Step(ToolKind.Tap, "Cool with running water", "Select the sink tap to simulate 20 min of cooling.",
                    "Cooling complete.", 2.0f),
                new Step(ToolKind.Film, "Cover the scald", "Lay a loose sheet of cling film over the scald.",
                    "Wound covered.")
            }
        };

        private static readonly string[][] Checklists = Array.ConvertAll(Steps,
            list => Array.ConvertAll(list, step => step.title));

        public int CaseIndex => currentCase;
        public int SiteIndex => siteIndex;
        public int CompletedSites => completedSites;
        public bool IsInspected => inspected;
        public bool IsComplete => stepIndex == Steps[currentCase].Length;
        public bool DesktopMode => desktopMode;

        private PatientCase CurrentPatient => siteIndex == 0 ? patients[currentCase] : alternateSites[currentCase];
        private Step CurrentStep => Steps[currentCase][stepIndex];

        private void Awake()
        {
            xrHands = FirstAidXRHands.Create(xrCamera);
            var supplies = GameObject.Find("Supplies");
            if (supplies != null)
            {
                foreach (var label in supplies.GetComponentsInChildren<TextMesh>(true))
                {
                    if (label.name == "Name Tag" || label.name == "Saline Label")
                        label.gameObject.SetActive(false);
                }
            }
        }

        private void Start()
        {
            FirstAidTreatmentCovers.Install(patients, alternateSites);
            thai = PlayerPrefs.GetInt(LanguagePreference, 0) != 0;
            markerProperties = new MaterialPropertyBlock();
            desktopCanvas.gameObject.SetActive(false);
            xrCanvas.gameObject.SetActive(false);
            clipboard = FirstAidClipboard.Create(this, transform);
            notice = FirstAidNotice.Create(transform);
            toolLabel = FirstAidToolLabel.Create(transform, desktopCamera);
            washStation = new FirstAidWashStation(transform, spout, sinkBowl, tapCollider, ShowStationNotice);
            coolingTap.selectEntered.AddListener(_ => ActivateCoolingTap());
            SetDesktopMode(!HasHeadset());
            SelectCase(0);
        }

        public void SetThai(bool value)
        {
            if (thai == value)
                return;

            thai = value;
            PlayerPrefs.SetInt(LanguagePreference, value ? 1 : 0);
            notice.Hide();
            checklistDirty = true;
        }

        public void SelectCase(int index)
        {
            if (index >= 0 && index < patients.Length)
                StartPractice(index, 0);
        }

        public void SwitchSite() => StartPractice(currentCase, 1 - siteIndex);
        public void RestartCase() => StartPractice(currentCase, siteIndex);

        private void StartPractice(int nextCase, int nextSite)
        {
            ReturnTools();
            if (nextCase != currentCase)
                washStation.ResetHands();
            currentCase = nextCase;
            siteIndex = nextSite;
            stepIndex = 0;
            inspected = false;
            cooling = false;
            coolingTime = 0f;
            nextAimHint = 0f;
            errorUntil = 0f;
            wrongTool = null;
            ResetContact();
            notice.Hide();

            for (int i = 0; i < patients.Length; i++)
            {
                patients[i].patient.SetActive(i == currentCase);
                ResetSite(patients[i], siteIndex == 0);
                ResetSite(alternateSites[i], siteIndex == 1);
            }

            checklistDirty = true;
            RefreshChecklist();
        }

        private static void ResetSite(PatientCase site, bool visible)
        {
            site.woundGroup.SetActive(visible);
            site.target.gameObject.SetActive(visible);
            site.covering.SetActive(false);
            if (site.debris != null)
                site.debris.SetActive(true);
        }

        public void InspectWound()
        {
            if (inspected || IsComplete)
                return;

            inspected = true;
            checklistDirty = true;
            ShowNotice(Observations[currentCase], null, false, 3.5f);
        }

        public void ActivateCoolingTap() => washStation.ToggleTap();

        private void ShowStationNotice(string message, Vector3 point, bool error)
        {
            notice.Show(FirstAidLanguage.Get(message, thai), null, point, 0.095f, error, 2.1f);
            checklistDirty = true;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (Application.isEditor && keyboard != null && keyboard.f2Key.wasPressedThisFrame)
            {
                SetDesktopMode(!desktopMode);
                RefreshChecklist();
                return;
            }

            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
                SetThai(!thai);

            clipboard.UpdatePose();
            highlightedTool = null;
            highlightedFixture = null;
            pouring = false;
            washStation.BeginFrame();
            if (desktopMode)
            {
                FitDesktopCamera();
                UpdateDesktop(keyboard);
            }
            else
            {
                UpdateXR();
                xrHands.UpdatePoses(washStation.HasGlove(0), washStation.HasGlove(1));
            }

            if (highlightedTool != null)
                toolLabel.Show(highlightedTool.transform, highlightedTool.DisplayName(thai));
            else if (highlightedFixture != null)
                toolLabel.Show(highlightedFixture, FirstAidLanguage.Get(highlightedFixtureText, thai));
            else
                toolLabel.Hide();

            UpdateCooling();
            sinkWater.SetFlow(spout.position, washStation.StreamTarget, washStation.TapOpen);
            rinseWater.SetFlow(streamStart, streamEnd, pouring);
            RefreshChecklist();
        }

        private void UpdateDesktop(Keyboard keyboard)
        {
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectCase(0);
                if (keyboard.digit2Key.wasPressedThisFrame) SelectCase(1);
                if (keyboard.digit3Key.wasPressedThisFrame) SelectCase(2);
                if (keyboard.bKey.wasPressedThisFrame) SwitchSite();
                if (keyboard.iKey.wasPressedThisFrame) InspectWound();
                if (keyboard.rKey.wasPressedThisFrame) RestartCase();
                if (keyboard.lKey.wasPressedThisFrame) SetThai(!thai);
                if (keyboard.tabKey.wasPressedThisFrame) ToggleClipboard();
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (clipboard.IsOpen)
                    clipboard.ToggleMouse();
                else if (mouseTool != null)
                    ReturnMouseTool();
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (clipboard.IsOpen || overUI || !desktopCamera.pixelRect.Contains(position))
            {
                ResetContact();
                return;
            }

            bool tilt = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            UpdatePointer(desktopCamera.ScreenPointToRay(position), mouse.leftButton.wasPressedThisFrame,
                mouse.leftButton.isPressed, tilt);
        }

        private void ToggleClipboard()
        {
            if (mouseTool != null)
                ReturnMouseTool();
            clipboard.ToggleMouse();
        }

        private void UpdatePointer(Ray ray, bool clicked, bool pressed, bool tilt)
        {
            if (clicked && tapCollider.Raycast(ray, out _, 12f))
            {
                ActivateCoolingTap();
                return;
            }

            bool onStream = washStation.IsAimingAtStream(ray);
            bool onTarget = !IsComplete && CurrentPatient.targetCollider.Raycast(ray, out _, 12f);
            if (mouseTool == null)
            {
                ResetContact();
                if (!clicked && Physics.Raycast(ray, out var hover, 12f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    Highlight(hover.collider);

                if (pressed && onStream && washStation.TapOpen)
                {
                    washStation.WashFromMouse(Time.unscaledDeltaTime);
                    return;
                }

                if (clicked && onTarget && !inspected)
                    InspectWound();
                else if (clicked)
                    PickOrUseFixture(ray);
                return;
            }

            if (onStream && !tilt && mouseTool.Kind == ToolKind.Water)
            {
                mouseTool.MoveMouthTo(washStation.RefillPosition);
                washStation.TryRefill(mouseTool, Time.unscaledDeltaTime);
                ResetContact();
                return;
            }

            var surface = new Plane(Vector3.up, CurrentPatient.target.position);
            if (surface.Raycast(ray, out float distance))
                mouseTool.MoveForMouse(ray.GetPoint(distance), desktopCamera.transform.forward, tilt);

            if (UpdateContact(FindContact(mouseTool, onTarget)))
                ReturnMouseTool();
        }

        private void PickOrUseFixture(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            if (hit.collider == clipboard.HitBox)
            {
                ToggleClipboard();
                return;
            }

            if (washStation.IsGloveBox(hit.collider))
            {
                washStation.EquipFromMouse();
                return;
            }

            var tool = hit.collider.GetComponentInParent<FirstAidTool>();
            if (tool != null && (!IsComplete || tool.Kind == ToolKind.Water))
                mouseTool = tool;
        }

        private void ReturnMouseTool()
        {
            mouseTool.ReturnToTray();
            mouseTool = null;
            ResetContact();
        }

        private void Highlight(Collider hit)
        {
            var tool = hit.GetComponentInParent<FirstAidTool>();
            if (tool != null && !tool.IsGrabbed)
                highlightedTool = tool;
            else if (hit == tapCollider || washStation.IsGloveBox(hit))
            {
                highlightedFixture = hit.transform;
                highlightedFixtureText = hit == tapCollider ?
                    washStation.TapOpen ? "Turn tap off" : "Turn tap on" : "Glove box";
            }
        }

        private void UpdateXR()
        {
            for (int hand = 0; hand < 2; hand++)
            {
                if (xrHands.TryGetFreePalm(hand, out Vector3 palm))
                    washStation.WashAt(hand, palm, Time.unscaledDeltaTime);
            }

            FirstAidTool reaching = null;
            foreach (var tool in tools)
            {
                if (tool.IsHovered)
                {
                    highlightedTool = tool;
                    break;
                }
            }

            foreach (var tool in tools)
            {
                if (!tool.IsGrabbed || washStation.TryRefill(tool, Time.unscaledDeltaTime) || IsComplete)
                    continue;

                var contact = FindContact(tool, tool.Reaches(CurrentPatient.target.position, ContactRadius));
                reaching ??= contact;
            }

            if (highlightedTool == null)
            {
                if (coolingTap.isHovered)
                    Highlight(tapCollider);
                else if (washStation.GloveBoxHovered)
                    Highlight(washStation.GloveCollider);
            }

            if (highlightedTool == null && highlightedFixture == null && simulatorMode &&
                Mouse.current is { } pointer && !pointer.rightButton.isPressed)
            {
                var ray = xrCamera.ScreenPointToRay(pointer.position.ReadValue());
                if (Physics.Raycast(ray, out var hover, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    Highlight(hover.collider);
                }
            }

            UpdateContact(reaching);
        }

        private FirstAidTool FindContact(FirstAidTool tool, bool overlapsWound)
        {
            if (IsComplete)
                return null;
            Vector3 wound = CurrentPatient.target.position;
            bool aboveWound = tool.IsBottle && tool.IsAbove(wound);
            if (Time.unscaledTime < errorUntil && tool != wrongTool && inspected &&
                (overlapsWound || aboveWound) && CurrentStep.Accepts(tool.Kind))
            {
                errorUntil = 0f;
                nextAimHint = 0f;
                notice.Hide();
            }

            if (!tool.IsBottle)
                return overlapsWound ? tool : null;
            if (tool.Liquid <= 0f && aboveWound && Time.unscaledTime >= nextAimHint)
            {
                ShowNotice(tool.Kind == ToolKind.Water ? "Water empty. Refill at the tap." : "Saline empty. Pick up a fresh bottle.",
                    tool, true, 2f);
                nextAimHint = Time.unscaledTime + 3f;
            }

            bool flowing = tool.TryPour(wound, out Vector3 origin, out Vector3 impact, out bool hitsWound);
            if (flowing)
                tool.Drain(Time.unscaledDeltaTime);
            if (flowing && (!pouring || hitsWound))
            {
                pouring = true;
                streamStart = origin;
                streamEnd = impact;
            }

            if (hitsWound)
                return tool;

            if (inspected && CurrentStep.Accepts(tool.Kind) && Time.unscaledTime >= nextAimHint &&
                aboveWound && Time.unscaledTime >= errorUntil)
            {
                ShowNotice(flowing ? "Aim the stream at the wound." : "Tilt the bottle to pour.",
                    tool, false, 1.7f);
                nextAimHint = Time.unscaledTime + 3f;
            }

            return null;
        }

        private bool UpdateContact(FirstAidTool tool)
        {
            if (contactTool != tool)
            {
                ResetContact();
                contactTool = tool;
            }

            if (tool == null || contactHandled || IsComplete)
                return false;

            bool correct = washStation.Ready && inspected && CurrentStep.Accepts(tool.Kind);
            contactTime += Time.unscaledDeltaTime;
            if (contactTime < (correct ? CurrentStep.holdTime : 0.38f))
                return false;

            contactHandled = true;
            if (correct)
            {
                CompleteStep();
                return true;
            }

            ShowWrongOrder(tool);
            return false;
        }

        private void ShowWrongOrder(FirstAidTool tool)
        {
            string instruction = !washStation.Ready ? PreparationText() :
                !inspected ? "Inspect the injury before choosing a treatment." : CurrentStep.instruction;
            string message = $"{FirstAidLanguage.Get("Wrong order", thai)}\n{FirstAidLanguage.Get(instruction, thai)}";
            Transform hand = tool != null ? tool.HoldingHand : null;
            notice.Show(message, hand, CurrentPatient.target.position, hand != null ? 0.17f : 0.16f, true, 3f);
            wrongTool = tool;
            errorUntil = Time.unscaledTime + 2.6f;
        }

        private void ShowNotice(string message, FirstAidTool tool, bool error, float seconds)
        {
            Transform hand = tool != null ? tool.HoldingHand : null;
            notice.Show(FirstAidLanguage.Get(message, thai), hand, CurrentPatient.target.position,
                hand != null ? 0.17f : 0.16f, error, seconds);
        }

        private void ResetContact()
        {
            contactTool = null;
            contactTime = 0f;
            contactHandled = false;
        }

        private void UpdateCooling()
        {
            bool allowed = !IsComplete && inspected && washStation.Ready &&
                CurrentStep.tool == ToolKind.Tap && washStation.TapOpen;
            if (!allowed)
            {
                cooling = false;
                return;
            }

            if (!cooling && coolingTime <= 0f)
                ShowNotice("20 min of cooling (simulated)", null, false, 2.1f);

            cooling = true;
            coolingTime += Time.unscaledDeltaTime;
            if (coolingTime >= CurrentStep.holdTime)
            {
                cooling = false;
                CompleteStep();
            }
        }

        private string PreparationText() => washStation.BothWashed ?
            "Use the glove box with each hand." : "Open the tap and wash both hands.";

        private void CompleteStep()
        {
            var finished = CurrentStep;
            if (finished.tool == ToolKind.Water && CurrentPatient.debris != null)
                CurrentPatient.debris.SetActive(false);

            stepIndex++;
            pouring = false;
            errorUntil = 0f;
            if (IsComplete)
            {
                completedSites |= 1 << (currentCase * 2 + siteIndex);
                CurrentPatient.target.gameObject.SetActive(false);
                if (finished.tool == ToolKind.Dressing)
                    CurrentPatient.woundGroup.SetActive(false);
                CurrentPatient.covering.SetActive(true);
            }

            checklistDirty = true;
            nextAimHint = Time.unscaledTime + 3f;
            ShowNotice(finished.completion, null, false, 2.5f);
        }

        private void RefreshChecklist()
        {
            bool useTap = !washStation.BothWashed && !washStation.TapOpen ||
                inspected && !IsComplete && CurrentStep.tool == ToolKind.Tap;
            if (tapMarker.activeSelf != useTap)
                tapMarker.SetActive(useTap);

            if (checklistDirty)
            {
                string instruction = !washStation.Ready ? PreparationText() :
                    !inspected ? "Inspect the marked injury to begin." :
                    IsComplete ? "Wound covered. Try another site or choose a new case." :
                    CurrentStep.instruction;
                clipboard.Refresh(thai, currentCase, siteIndex, Locations[currentCase, siteIndex],
                    Checklists[currentCase], inspected, stepIndex, instruction, completedSites);
                checklistDirty = false;
            }

            float progress = !IsComplete && inspected && !contactHandled
                ? Mathf.Clamp01((CurrentStep.tool == ToolKind.Tap ? coolingTime : contactTime) / CurrentStep.holdTime)
                : 0f;
            clipboard.SetProgress(washStation.Ready ? progress : washStation.WashProgress);

            if (!IsComplete)
            {
                Color color = Time.unscaledTime < errorUntil ? ErrorColor : inspected ? ReadyColor : InspectColor;
                markerProperties.SetColor(BaseColor, color);
                CurrentPatient.marker.SetPropertyBlock(markerProperties);
            }
        }

        private void SetDesktopMode(bool value)
        {
            ReturnTools();
            desktopMode = value;
            foreach (var tool in tools)
                tool.SetXRGrabbingEnabled(!value);
            coolingTap.enabled = !value;
            washStation.SetXR(!value);
            ResetContact();
            checklistDirty = true;

            desktopCamera.gameObject.SetActive(value);
            desktopCamera.rect = new Rect(0f, 0f, 1f, 1f);
            if (value)
                FitDesktopCamera();
            xrCamera.enabled = !value;
            var xrListener = xrCamera.GetComponent<AudioListener>();
            if (xrListener != null)
                xrListener.enabled = !value;

            bool simulatorActive = false;
#if UNITY_EDITOR
            if (!value && !HasHeadset() && simulatorPrefab != null && simulator == null)
                simulator = Instantiate(simulatorPrefab);

            simulatorActive = !value && simulator != null;
            if (simulator != null)
                simulator.SetActive(simulatorActive);
#endif

            Camera view = value ? desktopCamera : xrCamera;
            simulatorMode = simulatorActive;
            clipboard.SetMode(view, value, simulatorActive);
            xrHands.SetVisible(!value);
            toolLabel.SetCamera(view);
            toolLabel.Hide();
            notice.SetCamera(view);
            notice.Hide();

            if (EventSystem.current != null &&
                EventSystem.current.TryGetComponent<XRUIInputModule>(out var module))
            {
                module.enableXRInput = !value;
                module.enableMouseInput = value;
            }

        }

        private void FitDesktopCamera()
        {
            float aspect = desktopCamera.pixelWidth / (float)Mathf.Max(1, desktopCamera.pixelHeight);
            desktopCamera.orthographicSize = Mathf.Max(1.06f, 1.25f / Mathf.Max(0.1f, aspect));
        }

        private bool HasHeadset()
        {
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
            {
                if (display.running)
                    return true;
            }

            return false;
        }

        private void ReturnTools()
        {
            foreach (var tool in tools)
                tool.ReturnToTray();

            mouseTool = null;
        }
    }
}

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
        [SerializeField] private FirstAidPanel desktopPanel;
        [SerializeField] private FirstAidPanel xrPanel;
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
        private FirstAidTool mouseTool;
        private FirstAidTool contactTool;
        private float contactTime;
        private float coolingTime;
        private float feedbackUntil;
        private int currentCase;
        private int siteIndex;
        private int stepIndex;
        private int corrections;
        private int completedSites;
        private bool desktopMode;
        private bool inspected;
        private bool cooling;
        private bool contactHandled;
        private string feedback;
        private bool panelDirty;
        private MaterialPropertyBlock markerProperties;

        private const float ContactRadius = 0.115f;
        private const float HoldSeconds = 0.75f;
        private static readonly Color InspectColor = new(1f, 0.71f, 0.31f);
        private static readonly Color ReadyColor = new(0.13f, 0.86f, 0.73f);
        private static readonly Color ErrorColor = new(1f, 0.41f, 0.31f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private sealed class Step
        {
            public readonly ToolKind tool;
            public readonly string title;
            public readonly string instruction;
            public readonly string reason;
            public readonly float holdTime;

            public Step(ToolKind tool, string title, string instruction, string reason,
                float holdTime = HoldSeconds)
            {
                this.tool = tool;
                this.title = title;
                this.instruction = instruction;
                this.reason = reason;
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
                    "Rinsing removes grit that can stay under a dressing."),
                new Step(ToolKind.Dressing, "Cover the graze", "Place a sterile dressing over the wound.",
                    "The dressing protects the cleaned area.")
            },
            new[]
            {
                new Step(ToolKind.Gauze, "Control the bleeding", "Hold clean gauze against the marked cut.",
                    "Pressure helps stop bleeding before you clean the cut.", 1.25f),
                new Step(ToolKind.Water, "Rinse the cut", "Use clean water or saline once the bleeding stops.",
                    "Cleaning before covering helps remove dirt."),
                new Step(ToolKind.Dressing, "Cover the cut", "Place a sterile dressing over the cleaned cut.",
                    "A dressing protects the cleaned wound.")
            },
            new[]
            {
                new Step(ToolKind.Tap, "Cool with running water", "Select the sink tap to simulate 20 min of cooling.",
                    "Running water carries heat away from the skin.", 2.0f),
                new Step(ToolKind.Film, "Cover the scald", "Lay a loose sheet of cling film over the scald.",
                    "A loose cover protects the skin and leaves space for swelling.")
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

        private void Start()
        {
            markerProperties = new MaterialPropertyBlock();
            desktopPanel.Bind(this, true);
            xrPanel.Bind(this, false);
            coolingTap.selectEntered.AddListener(_ => ActivateCoolingTap());
            SetDesktopMode(!HasHeadset());
            SelectCase(0);
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
            currentCase = nextCase;
            siteIndex = nextSite;
            stepIndex = 0;
            corrections = 0;
            inspected = false;
            cooling = false;
            coolingTime = 0f;
            ResetContact();
            feedback = null;
            feedbackUntil = 0f;

            for (int i = 0; i < patients.Length; i++)
            {
                patients[i].patient.SetActive(i == currentCase);
                ResetSite(patients[i], siteIndex == 0);
                ResetSite(alternateSites[i], siteIndex == 1);
            }

            panelDirty = true;
            RefreshPanel();
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
            SetFeedback(Observations[currentCase], 5.5f);
        }

        public void ActivateCoolingTap()
        {
            if (IsComplete)
                return;

            if (!inspected)
            {
                SetFeedback("Inspect the injury before choosing a treatment.", 3.5f);
                return;
            }

            if (CurrentStep.tool != ToolKind.Tap)
            {
                corrections++;
                SetFeedback(CurrentStep.reason, 4.0f);
                return;
            }

            if (cooling)
                return;

            cooling = true;
            coolingTime = 0f;
            SetFeedback("Simulating cooling under running water.", 3.0f);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (Application.isEditor && keyboard != null && keyboard.f2Key.wasPressedThisFrame)
            {
                SetDesktopMode(!desktopMode);
                RefreshPanel();
                return;
            }

            if (desktopMode)
            {
                FitDesktopCamera();
                UpdateDesktop(keyboard);
            }
            else
            {
                UpdateXR();
            }

            UpdateCooling();
            UpdateWater();
            if (feedbackUntil > 0f && Time.unscaledTime >= feedbackUntil)
            {
                feedback = null;
                feedbackUntil = 0f;
                panelDirty = true;
            }

            RefreshPanel();
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
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.rightButton.wasPressedThisFrame && mouseTool != null)
                ReturnMouseTool();

            Vector2 position = mouse.position.ReadValue();
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI && desktopCamera.pixelRect.Contains(position))
                UpdatePointer(desktopCamera.ScreenPointToRay(position), mouse.leftButton.wasPressedThisFrame);
            else
                ResetContact();
        }

        private void UpdatePointer(Ray ray, bool clicked)
        {
            if (IsComplete)
                return;

            bool onTarget = CurrentPatient.targetCollider.Raycast(ray, out _, 12f);
            if (mouseTool == null)
            {
                ResetContact();
                if (clicked && onTarget && !inspected)
                    InspectWound();
                else if (clicked)
                    PickOrUseFixture(ray);
                return;
            }

            var surface = new Plane(Vector3.up, CurrentPatient.target.position + Vector3.up * 0.09f);
            if (surface.Raycast(ray, out float distance))
                mouseTool.MoveForMouse(ray.GetPoint(distance));

            if (UpdateContact(onTarget ? mouseTool : null))
                ReturnMouseTool();
        }

        private void PickOrUseFixture(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            if (hit.collider == tapCollider)
            {
                ActivateCoolingTap();
                return;
            }

            mouseTool = hit.collider.GetComponentInParent<FirstAidTool>();
            if (mouseTool != null)
                SetFeedback("Bring " + mouseTool.Label.ToLowerInvariant() + " to the wound marker.", 2.5f);
        }

        private void ReturnMouseTool()
        {
            mouseTool.ReturnToTray();
            mouseTool = null;
            ResetContact();
        }

        private void UpdateXR()
        {
            FirstAidTool reaching = null;
            if (!IsComplete)
            {
                foreach (var tool in tools)
                {
                    if (tool.IsGrabbed && tool.Reaches(CurrentPatient.target.position, ContactRadius))
                    {
                        reaching = tool;
                        break;
                    }
                }
            }

            UpdateContact(reaching);
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

            bool correct = inspected && CurrentStep.Accepts(tool.Kind);
            contactTime += Time.unscaledDeltaTime;
            if (contactTime < (correct ? CurrentStep.holdTime : 0.40f))
                return false;

            contactHandled = true;
            if (correct)
            {
                CompleteStep();
                return true;
            }

            if (!inspected)
                SetFeedback("Inspect the injury before choosing a treatment.", 3.5f);
            else
            {
                corrections++;
                SetFeedback(CurrentStep.reason, 4.0f);
            }

            return true;
        }

        private void ResetContact()
        {
            contactTool = null;
            contactTime = 0f;
            contactHandled = false;
        }

        private void UpdateCooling()
        {
            if (!cooling)
                return;

            coolingTime += Time.unscaledDeltaTime;
            if (coolingTime >= CurrentStep.holdTime)
            {
                cooling = false;
                CompleteStep();
            }
        }

        private void UpdateWater()
        {
            sinkWater.SetFlow(spout.position, sinkBowl.position, cooling);
            bool rinsing = inspected && !IsComplete && contactTool != null && !contactHandled
                && CurrentStep.tool == ToolKind.Water && CurrentStep.Accepts(contactTool.Kind);
            Vector3 wound = CurrentPatient.target.position;
            Vector3 from = rinsing ? contactTool.transform.position + Vector3.up * 0.08f : wound;
            rinseWater.SetFlow(from, wound, rinsing);
        }

        private void CompleteStep()
        {
            var finished = CurrentStep;
            if (finished.tool == ToolKind.Water && CurrentPatient.debris != null)
                CurrentPatient.debris.SetActive(false);

            stepIndex++;
            if (IsComplete)
            {
                completedSites |= 1 << (currentCase * 2 + siteIndex);
                CurrentPatient.target.gameObject.SetActive(false);
                CurrentPatient.covering.SetActive(true);
            }

            SetFeedback(finished.reason, 5.0f);
        }

        private void SetFeedback(string message, float seconds)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTime + seconds;
            panelDirty = true;
        }

        private void RefreshPanel()
        {
            var panel = desktopMode ? desktopPanel : xrPanel;
            bool useTap = inspected && !IsComplete && CurrentStep.tool == ToolKind.Tap;
            if (tapMarker.activeSelf != useTap)
                tapMarker.SetActive(useTap);

            if (panelDirty)
            {
                string instruction = !inspected ? "Inspect the marked injury to begin." :
                    IsComplete ? "Wound covered. Try another site or choose a new case." :
                    CurrentStep.instruction;
                panel.Show(currentCase, siteIndex, Locations[currentCase, siteIndex], Checklists[currentCase],
                    inspected, stepIndex, instruction, feedback, corrections, completedSites);
                panelDirty = false;
            }

            float progress = !IsComplete && inspected
                ? Mathf.Clamp01((cooling ? coolingTime : contactTime) / CurrentStep.holdTime)
                : 0f;
            panel.ShowHold(progress);

            if (!IsComplete)
            {
                Color color = !inspected ? InspectColor :
                    contactTool != null && !CurrentStep.Accepts(contactTool.Kind) ? ErrorColor : ReadyColor;
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

            ResetContact();
            panelDirty = true;
            desktopCamera.gameObject.SetActive(value);
            if (value)
                FitDesktopCamera();
            desktopCanvas.gameObject.SetActive(value);
            xrCamera.enabled = !value;
            xrCanvas.gameObject.SetActive(!value);
            var xrListener = xrCamera.GetComponent<AudioListener>();
            if (xrListener != null)
                xrListener.enabled = !value;

            if (EventSystem.current != null &&
                EventSystem.current.TryGetComponent<XRUIInputModule>(out var module))
            {
                module.enableXRInput = !value;
                module.enableMouseInput = value;
            }

#if UNITY_EDITOR
            if (!value && !HasHeadset() && simulatorPrefab != null && simulator == null)
                simulator = Instantiate(simulatorPrefab);

            if (simulator != null)
                simulator.SetActive(!value);
#endif
        }

        private void FitDesktopCamera()
        {
            float aspect = desktopCamera.pixelWidth / (float)Mathf.Max(1, desktopCamera.pixelHeight);
            desktopCamera.orthographicSize = Mathf.Max(1.04f, 1.27f / Mathf.Max(0.1f, aspect));
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

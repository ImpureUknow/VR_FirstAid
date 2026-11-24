using UnityEngine;
using UnityEngine.UI;

namespace FirstAid
{
    public sealed class FirstAidPanel : MonoBehaviour
    {
        [SerializeField] private Button[] caseButtons;
        [SerializeField] private Text[] caseLabels;
        [SerializeField] private Text[] checkmarks;
        [SerializeField] private Text location;
        [SerializeField] private Text phase;
        [SerializeField] private Text[] steps;
        [SerializeField] private Text instruction;
        [SerializeField] private Text feedback;
        [SerializeField] private Image holdBar;
        [SerializeField] private Button restart;
        [SerializeField] private Button next;
        [SerializeField] private Text inputHelp;
        [SerializeField] private Button inspect;
        [SerializeField] private Button switchSite;
        [SerializeField] private Text siteLabel;
        [SerializeField] private Text total;

        private static readonly Color Accent = new(0.40f, 0.91f, 0.79f);
        private static readonly Color Selected = new(0.12f, 0.34f, 0.34f);
        private static readonly Color Tile = new(0.12f, 0.20f, 0.24f);
        private static readonly Color Dim = new(0.65f, 0.75f, 0.76f);

        public void Bind(FirstAidSession session, bool desktop)
        {
            for (int index = 0; index < caseButtons.Length; index++)
            {
                int selection = index;
                caseButtons[index].onClick.AddListener(() => session.SelectCase(selection));
            }

            inspect.onClick.AddListener(session.InspectWound);
            switchSite.onClick.AddListener(session.SwitchSite);
            restart.onClick.AddListener(session.RestartCase);
            next.onClick.AddListener(() => session.SelectCase((session.CaseIndex + 1) % caseButtons.Length));
            inputHelp.text = desktop
                ? "Click a tool. Hold it over the wound.\nRight-click to return.  I Inspect  B Switch site  R Replay"
                : "Point and trigger at the menu.\nGrip to use the tap or hold a tool at the wound.";

            if (Application.isEditor)
                inputHelp.text += desktop ? "\nF2  XR controls" : "\nF2  Mouse controls";
        }

        public void Show(int selectedCase, int siteIndex, string site, string[] checklist,
            bool inspected, int currentStep, string guidance, string message, int corrections, int completedSites)
        {
            bool finished = currentStep == checklist.Length;
            siteLabel.text = "Site " + (siteIndex + 1) + " / 2";
            location.text = site.ToUpperInvariant();
            phase.text = !inspected ? "INSPECT THE INJURY" :
                finished ? "SITE COMPLETE" : $"STEP {currentStep + 1} OF {checklist.Length}";
            instruction.text = guidance;
            inspect.interactable = !inspected;
            next.interactable = finished;
            feedback.text = finished ? $"Hints used  {corrections}" + (message != null ? "\n" + message : "") :
                message ?? (inspected ? "Follow the steps at your own pace." : "Look at the highlighted area.");

            int completedCount = 0;
            for (int index = 0; index < caseButtons.Length; index++)
            {
                bool active = index == selectedCase;
                caseButtons[index].image.color = active ? Selected : Tile;
                caseLabels[index].color = active ? Accent : Color.white;
                int mask = (completedSites >> (2 * index)) & 3;
                int count = (mask & 1) + (mask >> 1);
                completedCount += count;
                checkmarks[index].text = count == 2 ? "DONE" : $"{count} / 2";
            }

            total.text = $"{completedCount} / {caseButtons.Length * 2} SITES COMPLETE";
            for (int index = 0; index < steps.Length; index++)
            {
                bool present = index < checklist.Length;
                steps[index].enabled = present;
                if (!present)
                    continue;

                string marker = index < currentStep ? "✓" : (index + 1).ToString("00");
                steps[index].text = marker + "   " + checklist[index];
                steps[index].color = inspected && index <= currentStep ? Accent : Dim;
            }
        }

        public void ShowHold(float progress) => holdBar.fillAmount = progress;
    }
}

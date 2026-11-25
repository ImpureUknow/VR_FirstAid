using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace FirstAid
{
    internal sealed class FirstAidWashStation
    {
        private readonly FirstAidSinkState state = new();
        private readonly Transform outlet;
        private readonly Transform bowl;
        private readonly XRSimpleInteractable gloveBox;
        private readonly BoxCollider gloveCollider;
        private readonly Action<string, Vector3, bool> feedback;
        private readonly Transform lever;
        private readonly Vector3 leverPosition;
        private readonly Quaternion leverRotation;
        private readonly Vector3 leverPivot;
        private Vector3? receivingMouth;

        public FirstAidWashStation(Transform parent, Transform outlet, Transform bowl, Collider tapCollider,
            Action<string, Vector3, bool> feedback)
        {
            this.outlet = outlet;
            this.bowl = bowl;
            this.feedback = feedback;

            var room = GameObject.Find("First Aid Room");
            var faucet = room == null ? null : room.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer => renderer.name == "Faucet lever");
            if (faucet != null)
            {
                lever = faucet.transform;
                leverPosition = lever.position;
                leverRotation = lever.rotation;
                leverPivot = faucet.bounds.center - Vector3.right * faucet.bounds.extents.x * 0.60f;
            }
            else
            {
                leverPivot = tapCollider.bounds.center;
            }

            var supplies = GameObject.Find("Supplies");
            var carton = supplies == null ? null : supplies.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer => renderer.name == "GloveBox_Carton");
            var fixture = new GameObject("Use Glove Box");
            fixture.transform.SetParent(parent, false);
            var bounds = carton != null ? carton.bounds : new Bounds(new Vector3(-0.92f, 0.93f, -0.02f),
                new Vector3(0.20f, 0.083f, 0.12f));
            fixture.transform.position = bounds.center;
            gloveCollider = fixture.AddComponent<BoxCollider>();
            gloveCollider.size = Vector3.Max(bounds.size + new Vector3(0.028f, 0.036f, 0.025f),
                new Vector3(0.12f, 0.07f, 0.095f));
            gloveBox = fixture.AddComponent<XRSimpleInteractable>();
            if (!gloveBox.colliders.Contains(gloveCollider))
                gloveBox.colliders.Add(gloveCollider);
            gloveBox.selectEntered.AddListener(args =>
            {
                var side = args.interactorObject.handedness;
                if (side == InteractorHandedness.Left)
                    EquipGlove(0);
                else if (side == InteractorHandedness.Right)
                    EquipGlove(1);
            });
        }

        public bool TapOpen => state.TapOpen;
        public bool BothWashed => state.IsWashed(0) && state.IsWashed(1);
        public bool Ready => state.BothProtected;
        public Vector3 StreamTarget => receivingMouth ?? bowl.position;
        public Vector3 RefillPosition => outlet.position + Vector3.down * 0.031f;
        public Collider GloveCollider => gloveCollider;
        public bool GloveBoxHovered => gloveBox.isHovered;
        public bool HasGlove(int hand) => state.HasGlove(hand);
        public float WashProgress => (state.WashProgress(0) + state.WashProgress(1)) * 0.5f;

        public void BeginFrame() => receivingMouth = null;
        public void ResetHands() => state.ResetHands();

        public void SetXR(bool active) => gloveBox.enabled = active;
        public bool IsGloveBox(Collider target) => target == gloveCollider;

        public void ToggleTap()
        {
            state.ToggleTap();
            if (lever != null)
            {
                lever.SetPositionAndRotation(leverPosition, leverRotation);
                if (TapOpen)
                    lever.RotateAround(leverPivot, Vector3.up, -23f);
            }

            feedback(TapOpen ? "Tap on." : "Tap off.", outlet.position, false);
        }

        public bool IsAimingAtStream(Ray ray)
        {
            Vector3 center = Vector3.Lerp(outlet.position, bowl.position, 0.47f);
            float distance = Vector3.Dot(center - ray.origin, ray.direction);
            return distance > 0f && distance < 12f &&
                (ray.GetPoint(distance) - center).sqrMagnitude < 0.117f * 0.117f;
        }

        public void WashAt(int hand, Vector3 position, float seconds)
        {
            if (InsideStream(position, 0.10f))
                Wash(hand, seconds);
        }

        public void WashFromMouse(float seconds)
        {
            Wash(0, seconds);
            Wash(1, seconds);
        }

        private void Wash(int hand, float seconds)
        {
            if (!state.AdvanceWash(hand, seconds))
                return;

            if (BothWashed)
                feedback("Hands washed. Put on gloves.", outlet.position, false);
            else
                feedback(hand == 0 ? "Left hand washed." : "Right hand washed.", outlet.position, false);
        }

        public void EquipFromMouse()
        {
            if (!state.HasGlove(0))
                EquipGlove(0);
            else
                EquipGlove(1);
        }

        public void EquipGlove(int hand)
        {
            if (state.HasGlove(hand))
                return;

            bool equipped = state.TryWearGlove(hand);
            string message = !equipped ? "Wash this hand first." : Ready ? "Both gloves on." :
                hand == 0 ? "Left glove on." : "Right glove on.";
            feedback(message, gloveBox.transform.position, !equipped);
        }

        public bool TryRefill(FirstAidTool tool, float time)
        {
            if (tool.Kind != ToolKind.Water || !TapOpen || !tool.IsUpright ||
                !InsideStream(tool.Mouth, 0.077f))
                return false;

            receivingMouth = tool.Mouth;
            if (tool.Refill(time))
                feedback("Water bottle full.", outlet.position, false);
            return true;
        }

        private bool InsideStream(Vector3 point, float radius)
        {
            Vector3 delta = point - outlet.position;
            return point.y <= outlet.position.y + 0.036f && point.y >= bowl.position.y - 0.026f &&
                delta.x * delta.x + delta.z * delta.z <= radius * radius;
        }
    }
}

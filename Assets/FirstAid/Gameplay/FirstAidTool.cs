using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FirstAid
{
    public enum ToolKind
    {
        Water,
        Gauze,
        Dressing,
        Film,
        Saline,
        Tap
    }

    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(XRGrabInteractable))]
    public sealed class FirstAidTool : MonoBehaviour
    {
        [SerializeField] private ToolKind kind;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private BoxCollider hitBox;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private bool dropped;

        public ToolKind Kind => kind;
        public bool IsGrabbed => grab.isSelected;
        public string Label => kind switch
        {
            ToolKind.Water => "Clean water",
            ToolKind.Gauze => "Gauze",
            ToolKind.Dressing => "Dressing",
            ToolKind.Film => "Cling film",
            ToolKind.Saline => "Saline",
            _ => kind.ToString()
        };

        public void SetKind(ToolKind value) => kind = value;

        public void SetXRGrabbingEnabled(bool value) => grab.enabled = value;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            hitBox = GetComponent<BoxCollider>();
            homePosition = transform.position;
            homeRotation = transform.rotation;
            grab.selectExited.AddListener(_ => dropped = true);
        }

        private void LateUpdate()
        {
            if (!dropped || IsGrabbed)
                return;

            dropped = false;
            ReturnToTray();
        }

        public bool Reaches(Vector3 point, float radius)
        {
            return (hitBox.ClosestPoint(point) - point).sqrMagnitude <= radius * radius;
        }

        public void MoveForMouse(Vector3 position)
        {
            transform.position = position;
        }

        public void ReturnToTray()
        {
            if (grab.isSelected)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);

            body.isKinematic = true;
            body.position = homePosition;
            body.rotation = homeRotation;
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }
    }
}

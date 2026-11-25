using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
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
        private const float TiltThreshold = 0.423f; // cos(65 degrees)
        private const float Gravity = 3.8f;
        private const float PourRadius = 0.103f;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private BoxCollider hitBox;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Vector3 localSpout;
        private bool dropped;
        private float liquid = 1f;

        public ToolKind Kind => kind;
        public bool IsGrabbed => grab.isSelected;
        public bool IsHovered => grab.isHovered && !IsGrabbed;

        public string DisplayName(bool thai)
        {
            string title = FirstAidLanguage.Get(kind switch
            {
                ToolKind.Water => "Clean water",
                ToolKind.Gauze => "Gauze",
                ToolKind.Dressing => "Dressing",
                ToolKind.Film => "Cling film",
                ToolKind.Saline => "Saline",
                _ => "Tap"
            }, thai);
            return IsBottle ? $"{title}  {Mathf.RoundToInt(liquid * 100f)}%" : title;
        }

        public bool IsBottle => kind == ToolKind.Water || kind == ToolKind.Saline;
        public float Liquid => liquid;
        public bool IsUpright => Vector3.Dot(transform.up, Vector3.up) >= 0.85f;
        public Vector3 Mouth => transform.TransformPoint(localSpout);
        internal Vector3 BottleGripCenter => transform.TransformPoint(hitBox.center);
        internal float BottleRadius => Mathf.Min(transform.TransformVector(Vector3.right * hitBox.size.x).magnitude,
            transform.TransformVector(Vector3.forward * hitBox.size.z).magnitude) * 0.5f;
        public Transform HoldingHand => grab.interactorsSelecting.Count > 0
            ? grab.interactorsSelecting[0].transform : null;

        public void SetKind(ToolKind value) => kind = value;
        public void SetXRGrabbingEnabled(bool value) => grab.enabled = value;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            hitBox = GetComponent<BoxCollider>();
            homePosition = transform.position;
            homeRotation = transform.rotation;
            localSpout = hitBox.center + Vector3.up * (hitBox.size.y * 0.5f - 0.008f);

            var handGrip = new GameObject("Hand Grip").transform;
            handGrip.SetParent(transform, false);
            handGrip.localPosition = IsBottle
                ? new Vector3(0f, 0f, -0.070f)
                : new Vector3(0f, -0.010f, -0.065f);
            grab.attachTransform = handGrip;
            grab.useDynamicAttach = false;
            grab.attachEaseInTime = 0f;
            grab.farAttachMode = InteractableFarAttachMode.Near;
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

        public bool IsAbove(Vector3 wound)
        {
            Vector3 separation = transform.position - wound;
            return separation.y > -0.04f && separation.y < 0.58f &&
                Vector2.SqrMagnitude(new Vector2(separation.x, separation.z)) < 0.25f * 0.25f;
        }

        public bool TryPour(Vector3 wound, out Vector3 origin, out Vector3 impact, out bool hitsWound)
        {
            origin = Mouth;
            impact = origin;
            hitsWound = false;
            if (!IsBottle || liquid <= 0f || Vector3.Dot(transform.up, Vector3.up) > TiltThreshold)
                return false;

            Vector3 velocity = transform.up * 0.24f;
            float height = origin.y - wound.y;
            if (height >= 0.019f && height <= 0.58f)
            {
                Vector3 atWoundHeight = Landing(origin, velocity, height);
                Vector2 difference = new(atWoundHeight.x - wound.x, atWoundHeight.z - wound.z);
                if (difference.sqrMagnitude <= PourRadius * PourRadius)
                {
                    impact = atWoundHeight;
                    hitsWound = true;
                    return true;
                }
            }

            float fall = 0.26f;
            if (Physics.Raycast(origin, Vector3.down, out var ground, 0.55f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && ground.collider != hitBox)
                fall = Mathf.Max(0.02f, ground.distance);
            impact = Landing(origin, velocity, fall);
            return true;
        }

        public void Drain(float seconds) => liquid = Mathf.Max(0f, liquid - seconds * 0.24f);

        public bool Refill(float seconds)
        {
            bool belowFull = liquid < 1f;
            liquid = Mathf.Min(1f, liquid + seconds * 0.62f);
            return belowFull && liquid >= 1f;
        }

        public void MoveMouthTo(Vector3 mouth)
        {
            transform.SetPositionAndRotation(mouth - homeRotation * localSpout, homeRotation);
        }

        private static Vector3 Landing(Vector3 start, Vector3 velocity, float height)
        {
            float time = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * Gravity * height)) / Gravity;
            return new Vector3(start.x + velocity.x * time, start.y - height, start.z + velocity.z * time);
        }

        public void MoveForMouse(Vector3 cursor, Vector3 viewDirection, bool tilt)
        {
            if (!IsBottle)
            {
                transform.position = cursor + Vector3.up * 0.09f;
                return;
            }

            Vector3 horizontal = Vector3.ProjectOnPlane(viewDirection, Vector3.up).normalized;
            Vector3 bottleAxis = (horizontal + Vector3.down * 0.62f).normalized;
            Quaternion rotation = tilt
                ? Quaternion.FromToRotation(Vector3.up, bottleAxis) * homeRotation
                : homeRotation;
            Vector3 nozzle = cursor + Vector3.up * 0.21f;
            transform.SetPositionAndRotation(nozzle - rotation * localSpout, rotation);
        }

        public void ReturnToTray()
        {
            if (grab.isSelected)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);

            if (kind == ToolKind.Saline && liquid <= 0f)
                liquid = 1f;

            body.isKinematic = true;
            body.position = homePosition;
            body.rotation = homeRotation;
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }
    }
}

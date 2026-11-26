using System.Linq;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace FirstAid
{
    internal sealed class FirstAidHandModel
    {
        // The imported meshes point along +Z with their palms facing +Y.
        private static readonly Vector3 PalmNormal = Vector3.up;
        private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        private static readonly XRHandJointID[] FirstTrackedJoints =
        {
            XRHandJointID.ThumbMetacarpal, XRHandJointID.IndexProximal,
            XRHandJointID.MiddleProximal, XRHandJointID.RingProximal, XRHandJointID.LittleProximal
        };

        private readonly SkinnedMeshRenderer mesh;
        private readonly Transform[,] joints = new Transform[5, 4];
        private readonly Quaternion[,] rest = new Quaternion[5, 3];
        private readonly Vector3[,] curl = new Vector3[5, 3];
        private readonly float side;
        private readonly float indexSide;
        private static Material skin;
        private static Material glove;
        private float grip;
        private bool gloved;

        public Transform Root { get; }
        public Vector3 Palm => Root.TransformPoint(new Vector3(0f, 0.013f, 0.06f));

        public static FirstAidHandModel Create(Transform parent, bool left)
        {
            string name = left ? "LeftHand" : "RightHand";
            var asset = Resources.Load<GameObject>("PlayerHands/" + name);
            if (asset == null)
                return null;

            var instance = Object.Instantiate(asset, parent, false);
            instance.name = left ? "Player Left Hand" : "Player Right Hand";
            return new FirstAidHandModel(instance.transform, left);
        }

        private FirstAidHandModel(Transform root, bool left)
        {
            Root = root;
            side = left ? -1f : 1f;
            mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
            EnsureMaterials();
            mesh.sharedMaterial = skin;
            mesh.updateWhenOffscreen = true;

            var hierarchy = root.GetComponentsInChildren<Transform>();
            string prefix = left ? "J_Left_Hand" : "J_Right_Hand";
            for (int finger = 0; finger < Fingers.Length; finger++)
            {
                for (int joint = 0; joint < 4; joint++)
                    joints[finger, joint] = hierarchy.First(node => node.name == prefix + Fingers[finger] + (joint + 1));

                for (int joint = 0; joint < 3; joint++)
                {
                    var bone = joints[finger, joint];
                    rest[finger, joint] = bone.localRotation;
                    Vector3 inward = PalmNormal;
                    if (finger == 0)
                        inward.x = -Mathf.Sign(root.InverseTransformPoint(bone.position).x) * 0.55f;
                    Vector3 axis = Vector3.Cross(joints[finger, joint + 1].position - bone.position,
                        root.TransformDirection(inward));
                    curl[finger, joint] = bone.InverseTransformDirection(axis.normalized);
                }
            }
            indexSide = Mathf.Sign(root.InverseTransformPoint(joints[1, 0].position).x -
                root.InverseTransformPoint(joints[4, 0].position).x);
        }

        public void Dress(bool value)
        {
            if (gloved == value)
                return;

            gloved = value;
            mesh.sharedMaterial = value ? glove : skin;
        }

        public void Pose(bool holding, FirstAidTool bottle)
        {
            grip = Mathf.MoveTowards(grip, holding ? 1f : 0f, Time.unscaledDeltaTime * 12f);
            if (bottle == null)
            {
                Root.localPosition = Vector3.zero;
                Root.localRotation = Quaternion.Euler(Mathf.Lerp(-14f, -7f, grip), side * Mathf.Lerp(11f, -7f, grip), 0f) *
                    Quaternion.AngleAxis(side * Mathf.Lerp(106f, 83f, grip), Vector3.forward);
            }
            else
            {
                var frame = bottle.transform;
                Quaternion rotation = frame.rotation * Quaternion.Euler(0f, -side * 13f, 0f) *
                    Quaternion.AngleAxis(side * 90f, Vector3.forward);
                Vector3 towardBottle = rotation * PalmNormal;
                Vector3 palmCenter = bottle.BottleGripCenter - towardBottle * (bottle.BottleRadius + 0.010f);
                Root.SetPositionAndRotation(palmCenter - rotation * new Vector3(0f, 0.012f, 0.073f), rotation);
            }

            for (int finger = 0; finger < Fingers.Length; finger++)
            for (int joint = 0; joint < 3; joint++)
            {
                float relaxed = finger == 0 ? 7f : (joint == 0 ? 6f + finger * 3f : 8f + finger * 6f);
                float closed = finger == 0 ? (joint == 0 ? 24f : 31f) :
                    joint == 0 ? 38f : joint == 1 ? 54f : 37f;
                if (bottle != null)
                {
                    closed = finger == 0 ? (joint == 0 ? 17f : 26f) :
                        joint == 0 ? 25f : joint == 1 ? 40f : 38f;
                }

                float angle = Mathf.Lerp(relaxed, closed, grip);
                joints[finger, joint].localRotation = rest[finger, joint] *
                    Quaternion.AngleAxis(angle, curl[finger, joint]);
            }
        }

        public bool Follow(XRHand hand, Transform space)
        {
            if (!TryGetPosition(hand, space, XRHandJointID.Wrist, out var wrist) ||
                !TryGetPosition(hand, space, XRHandJointID.MiddleProximal, out var middle) ||
                !TryGetPosition(hand, space, XRHandJointID.IndexProximal, out var index) ||
                !TryGetPosition(hand, space, XRHandJointID.LittleProximal, out var little))
                return false;

            var forward = middle - wrist;
            Vector3 right = (index - little) * indexSide;
            Vector3 palmNormal = Vector3.Cross(forward.normalized, right.normalized);
            if (palmNormal.sqrMagnitude < 0.01f)
                return false;

            Root.SetPositionAndRotation(wrist, Quaternion.LookRotation(forward, palmNormal));
            for (int finger = 0; finger < Fingers.Length; finger++)
            for (int joint = 0; joint < 3; joint++)
            {
                var bone = joints[finger, joint];
                bone.localRotation = rest[finger, joint];
                var jointId = FirstTrackedJoints[finger] + joint;
                if (TryGetPosition(hand, space, jointId, out var start) &&
                    TryGetPosition(hand, space, jointId + 1, out var end))
                {
                    var segment = joints[finger, joint + 1].position - bone.position;
                    bone.rotation = Quaternion.FromToRotation(segment, end - start) * bone.rotation;
                }
            }

            return true;
        }

        public static bool TryGetPosition(XRHand hand, Transform space, XRHandJointID id, out Vector3 point)
        {
            point = default;
            if (!hand.isTracked || !hand.GetJoint(id).TryGetPose(out var pose))
                return false;

            point = space.TransformPoint(pose.position);
            return true;
        }

        private static void EnsureMaterials()
        {
            if (skin != null)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            skin = new Material(shader) { name = "Bare hand skin" };
            skin.SetTexture("_BaseMap", Resources.Load<Texture2D>("PlayerHands/Textures/BaseColor"));
            skin.SetFloat("_Smoothness", 0.27f);

            glove = new Material(shader) { name = "Teal nitrile glove" };
            glove.SetColor("_BaseColor", new Color(0.14f, 0.59f, 0.60f));
            glove.SetFloat("_Smoothness", 0.37f);
        }
    }
}

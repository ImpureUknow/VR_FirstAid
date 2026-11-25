using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace FirstAid
{
    internal sealed class FirstAidXRHands
    {
        private readonly Hand left;
        private readonly Hand right;

        private FirstAidXRHands(Hand left, Hand right)
        {
            this.left = left;
            this.right = right;
        }

        public static FirstAidXRHands Create(Camera xrCamera)
        {
            var rig = xrCamera.transform.root;
            var modality = rig.GetComponentInChildren<XRInputModalityManager>(true);
            foreach (var node in rig.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == "Affordance Callouts Left" || node.name == "Affordance Callouts Right")
                    node.gameObject.SetActive(false);
            }

            if (modality == null)
                return new FirstAidXRHands(null, null);

            var trackingSpace = xrCamera.transform.parent;
            var drivers = rig.GetComponentsInChildren<XRHandSkeletonDriver>(true);
            return new FirstAidXRHands(
                new Hand(modality.leftController, modality.leftHand, trackingSpace, drivers, true),
                new Hand(modality.rightController, modality.rightHand, trackingSpace, drivers, false));
        }

        public void SetVisible(bool visible)
        {
            left?.SetVisible(visible);
            right?.SetVisible(visible);
        }

        public bool TryGetFreePalm(int hand, out Vector3 palm)
        {
            palm = default;
            var source = hand == 0 ? left : right;
            return source != null && source.TryGetFreePalm(out palm);
        }

        public void UpdatePoses(bool leftGlove, bool rightGlove)
        {
            left?.UpdatePose(leftGlove);
            right?.UpdatePose(rightGlove);
        }

        private sealed class Hand
        {
            private readonly NearFarInteractor controllerInteractor;
            private readonly XRBaseInteractor[] trackedInteractors;
            private readonly XRHandSkeletonDriver[] drivers;
            private readonly FirstAidHandModel controllerModel;
            private readonly FirstAidHandModel trackedModel;
            private readonly Transform trackingSpace;
            private readonly List<XRHandSubsystem> subsystems = new();
            private readonly bool left;
            private XRHandSubsystem tracking;
            private bool visible;

            public Hand(GameObject controller, GameObject tracked, Transform space,
                XRHandSkeletonDriver[] allDrivers, bool left)
            {
                this.left = left;
                trackingSpace = space;

                if (controller != null)
                {
                    controllerInteractor = controller.GetComponentInChildren<NearFarInteractor>(true);
                    if (controllerInteractor != null)
                    {
                        var anchor = controllerInteractor.interactionAttachController?.transformToFollow ?? controller.transform;
                        controllerModel = FirstAidHandModel.Create(anchor, left);
                        if (controllerModel != null)
                        {
                            foreach (var node in controller.GetComponentsInChildren<Transform>(true))
                            {
                                if (node.name == "Left Controller Visual" || node.name == "Right Controller Visual" ||
                                    node.name == "Universal Controller Materials Variant")
                                    node.gameObject.SetActive(false);
                            }
                        }
                    }
                }

                if (tracked != null)
                {
                    trackedInteractors = tracked.GetComponentsInChildren<XRBaseInteractor>(true);
                    trackedModel = FirstAidHandModel.Create(space, left);
                    if (trackedModel != null)
                        trackedModel.Root.name += " (Tracked)";
                }

                var side = left ? Handedness.Left : Handedness.Right;
                drivers = System.Array.FindAll(allDrivers, driver => driver.handTrackingEvents?.handedness == side);
                SetVisible(false);
            }

            public void SetVisible(bool value)
            {
                visible = value;
                if (controllerModel != null)
                    controllerModel.Root.gameObject.SetActive(value);
                if (!value)
                    ShowTrackedModel(false);
            }

            public bool TryGetFreePalm(out Vector3 palm)
            {
                palm = default;
                if (controllerInteractor != null && controllerInteractor.gameObject.activeInHierarchy)
                {
                    if (controllerInteractor.hasSelection)
                        return false;

                    palm = controllerModel != null ? controllerModel.Palm : controllerInteractor.transform.position;
                    return true;
                }

                return trackedInteractors != null &&
                    !System.Array.Exists(trackedInteractors, interactor => interactor.isActiveAndEnabled && interactor.hasSelection) &&
                    TryGetTrackedHand(out var hand) &&
                    FirstAidHandModel.TryGetPosition(hand, trackingSpace, XRHandJointID.Palm, out palm);
            }

            public void UpdatePose(bool gloved)
            {
                if (!visible)
                    return;

                if (controllerModel != null && controllerInteractor.gameObject.activeInHierarchy)
                {
                    var tool = controllerInteractor.firstInteractableSelected?.transform.GetComponent<FirstAidTool>();
                    controllerModel.Pose(controllerInteractor.hasSelection, tool != null && tool.IsBottle ? tool : null);
                    controllerModel.Dress(gloved);
                }

                bool follow = trackedModel != null && TryGetTrackedHand(out var hand) && trackedModel.Follow(hand, trackingSpace);
                if (follow)
                    trackedModel.Dress(gloved);
                ShowTrackedModel(follow);
            }

            private bool TryGetTrackedHand(out XRHand hand)
            {
                hand = default;
                if (controllerInteractor != null && controllerInteractor.gameObject.activeInHierarchy)
                    return false;

                if (tracking == null || !tracking.running)
                {
                    SubsystemManager.GetSubsystems(subsystems);
                    tracking = subsystems.Find(subsystem => subsystem.running);
                }

                if (tracking == null)
                    return false;

                hand = left ? tracking.leftHand : tracking.rightHand;
                return hand.isTracked;
            }

            private void ShowTrackedModel(bool value)
            {
                if (trackedModel == null || trackedModel.Root.gameObject.activeSelf == value)
                    return;

                trackedModel.Root.gameObject.SetActive(value);
                foreach (var driver in drivers)
                {
                    if (driver.TryGetComponent<SkinnedMeshRenderer>(out var mesh))
                        mesh.forceRenderingOff = value;
                    else
                    {
                        foreach (var child in driver.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            child.forceRenderingOff = value;
                    }
                }
            }
        }
    }
}

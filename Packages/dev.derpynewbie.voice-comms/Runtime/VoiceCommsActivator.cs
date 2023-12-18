using System;
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace DerpyNewbie.VoiceComms
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VoiceCommsActivator : UdonSharpBehaviour
    {
        [SerializeField]
        private VoiceCommsManager voiceComms;
        [SerializeField]
        private float interactionProximity = 0.1F;
        [SerializeField]
        private bool adjustProximityWithEyeHeight = true;
        [SerializeField]
        private KeyCode vcKey = KeyCode.B;
        [SerializeField]
        private bool toggleVc;
        [SerializeField]
        private ActivatorInteractType interactType;
        [SerializeField]
        private string customInteractionName = "Custom";

        [PublicAPI]
        public ActivatorInteractType InteractType
        {
            get => interactType;
            set => interactType = value;
        }

        [PublicAPI] [Obsolete("Use InteractType instead")]
        public bool UseRightShoulder
        {
            get => interactType == ActivatorInteractType.RightShoulder;
            set => interactType =
                value ? ActivatorInteractType.RightShoulder : ActivatorInteractType.LeftShoulder;
        }

        [PublicAPI]
        public bool UseToggleVc
        {
            get => toggleVc;
            set => toggleVc = value;
        }

        private VRCPlayerApi _local;
        private readonly DataDictionary _interactionData = new DataDictionary();

        private void Start()
        {
            _local = Networking.LocalPlayer;
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            var handTypeKey = args.handType == HandType.LEFT ? "LEFT" : "RIGHT";
            if (_interactionData.ContainsKey(handTypeKey))
            {
                if (value) return;
                var lastInteractionType = _interactionData[handTypeKey].String;
                _interactionData.Remove(handTypeKey);
                _OnVcUseUp(args.handType, lastInteractionType);
                return;
            }

            if (!CheckInteraction(InteractType, args.handType, out var interactionType)) return;

            _interactionData.Add(handTypeKey, interactionType);
            _OnVcUseDown(args.handType, interactionType);
        }

        private void Update()
        {
            if (Input.GetKeyDown(vcKey)) _OnVcUseDown(HandType.LEFT, "Desktop");
            if (Input.GetKeyUp(vcKey)) _OnVcUseUp(HandType.LEFT, "Desktop");
        }

        private void _OnVcUseDown(HandType handType, string interactionType)
        {
            _local.PlayHapticEventInHand(
                handType == HandType.LEFT ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right,
                .1F, 0.2F, 0.2F
            );

            if (toggleVc)
            {
                if (voiceComms.IsTransmitting) voiceComms._EndVCTransmission(interactionType);
                else voiceComms._BeginVCTransmission(interactionType);
            }
            else
            {
                voiceComms._BeginVCTransmission(interactionType);
            }
        }

        private void _OnVcUseUp(HandType handType, string interactionType)
        {
            _local.PlayHapticEventInHand(
                handType == HandType.LEFT ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right,
                .1F, 0.2F, 0.2F
            );

            if (!toggleVc) voiceComms._EndVCTransmission(interactionType);
        }

        private bool CheckInteraction(ActivatorInteractType intType, HandType handType, out string interactionType)
        {
            if (_local.GetPickupInHand(handType == HandType.LEFT
                    ? VRC_Pickup.PickupHand.Left
                    : VRC_Pickup.PickupHand.Right) != null)
            {
                interactionType = default;
                return false;
            }

            if (intType == ActivatorInteractType.BothShoulder)
            {
                // if left shoulder check returns true, interactionType will be set to LeftShoulder.
                // otherwise, it'll be overwritten by right shoulder check.
                return CheckInteraction(ActivatorInteractType.LeftShoulder, handType, out interactionType) ||
                       CheckInteraction(ActivatorInteractType.RightShoulder, handType, out interactionType);
            }

            var handPos = _local.GetTrackingData(handType == HandType.LEFT
                ? VRCPlayerApi.TrackingDataType.LeftHand
                : VRCPlayerApi.TrackingDataType.RightHand).position;
            var eyeHeight = adjustProximityWithEyeHeight ? _local.GetAvatarEyeHeightAsMeters() : 1F;

            Vector3 interactPos;
            switch (intType)
            {
                // Both shoulder case is handled beforehand. unreachable
                default:
                case ActivatorInteractType.Custom:
                {
                    interactPos = transform.position;
                    interactionType = customInteractionName;
                    break;
                }
                case ActivatorInteractType.LeftShoulder:
                {
                    interactPos = _local.GetBonePosition(HumanBodyBones.LeftShoulder);
                    interactionType = "LeftShoulder";
                    break;
                }
                case ActivatorInteractType.RightShoulder:
                {
                    interactPos = _local.GetBonePosition(HumanBodyBones.RightShoulder);
                    interactionType = "RightShoulder";
                    break;
                }
            }

            return Vector3.Distance(interactPos, handPos) <= interactionProximity * eyeHeight;
        }
    }

    public enum ActivatorInteractType
    {
        LeftShoulder,
        RightShoulder,
        BothShoulder,
        Custom
    }
}
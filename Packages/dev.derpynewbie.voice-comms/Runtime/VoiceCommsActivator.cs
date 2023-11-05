using System;
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
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

        [PublicAPI]
        public ActivatorInteractType InteractType
        {
            get => interactType;
            set => interactType = value;
        }

        [PublicAPI] [Obsolete]
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
        private bool _isInteracting;
        private HandType _interactedHandType;

        private void Start()
        {
            _local = Networking.LocalPlayer;
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (_isInteracting)
            {
                if (args.handType != _interactedHandType || value) return;

                _isInteracting = false;
                _OnVcUseUp();
                return;
            }

            if (!CheckInteraction(InteractType, args.handType)) return;

            _isInteracting = true;
            _interactedHandType = args.handType;
            _OnVcUseDown();
        }

        private void Update()
        {
            if (Input.GetKeyDown(vcKey)) _OnVcUseDown();
            if (Input.GetKeyUp(vcKey)) _OnVcUseUp();
        }

        private void _OnVcUseDown()
        {
            _local.PlayHapticEventInHand(
                _interactedHandType == HandType.LEFT ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right,
                .1F, 0.2F, 0.2F
            );

            if (toggleVc)
            {
                if (voiceComms.IsTransmitting) voiceComms._EndVCTransmission();
                else voiceComms._BeginVCTransmission();
            }
            else
            {
                voiceComms._BeginVCTransmission();
            }
        }

        private void _OnVcUseUp()
        {
            _local.PlayHapticEventInHand(
                _interactedHandType == HandType.LEFT ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right,
                .1F, 0.2F, 0.2F
            );

            if (!toggleVc) voiceComms._EndVCTransmission();
        }

        private bool CheckInteraction(ActivatorInteractType intType, HandType handType)
        {
            if (_local.GetPickupInHand(handType == HandType.LEFT
                    ? VRC_Pickup.PickupHand.Left
                    : VRC_Pickup.PickupHand.Right) != null) return false;

            if (intType == ActivatorInteractType.BothShoulder)
                return CheckInteraction(ActivatorInteractType.LeftShoulder, handType) ||
                       CheckInteraction(ActivatorInteractType.RightShoulder, handType);

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
                    break;
                }
                case ActivatorInteractType.LeftShoulder:
                {
                    interactPos = _local.GetBonePosition(HumanBodyBones.LeftShoulder);
                    break;
                }
                case ActivatorInteractType.RightShoulder:
                {
                    interactPos = _local.GetBonePosition(HumanBodyBones.RightShoulder);
                    break;
                }
            }

            return Vector3.Distance(interactPos, handPos) >= interactionProximity * eyeHeight;
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
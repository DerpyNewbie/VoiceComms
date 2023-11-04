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
        private KeyCode vcKey = KeyCode.B;
        [SerializeField]
        private bool toggleVc;
        [SerializeField]
        private bool useRightShoulder;

        [PublicAPI]
        public bool UseRightShoulder
        {
            get => useRightShoulder;
            set => useRightShoulder = value;
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

            if (_local.GetPickupInHand(args.handType == HandType.LEFT
                    ? VRC_Pickup.PickupHand.Left
                    : VRC_Pickup.PickupHand.Right) != null) return;

            var eyeHeight = _local.GetAvatarEyeHeightAsMeters();
            var interactionPos =
                _local.GetBonePosition(UseRightShoulder ? HumanBodyBones.RightShoulder : HumanBodyBones.LeftShoulder);
            var handPos = _local.GetTrackingData(args.handType == HandType.LEFT
                ? VRCPlayerApi.TrackingDataType.LeftHand
                : VRCPlayerApi.TrackingDataType.RightHand).position;

            if (Vector3.Distance(interactionPos, handPos) >= interactionProximity * eyeHeight) return;

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
    }
}
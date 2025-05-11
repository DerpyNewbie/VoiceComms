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

        private void Update()
        {
            if (Input.GetKeyDown(vcKey)) OnVcUseDown(HandType.LEFT, "Desktop");
            if (Input.GetKeyUp(vcKey)) OnVcUseUp(HandType.LEFT, "Desktop");
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            var handTypeKey = args.handType == HandType.LEFT ? "LEFT" : "RIGHT";
            if (_interactionData.ContainsKey(handTypeKey))
            {
                if (value) return;
                var lastInteractionType = _interactionData[handTypeKey].String;
                _interactionData.Remove(handTypeKey);
                OnVcUseUp(args.handType, lastInteractionType);
                return;
            }

            if (IsPickupInHand(args.handType)) return;
            if (!CanInteract(InteractType, args.handType, out var interactionName)) return;

            _interactionData.Add(handTypeKey, interactionName);
            OnVcUseDown(args.handType, interactionName);
        }

        private void OnVcUseDown(HandType handType, string interactionName)
        {
            _local.PlayHapticEventInHand(
                handType == HandType.LEFT ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right,
                .1F, 0.2F, 0.2F
            );

            if (toggleVc)
            {
                if (voiceComms.IsTransmitting) voiceComms._EndVCTransmission(interactionName);
                else voiceComms._BeginVCTransmission(interactionName);
            }
            else
            {
                voiceComms._BeginVCTransmission(interactionName);
            }
        }

        private void OnVcUseUp(HandType handType, string interactionType)
        {
            _local.PlayHapticEventInHand(
                handType == HandType.LEFT ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right,
                .1F, 0.2F, 0.2F
            );

            if (!toggleVc) voiceComms._EndVCTransmission(interactionType);
        }

        private bool CanInteract(ActivatorInteractType activatorInteractType, HandType handType,
            out string interactionName)
        {
            // Handle BothShoulder case
            if (activatorInteractType == ActivatorInteractType.BothShoulder)
            {
                return CanInteract(ActivatorInteractType.LeftShoulder, handType, out interactionName) ||
                       CanInteract(ActivatorInteractType.RightShoulder, handType, out interactionName);
            }

            // Get hand position
            var handPos = _local.GetTrackingData(handType == HandType.LEFT
                ? VRCPlayerApi.TrackingDataType.LeftHand
                : VRCPlayerApi.TrackingDataType.RightHand).position;

            // Get interaction pos and name
            var interactPos = GetInteractionPosition(activatorInteractType);
            interactionName = GetInteractionType(activatorInteractType);

            // Calculate proximity
            var proximity = interactionProximity;
            if (adjustProximityWithEyeHeight)
            {
                proximity *= _local.GetAvatarEyeHeightAsMeters();
            }

            return Vector3.Distance(interactPos, handPos) <= proximity;
        }

        private Vector3 GetInteractionPosition(ActivatorInteractType interactionType)
        {
            // Not supported in UdonSharp
            // ReSharper disable once ConvertSwitchStatementToSwitchExpression
            switch (interactionType)
            {
                case ActivatorInteractType.RightShoulder:
                    return _local.GetBonePosition(HumanBodyBones.RightShoulder);
                case ActivatorInteractType.LeftShoulder:
                    return _local.GetBonePosition(HumanBodyBones.LeftShoulder);
                case ActivatorInteractType.Custom:
                case ActivatorInteractType.BothShoulder:
                default:
                    return transform.position;
            }
        }

        private string GetInteractionType(ActivatorInteractType activatorInteractType)
        {
            // Not supported in UdonSharp
            // ReSharper disable once ConvertSwitchStatementToSwitchExpression
            switch (activatorInteractType)
            {
                case ActivatorInteractType.RightShoulder:
                    return "RightShoulder";
                case ActivatorInteractType.LeftShoulder:
                    return "LeftShoulder";
                case ActivatorInteractType.Custom:
                case ActivatorInteractType.BothShoulder:
                default:
                    return customInteractionName;
            }
        }

        private bool IsPickupInHand(HandType handType)
        {
            // Not supported in UdonSharp
            // ReSharper disable once ConvertSwitchStatementToSwitchExpression
            switch (handType)
            {
                case HandType.LEFT:
                    return _local.GetPickupInHand(VRC_Pickup.PickupHand.Left);
                case HandType.RIGHT:
                    return _local.GetPickupInHand(VRC_Pickup.PickupHand.Right);
                default:
                    return false;
            }
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
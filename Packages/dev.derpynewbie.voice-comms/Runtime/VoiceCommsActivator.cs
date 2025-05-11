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
        [Tooltip("Maximum duration for continuous transmission. Set this 0 or below for unlimited duration.")]
        private float maxDuration;

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
        private float _startTime = float.MinValue;

        private void Start()
        {
            _local = Networking.LocalPlayer;
        }

        private void Update()
        {
            if (Input.GetKeyDown(vcKey)) OnVcUseDown(HandType.LEFT, "Desktop");
            if (Input.GetKeyUp(vcKey)) OnVcUseUp(HandType.LEFT);

            if (HasDurationLimit() && IsInteracting() && _startTime + maxDuration < Time.timeSinceLevelLoad)
            {
                var keys = _interactionData.GetKeys().ToArray();
                foreach (var key in keys)
                {
                    OnVcUseUp(TokenToHandType(key), true);
                }
            }
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            var handTypeKey = HandTypeToToken(args.handType);
            if (_interactionData.ContainsKey(handTypeKey))
            {
                if (value) return;
                OnVcUseUp(args.handType);
                return;
            }

            if (IsPickupInHand(args.handType)) return;
            if (!CanInteract(InteractType, args.handType, out var interactionName)) return;

            OnVcUseDown(args.handType, interactionName);
        }

        private void OnVcUseDown(HandType handType, string interactionName)
        {
            if (!voiceComms)
            {
                Debug.LogError($"[VoiceCommsActivator-{name}] VoiceCommsManager is not assigned");
                return;
            }

            _local.PlayHapticEventInHand(
                handType == HandType.LEFT ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right,
                .1F, 0.2F, 0.2F
            );

            // If already in use, end it.
            var handTypeKey = HandTypeToToken(handType);
            if (_interactionData.ContainsKey(handTypeKey))
            {
                OnVcUseUp(handType, true);
                if (UseToggleVc) return;
            }

            _startTime = Time.timeSinceLevelLoad;
            _interactionData.Add(handTypeKey, interactionName);
            voiceComms._BeginVCTransmission(interactionName);
        }

        private void OnVcUseUp(HandType handType, bool forceEnd = false)
        {
            if (!voiceComms)
            {
                Debug.LogError($"[VoiceCommsActivator-{name}] VoiceCommsManager is not assigned");
                return;
            }

            var handTypeKey = HandTypeToToken(handType);
            if (!_interactionData.ContainsKey(handTypeKey))
            {
                Debug.LogWarning($"[VoiceCommsActivator-{name}] HandType {handType} is not in use");
                return;
            }

            _local.PlayHapticEventInHand(
                handType == HandType.LEFT ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right,
                .1F, 0.2F, 0.2F
            );

            if (!toggleVc || forceEnd)
            {
                voiceComms._EndVCTransmission(_interactionData[handTypeKey].String);
                _interactionData.Remove(handTypeKey);
            }
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
                    return transform.position;
                default:
                case ActivatorInteractType.BothShoulder:
                    return Vector3.zero; // Should throw but not supported in UdonSharp
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
                    return customInteractionName;
                case ActivatorInteractType.BothShoulder:
                default:
                    return "Unknown"; // Should throw but not supported in UdonSharp
            }
        }

        private DataToken HandTypeToToken(HandType handType)
        {
            switch (handType)
            {
                case HandType.LEFT:
                    return new DataToken("LEFT");
                case HandType.RIGHT:
                    return new DataToken("RIGHT");
                default:
                    Debug.LogError($"[VoiceCommsActivator-{name}] Invalid hand type: {handType}");
                    return new DataToken("UNKNOWN"); // Should throw but not supported in UdonSharp
            }
        }

        private HandType TokenToHandType(DataToken token)
        {
            if (token.TokenType != TokenType.String)
            {
                Debug.LogError($"[VoiceCommsActivator-{name}] Invalid token type for HandType: {token.TokenType}");
                return HandType.LEFT; // Should throw but not supported in UdonSharp
            }

            switch (token.String)
            {
                case "LEFT":
                    return HandType.LEFT;
                case "RIGHT":
                    return HandType.RIGHT;
                default:
                    Debug.LogError($"[VoiceCommsActivator-{name}] Invalid token value for HandType: {token.String}");
                    return HandType.LEFT; // Should throw but not supported in UdonSharp
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
                    return false; // Should throw but not supported in UdonSharp
            }
        }

        private bool IsInteracting()
        {
            return _interactionData.Count != 0;
        }

        private bool HasDurationLimit()
        {
            return maxDuration > 0;
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
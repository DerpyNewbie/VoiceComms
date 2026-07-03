using DerpyNewbie.Common;
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
namespace DerpyNewbie.VoiceComms
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class VoiceCommsUser : UdonSharpBehaviour
    {
        #region Fields
        [SerializeField] [NewbieInject]
        private VoiceCommsManager voiceCommsManager;

        private bool _hasCustomSettings;
        private bool _isSuppressed;
        private float _gain = 15;
        private float _near = 0.1F;
        private float _far = 25;
        private float _volumetricRadius = 10;
        private bool _lowpass = true;
        #endregion

        #region PublicAPIProperties
        [PublicAPI]
        [field: UdonSynced]
        public bool IsTransmitting { get; private set; }

        [PublicAPI]
        [field: UdonSynced]
        [field: FieldChangeCallback(nameof(TransmittingChannels))]
        public int[] TransmittingChannels { get; private set; } = new int[0];

        [PublicAPI]
        public bool HasCustomSettings
        {
            get => _hasCustomSettings;
            set
            {
                if (value == _hasCustomSettings) return;

                _hasCustomSettings = value;
                _isSuppressed = false;
                voiceCommsManager._GetDefaultVcSettings(out _gain, out _near, out _far, out _volumetricRadius, out _lowpass);
            }
        }

        [PublicAPI]
        public bool IsSuppressed
        {
            get => HasCustomSettings && _isSuppressed;
            set
            {
                HasCustomSettings = true;
                _isSuppressed = value;
            }
        }

        [PublicAPI]
        public bool Lowpass
        {
            get => HasCustomSettings ? _lowpass : voiceCommsManager.Lowpass;
            set
            {
                HasCustomSettings = true;
                _lowpass = value;
            }
        }

        [PublicAPI]
        public float Gain
        {
            get => HasCustomSettings ? _gain : voiceCommsManager.Gain;
            set
            {
                HasCustomSettings = true;
                _gain = value;
            }
        }

        [PublicAPI]
        public float Near
        {
            get => HasCustomSettings ? _near : voiceCommsManager.Near;
            set
            {
                HasCustomSettings = true;
                _near = value;
            }
        }

        [PublicAPI]
        public float Far
        {
            get => HasCustomSettings ? _far : voiceCommsManager.Far;
            set
            {
                HasCustomSettings = true;
                _far = value;
            }
        }

        [PublicAPI]
        public float VolumetricRadius
        {
            get => HasCustomSettings ? _volumetricRadius : voiceCommsManager.VolumetricRadius;
            set
            {
                HasCustomSettings = true;
                _volumetricRadius = value;
            }
        }

        [PublicAPI]
        public VRCPlayerApi VrcPlayer => Networking.GetOwner(gameObject);
        #endregion

        public override void OnPreSerialization()
        {
            if (IsTransmitting)
            {
                var txChannelIds = voiceCommsManager.TxChannelId.ToArray();
                TransmittingChannels = new int[txChannelIds.Length];
                for (var i = 0; i < txChannelIds.Length; i++)
                {
                    var channelId = txChannelIds[i];
                    if (channelId.TokenType != TokenType.Int)
                    {
                        TransmittingChannels[i] = -1;
                        continue;
                    }

                    TransmittingChannels[i] = channelId.Int;
                }
            }
            else
            {
                TransmittingChannels = new int[0];
            }
        }

        public override void OnDeserialization()
        {
            voiceCommsManager._UpdateVoice(this);
        }

        public void SyncWithManager()
        {
            if (!VrcPlayer.isLocal) return;

            IsTransmitting = voiceCommsManager.IsTransmitting;
            RequestSerialization();
        }

        public void ForceStopTransmitting()
        {
            IsTransmitting = false;
        }
    }

    public static class VoiceCommsUserExtension
    {
        private static bool TryGetVoiceCommsUser(GameObject obj, out VoiceCommsUser vcUser)
        {
            vcUser = obj.GetComponent<VoiceCommsUser>();
            if (vcUser)
                return true;

            var transform = obj.transform;
            var childCount = transform.childCount;
            for (var i = 0; i < childCount; i++)
                if (TryGetVoiceCommsUser(transform.GetChild(i).gameObject, out vcUser))
                    return true;

            vcUser = null;
            return false;
        }

        public static bool TryGetVoiceCommsUser(this VRCPlayerApi api, out VoiceCommsUser vcUser)
        {
            var playerObjects = api.GetPlayerObjects();
            foreach (var playerObject in playerObjects)
                if (TryGetVoiceCommsUser(playerObject, out vcUser))
                    return true;

            vcUser = null;
            return false;
        }

        public static VoiceCommsUser GetVoiceCommsUser(this VRCPlayerApi api)
        {
            return api.TryGetVoiceCommsUser(out var vcUser) ? vcUser : null;
        }
    }
}

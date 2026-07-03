using JetBrains.Annotations;
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
namespace DerpyNewbie.VoiceComms
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class VoiceCommsManager : UdonSharpBehaviour
    {
        #region SerializeField
        [SerializeField]
        private float vcGain;

        [SerializeField]
        private float vcNear = 999999;

        [SerializeField]
        private float vcFar = 999999;

        [SerializeField]
        private float vcVolumetricRadius;

        [SerializeField]
        private bool vcLowpass;

        [SerializeField]
        private float defaultGain = 15;

        [SerializeField]
        private float defaultNear;

        [SerializeField]
        private float defaultFar = 25;

        [SerializeField]
        private float defaultVolumetricRadius;

        [SerializeField]
        private bool defaultLowpass = true;
        #endregion

        #region PublicAPIs
        private readonly DataList _txChannelId = new DataList { new DataToken(0) };
        private readonly DataList _rxChannelId = new DataList { new DataToken(0) };
        private readonly DataList _activeInteractionType = new DataList();
        private VoiceCommsUser _localUser;
        private bool _isTransmitting;

        /// <summary>
        /// TX channel ID for local player. transmits voice over these channels.
        /// </summary>
        /// <remarks>
        /// The returned list is deeply cloned. Use methods such as <see cref="_AddTxChannel"/> or <see cref="_RemoveTxChannel"/> to interact with it instead.
        /// It's initialized with { 0 }.
        /// </remarks>
        /// <seealso cref="VoiceCommsManager._AddTxChannel"/>
        /// <seealso cref="VoiceCommsManager._RemoveTxChannel"/>
        [PublicAPI]
        public DataList TxChannelId => _txChannelId.DeepClone();

        /// <summary>
        /// RX Channel ID for local player. another player's VC transmission can be heard if one of the channels id matches.
        /// </summary>
        /// <remarks>
        /// The returned list is deeply cloned. Use methods such as <see cref="_AddRxChannel"/> or <see cref="_RemoveRxChannel"/> to interact with it instead.
        /// It's initialized with { 0 }.
        /// </remarks>
        /// <seealso cref="VoiceCommsManager._AddRxChannel"/>
        /// <seealso cref="VoiceCommsManager._RemoveRxChannel"/>
        [PublicAPI]
        public DataList RxChannelId => _rxChannelId.DeepClone();

        /// <summary>
        /// Players who have their VC activated currently will be in this list.
        /// </summary>
        [PublicAPI]
        public DataList ActivePlayerId => _activePlayerId.DeepClone();

        /// <summary>
        /// Returns all current active interaction types supplied in <see cref="_BeginVCTransmission"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="_BeginVCTransmission"/> is basically an Add operation.
        /// and <see cref="_EndVCTransmission"/> is responsive for Remove op.
        /// The returned list is deeply cloned.
        /// </remarks>
        [PublicAPI]
        public DataList ActiveInteractionType => _activeInteractionType.DeepClone();

        /// <summary>
        /// Is local transmitting voice over a channel?
        /// </summary>
        [PublicAPI]
        public bool IsTransmitting
        {
            get => _isTransmitting;
            private set
            {
                _isTransmitting = value;
                var localUser = LocalUser;
                if (localUser) localUser.SyncWithManager();
            }
        }

        /// <summary>
        /// Is VoiceCommsManager enabled and working?
        /// </summary>
        [PublicAPI]
        public bool IsVoiceCommsEnabled { get; set; } = true;

        [PublicAPI]
        public bool Lowpass => vcLowpass;

        [PublicAPI]
        public float Gain => vcGain;

        [PublicAPI]
        public float Near => vcNear;

        [PublicAPI]
        public float Far => vcFar;

        [PublicAPI]
        public float VolumetricRadius => vcVolumetricRadius;

        [PublicAPI]
        public VoiceCommsUser LocalUser
        {
            get
            {
                if (!_localUser)
                    _localUser = Networking.LocalPlayer.GetVoiceCommsUser();
                return _localUser;
            }
        }

        /// <summary>
        /// Begins VC transmission in <see cref="TxChannelId"/> from local player.
        /// </summary>
        [PublicAPI]
        public void _BeginVCTransmission(string interactionType = "Default")
        {
            Debug.Log($"[VCManager] _BeginVCTransmission: {interactionType}");

            _activeInteractionType.Add(interactionType);
            IsTransmitting = true;
            _Invoke_OnBeginTransmission(interactionType);
        }

        /// <summary>
        /// Ends VC transmission from local player.
        /// </summary>
        [PublicAPI]
        public void _EndVCTransmission(string interactionType = "Default")
        {
            Debug.Log($"[VCManager] _EndVCTransmission: {interactionType}");

            _activeInteractionType.Remove(interactionType);
            IsTransmitting = _activeInteractionType.Count != 0;
            _Invoke_OnEndTransmission(interactionType);
        }

        [PublicAPI]
        public void _AddTxChannel(int channel)
        {
            _txChannelId.Add(channel);
        }

        [PublicAPI]
        public bool _RemoveTxChannel(int channel)
        {
            return _txChannelId.Remove(channel);
        }

        [PublicAPI]
        public void _ClearTxChannel()
        {
            _txChannelId.Clear();
        }

        [PublicAPI]
        public void _AddRxChannel(int channel)
        {
            _rxChannelId.Add(channel);
        }

        [PublicAPI]
        public bool _RemoveRxChannel(int channel)
        {
            return _rxChannelId.Remove(channel);
        }

        [PublicAPI]
        public void _ClearRxChannel()
        {
            _rxChannelId.Clear();
        }

        /// <summary>
        /// Gets TX Channels which is filtered by <see cref="RxChannelId"/>
        /// </summary>
        /// <param name="playerId">playerId of VRCPlayerApi to get filtered TX channels</param>
        /// <returns>List of TX channels <paramref name="playerId"/> is using to transmit. Can be empty</returns>
        /// <remarks>
        /// Empty list means <paramref name="playerId"/> is not transmitting, or <paramref name="playerId"/>
        /// is transmitting but <see cref="RxChannelId"/> didn't match
        /// </remarks>
        [PublicAPI]
        public DataList _GetActiveTxChannels(int playerId)
        {
            var txChannels = _GetTxChannels(playerId);
            var result = new DataList();
            var rxChArr = _rxChannelId.ToArray();
            var txChArr = txChannels.ToArray();
            foreach (var rxChannel in rxChArr)
            foreach (var txChannel in txChArr)
                if (_IsSameChannelId(rxChannel, txChannel))
                    result.Add(txChannel);
            return result;
        }

        /// <summary>
        /// Gets TX Channels used by <paramref name="playerId"/>
        /// </summary>
        /// <param name="playerId">playerId of VRCPlayerApi to get TX channels</param>
        /// <returns>List of TX channels player with <paramref name="playerId"/> is using to transmit. Can be empty</returns>
        /// <remarks>
        /// Empty list means the player is not transmitting
        /// </remarks>
        [PublicAPI] [NotNull]
        public DataList _GetTxChannels(int playerId)
        {
            var vrcPlayer = VRCPlayerApi.GetPlayerById(playerId);
            if (vrcPlayer == null || !Utilities.IsValid(vrcPlayer)) return new DataList();

            var vcUser = vrcPlayer.GetVoiceCommsUser();
            if (vcUser == null) return new DataList();

            var result = new DataList();
            foreach (var channel in vcUser.TransmittingChannels)
                result.Add(channel);

            return result;
        }

        /// <summary>
        /// Gets <paramref name="playerId"/>'s transmitting state
        /// </summary>
        /// <param name="playerId">playerId of VRCPlayerApi to get transmitting state</param>
        /// <returns>
        /// <c>true</c> if <paramref name="playerId"/> is transmitting (not considering <see cref="RxChannelId"/>).
        /// <c>false</c> otherwise
        /// </returns>
        /// <remarks>
        /// This returns <c>true</c> even if <paramref name="playerId"/> does not have matching TX/RX channels.
        /// </remarks>
        /// <seealso cref="_IsActivelyTransmitting"/>
        [PublicAPI]
        public bool _IsTransmitting(int playerId)
        {
            var vrcPlayer = VRCPlayerApi.GetPlayerById(playerId);
            if (vrcPlayer == null || !Utilities.IsValid(vrcPlayer)) return false;

            var vcUser = vrcPlayer.GetVoiceCommsUser();
            return vcUser && vcUser.IsTransmitting;
        }

        /// <summary>
        /// Gets <paramref name="playerId"/>'s transmitting state
        /// </summary>
        /// <param name="playerId">playerId of VRCPlayerApi to get transmitting state</param>
        /// <returns>
        /// <c>true</c> if <paramref name="playerId"/> is actively transmitting.
        /// <c>false</c> otherwise
        /// </returns>
        /// <seealso cref="_IsTransmitting"/>
        [PublicAPI]
        public bool _IsActivelyTransmitting(int playerId)
        {
            return _activePlayerId.Contains($"{playerId}");
        }

        /// <summary>
        /// Forcefully clears all active TXs 
        /// </summary>
        /// <remarks>
        /// Syncs data if local is master
        /// </remarks>
        [PublicAPI]
        public void ClearAllTransmissions()
        {
            _activeInteractionType.Clear();
            _isTransmitting = false;

            var players = VRCPlayerApi.GetPlayers(new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]);
            foreach (var player in players)
            {
                if (!player.TryGetVoiceCommsUser(out var vcUser)) continue;
                vcUser.ForceStopTransmitting();
            }
        }

        /// <summary>
        /// Forcefully clears all active TXs for all clients
        /// </summary>
        /// <remarks>
        /// Sends network event <see cref="ClearAllTransmissions"/> for all targets 
        /// </remarks>
        [PublicAPI]
        public void _ClearAllTransmissionsGlobally()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ClearAllTransmissions));
        }

        [PublicAPI]
        public void _SetUserVcSettings(string displayName,
                                       bool suppressed, float gain, float near, float far, float volumetricRadius, bool lowpass)
        {
            if (!TryGetPlayerByDisplayName(displayName, out var playerApi))
                return;

            if (!playerApi.TryGetVoiceCommsUser(out var vcUser))
                return;

            vcUser.IsSuppressed = suppressed;
            vcUser.Gain = gain;
            vcUser.Near = near;
            vcUser.Far = far;
            vcUser.VolumetricRadius = volumetricRadius;
            vcUser.Lowpass = lowpass;

            _RefreshVC();
            _Invoke_OnVoiceSettingsUpdated(displayName);
        }

        [PublicAPI]
        public void _GetUserVcSettings(string displayName,
                                       out bool suppressed, out float gain, out float near, out float far, out float volumetricRadius,
                                       out bool lowpass)
        {
            if (!TryGetPlayerByDisplayName(displayName, out var playerApi) || !playerApi.TryGetVoiceCommsUser(out var vcUser))
            {
                suppressed = false;
                _GetDefaultVcSettings(out gain, out near, out far, out volumetricRadius, out lowpass);
                return;
            }

            suppressed = vcUser.IsSuppressed;
            gain = vcUser.Gain;
            near = vcUser.Near;
            far = vcUser.Far;
            volumetricRadius = vcUser.VolumetricRadius;
            lowpass = vcUser.Lowpass;
        }

        [PublicAPI]
        public void _ClearUserVcSettings(string displayName)
        {
            if (!TryGetPlayerByDisplayName(displayName, out var playerApi) || !playerApi.TryGetVoiceCommsUser(out var vcUser)) return;
            vcUser.HasCustomSettings = false;

            _RefreshVC();
            _Invoke_OnVoiceSettingsUpdated(displayName);
        }

        [PublicAPI]
        public void _ClearUserVcSettings()
        {
            var players = VRCPlayerApi.GetPlayers(new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]);
            foreach (var player in players)
            {
                if (!player.TryGetVoiceCommsUser(out var vcUser)) continue;
                vcUser.HasCustomSettings = false;
            }
        }

        [PublicAPI]
        public void _SetDefaultVcSettings(float gain, float near, float far, float volumetricRadius, bool lowpass)
        {
            var prevGain = vcGain;
            var prevNear = vcNear;
            var prevFar = vcFar;
            var prevVolumetricRadius = vcVolumetricRadius;
            var prevLowpass = vcLowpass;

            vcGain = gain;
            vcNear = near;
            vcFar = far;
            vcVolumetricRadius = volumetricRadius;
            vcLowpass = lowpass;

            _Invoke_OnVoiceDefaultSettingsUpdated(prevGain, prevNear, prevFar, prevVolumetricRadius, prevLowpass);
            _RefreshVC();
        }

        [PublicAPI]
        public void _GetDefaultVcSettings(out float gain, out float near, out float far,
                                          out float volumetricRadius, out bool lowpass)
        {
            gain = vcGain;
            near = vcNear;
            far = vcFar;
            volumetricRadius = vcVolumetricRadius;
            lowpass = vcLowpass;
        }

        [PublicAPI]
        public bool _IsUserSuppressed(VRCPlayerApi playerApi)
        {
            return playerApi.TryGetVoiceCommsUser(out var vcUser) && vcUser.IsSuppressed;
        }
        #endregion

        #region Internals
        private readonly DataList _activePlayerId = new DataList();

        public void _UpdateVoice(VoiceCommsUser user)
        {
            if (!user)
            {
                return;
            }

            if (!user.IsTransmitting)
            {
                _SetDefaultVoice(user.VrcPlayer);
                return;
            }

            if (_ContainsSameChannelId(user.TransmittingChannels, RxChannelId))
            {
                _SetVCVoice(user.VrcPlayer);
                return;
            }
        }

        private void _RefreshVC()
        {
            var players = VRCPlayerApi.GetPlayers(new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]);
            foreach (var player in players)
                _UpdateVoice(player.GetVoiceCommsUser());
        }

        private void _SetVCVoice(VRCPlayerApi api)
        {
            if (api == null || !api.IsValid())
            {
                return;
            }

            if (api.GetPlayerTag("NCVoiceOverride") == "true")
            {
                Debug.Log($"[VCManager] _SetVCVoice: {api.displayName} has NCVoiceOverride tag enabled, ignoring.");
                return;
            }

            _GetUserVcSettings(
                api.displayName,
                out var muted,
                out var gain,
                out var near,
                out var far,
                out var volumetricRadius,
                out var lowpass
            );

            if (muted)
            {
                _SetDefaultVoice(api);
                return;
            }

            api.SetPlayerTag("VoiceCommsEnabled", "true");

            if (!_activePlayerId.Contains($"{api.playerId}"))
            {
                _activePlayerId.Add($"{api.playerId}");
            }

            _SetVoice(api, gain, near, far, volumetricRadius, lowpass);
            _Invoke_OnVoiceUpdated(api, true);
        }

        private void _SetDefaultVoice(VRCPlayerApi api)
        {
            if (api == null || !api.IsValid())
            {
                return;
            }

            if (api.GetPlayerTag("NCVoiceOverride") == "true")
            {
                Debug.Log($"[VCManager] _SetDefaultVoice: {api.displayName} has NCVoiceOverride tag enabled, ignoring.");
                return;
            }

            if (api.GetPlayerTag("VoiceCommsEnabled") != "true")
            {
                return;
            }

            api.SetPlayerTag("VoiceCommsEnabled", "false");

            if (_activePlayerId.Contains($"{api.playerId}"))
            {
                _activePlayerId.RemoveAll($"{api.playerId}");
            }

            _SetVoice(api, defaultGain, defaultNear, defaultFar, defaultVolumetricRadius, defaultLowpass);
            _Invoke_OnVoiceUpdated(api, false);
        }

        private static void _SetVoice(
            VRCPlayerApi api,
            float gain,
            float near,
            float far,
            float volumetricRadius,
            bool lowpass
        )
        {
            if (!Utilities.IsValid(api))
                return;

            api.SetVoiceGain(gain);
            api.SetVoiceDistanceNear(near);
            api.SetVoiceDistanceFar(far);
            api.SetVoiceVolumetricRadius(volumetricRadius);
            api.SetVoiceLowpass(lowpass);
        }

        private static bool _ContainsSameChannelId(int[] ch1, DataList ch2)
        {
            var ch2Arr = ch2.ToArray();
            foreach (var ch1Token in ch1)
            foreach (var ch2Token in ch2Arr)
                if (_IsSameChannelId(ch1Token, ch2Token))
                    return true;

            return false;
        }

        private static bool _IsSameChannelId(DataToken t1, DataToken t2)
        {
            var isNumbers = t1.IsNumber && t2.IsNumber;
            if (isNumbers && Math.Abs(t1.Number - t2.Number) < double.Epsilon) return true;
            return t1.TokenType != t2.TokenType ? t1.ToString().Equals(t2.ToString()) : t1.Equals(t2);
        }
        #endregion

        #region EventCallbacks
        private readonly DataList _callbacks = new DataList();

        [PublicAPI]
        public void _SubscribeCallback(VoiceCommsManagerCallback callback)
        {
            _callbacks.Add(callback);
        }

        [PublicAPI]
        public void _UnsubscribeCallback(VoiceCommsManagerCallback callback)
        {
            _callbacks.Remove(callback);
        }

        private void _Invoke_OnVoiceUpdated(VRCPlayerApi player, bool activated)
        {
            Debug.Log(
                $"[VCManager] Invoke_OnVoiceUpdated: {(player != null && Utilities.IsValid(player) ? player.playerId : -1)}, {activated}");

            var arr = _callbacks.ToArray();
            foreach (var callback in arr)
            {
                var obj = (VoiceCommsManagerCallback)callback.Reference;
                if (obj) obj.OnVoiceUpdated(player, activated);
            }
        }

        private void _Invoke_OnBeginTransmission(string interactionType)
        {
            Debug.Log($"[VCManager] Invoke_OnBeginTransmission: {interactionType}");

            var arr = _callbacks.ToArray();
            foreach (var callback in arr)
            {
                var obj = (VoiceCommsManagerCallback)callback.Reference;
                if (obj) obj.OnBeginTransmission(interactionType);
            }
        }

        private void _Invoke_OnEndTransmission(string interactionType)
        {
            Debug.Log($"[VCManager] Invoke_OnEndTransmission: {interactionType}");

            var arr = _callbacks.ToArray();
            foreach (var callback in arr)
            {
                var obj = (VoiceCommsManagerCallback)callback.Reference;
                if (obj) obj.OnEndTransmission(interactionType);
            }
        }

        private void _Invoke_OnVoiceSettingsUpdated(string displayName)
        {
            Debug.Log($"[VCManager] Invoke_OnVoiceSettingsUpdated: {displayName}");

            var arr = _callbacks.ToArray();
            foreach (var callback in arr)
            {
                var obj = (VoiceCommsManagerCallback)callback.Reference;
                if (obj) obj.OnVoiceSettingsUpdated(displayName);
            }
        }

        private void _Invoke_OnVoiceDefaultSettingsUpdated(
            float prevGain, float prevNear, float prevFar, float prevVolumetricRadius, bool prevLowpass)
        {
            Debug.Log(
                $"[VCManager] Invoke_OnVoiceDefaultSettingsUpdated: {prevGain}, {prevNear}, {prevFar}, {prevVolumetricRadius}, {prevLowpass}");

            var arr = _callbacks.ToArray();
            foreach (var callback in arr)
            {
                var obj = (VoiceCommsManagerCallback)callback.Reference;
                if (obj)
                    obj.OnVoiceDefaultSettingsUpdated(prevGain, prevNear, prevFar, prevVolumetricRadius, prevLowpass);
            }
        }
        #endregion

        private static bool TryGetPlayerByDisplayName(string displayName, [CanBeNull] out VRCPlayerApi playerApi)
        {
            var players = VRCPlayerApi.GetPlayers(new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]);
            foreach (var player in players)
            {
                if (player.displayName != displayName)
                    continue;

                playerApi = player;
                return true;
            }

            playerApi = null;
            return false;
        }
    }

    public abstract class VoiceCommsManagerCallback : UdonSharpBehaviour
    {
        public virtual void OnVoiceUpdated(VRCPlayerApi player, bool activated)
        {
        }

        public virtual void OnBeginTransmission(string interactionType)
        {
        }

        public virtual void OnEndTransmission(string interactionType)
        {
        }

        public virtual void OnVoiceSettingsUpdated(string displayName)
        {
        }

        public virtual void OnVoiceDefaultSettingsUpdated(
            float prevGain, float prevNear, float prevFar, float prevVolumetricRadius, bool prevLowpass)
        {
        }
    }
}

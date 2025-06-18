using System;
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace DerpyNewbie.VoiceComms
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class VoiceCommsManager : UdonSharpBehaviour
    {
        #region UdonSynced

        [UdonSynced]
        private string _vcUserDataJson = "{}";

        private string _lastVcUserDataJson = "{}";

        #endregion

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
        public bool IsTransmitting { get; private set; }

        /// <summary>
        /// Begins VC transmission in <see cref="TxChannelId"/> from local player.
        /// </summary>
        [PublicAPI]
        public void _BeginVCTransmission(string interactionType = "Default")
        {
            Debug.Log($"[VCManager] _BeginVCTransmission: {interactionType}");

            _activeInteractionType.Add(interactionType);
            IsTransmitting = true;
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();

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
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();

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
            var data = _GetVCUserData(_vcUserDataJson);
            // Cannot be _ in UdonSharp
            // ReSharper disable once UnusedVariable
            _GetVCUserRecord(data, playerId, out var isTransmitting, out var txChannels);

            // Not available in UdonSharp
            // ReSharper disable once MergeConditionalExpression
            return txChannels != null ? txChannels : new DataList();
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
            var data = _GetVCUserData(_vcUserDataJson);
            // Cannot be _ in UdonSharp
            // ReSharper disable once UnusedVariable
            _GetVCUserRecord(data, playerId, out var isTransmitting, out var txChannels);
            return isTransmitting;
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
            _vcUserDataJson = "{}";
            _activeInteractionType.Clear();
            IsTransmitting = false;
            _DiffApplyVCVoice();

            if (!Networking.IsMaster) return;

            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
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

        #endregion

        #region UdonEvents

        public override void OnPlayerLeft(VRCPlayerApi playerApi)
        {
            var dict = _GetVCUserData(_vcUserDataJson);
            _UpdateVCUserRecord(dict, playerApi.playerId, false, new DataList());
            _UpdateJson(dict);
            _DiffApplyVCVoice();
        }

        public override void OnPreSerialization()
        {
            var dict = _GetVCUserData(_vcUserDataJson);
            _UpdateVCUserRecord(dict, Networking.LocalPlayer.playerId, IsTransmitting, _txChannelId);
            _UpdateJson(dict);
        }

        public override void OnPostSerialization(SerializationResult result)
        {
            if (result.success)
                _DiffApplyVCVoice();
            else
                RequestSerialization();
        }

        public override void OnDeserialization()
        {
            var dict = _GetVCUserData(_vcUserDataJson);
            // Cannot be _ in UdonSharp
            // ReSharper disable once UnusedVariable
            _GetVCUserRecord(dict, Networking.LocalPlayer.playerId, out var isTransmitting, out var txChannels);
            if (IsTransmitting != isTransmitting)
            {
                Debug.LogWarning("[VCManager] Detected VC data de-sync! re-syncing!");
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                RequestSerialization();
            }

            _DiffApplyVCVoice();
        }

        #endregion

        #region Internals

        private readonly DataList _activePlayerId = new DataList();

        private void _DiffApplyVCVoice()
        {
            if (!VRCJson.TryDeserializeFromJson(_vcUserDataJson, out var updatedToken))
                Debug.LogError(
                    $"[VCManager] Could not deserialize from VCUserDataJson: {_vcUserDataJson}, reason: {updatedToken.ToString()}");

            var updated = updatedToken.TokenType == TokenType.DataDictionary
                ? updatedToken.DataDictionary
                : new DataDictionary();

            if (!VRCJson.TryDeserializeFromJson(_lastVcUserDataJson, out var outdatedToken))
                Debug.LogError(
                    $"[VCManager] Could not deserialize from lastVCUserDataJson: {_lastVcUserDataJson}, reason: {outdatedToken.ToString()}");

            var outdated = outdatedToken.TokenType == TokenType.DataDictionary
                ? outdatedToken.DataDictionary
                : new DataDictionary();

            var updatedKeys = updated.GetKeys().ToArray();
            foreach (var key in updatedKeys)
            {
                if (_ContainsSameChannelId(_rxChannelId, updated[key].DataList) &&
                    !_activePlayerId.Contains(key) && int.TryParse(key.String, out var id))
                {
                    var playerApi = VRCPlayerApi.GetPlayerById(id);
                    if (playerApi == null || !Utilities.IsValid(playerApi)) continue;

                    _activePlayerId.Add(key);
                    _SetVCVoice(playerApi);
                    _Invoke_OnVoiceUpdated(playerApi, true);
                }

                outdated.Remove(key);
            }

            var outdatedKeys = outdated.GetKeys().ToArray();
            foreach (var key in outdatedKeys)
            {
                if (!_activePlayerId.Contains(key)) continue;

                _activePlayerId.Remove(key);

                if (!int.TryParse(key.String, out var id)) continue;

                var playerApi = VRCPlayerApi.GetPlayerById(id);
                if (playerApi == null || !Utilities.IsValid(playerApi)) continue;

                _SetDefaultVoice(playerApi);
                _Invoke_OnVoiceUpdated(playerApi, false);
            }

            _lastVcUserDataJson = _vcUserDataJson;
        }

        private void _UpdateJson(DataDictionary dict)
        {
            if (!VRCJson.TrySerializeToJson(dict, JsonExportType.Minify, out var result))
            {
                Debug.LogError($"[VCManager] Unable to serialize to json {_vcUserDataJson}: {result.ToString()}");
                return;
            }

            _vcUserDataJson = result.String;
        }

        private void _SetVCVoice(VRCPlayerApi api)
        {
            api.SetPlayerTag("VoiceCommsEnabled", "true");
            _SetVoice(api, vcGain, vcNear, vcFar, vcVolumetricRadius, vcLowpass);
        }

        private void _SetDefaultVoice(VRCPlayerApi api)
        {
            api.SetPlayerTag("VoiceCommsEnabled", "false");
            _SetVoice(api, defaultGain, defaultNear, defaultFar, defaultVolumetricRadius, defaultLowpass);
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

        private static DataDictionary _GetVCUserData(string source)
        {
            if (!VRCJson.TryDeserializeFromJson(source, out var result))
                Debug.LogError(
                    $"[VCManager] Could not get VC user data from source \"{source}\". {result.ToString()}");

            return result.TokenType == TokenType.DataDictionary
                ? result.DataDictionary
                : new DataDictionary();
        }

        private static void _UpdateVCUserRecord(DataDictionary dict, int playerId, bool isTransmitting,
            DataList txChannelId)
        {
            var key = new DataToken($"{playerId}");
            if (isTransmitting)
            {
                if (!dict.ContainsKey(key)) dict.Add(key, new DataToken(txChannelId));
                else dict.SetValue(key, new DataToken(txChannelId));

                return;
            }

            dict.Remove(key);
        }

        private static void _GetVCUserRecord(DataDictionary dict, int playerId, out bool isTransmitting,
            [CanBeNull] out DataList txChannelId)
        {
            var key = $"{playerId}";
            if (!dict.ContainsKey(key) || !dict.TryGetValue(key, TokenType.DataList, out var value))
            {
                isTransmitting = false;
                txChannelId = null;
                return;
            }

            isTransmitting = true;
            txChannelId = value.DataList;
        }

        private static bool _ContainsSameChannelId(DataList ch1, DataList ch2)
        {
            var ch1Arr = ch1.ToArray();
            var ch2Arr = ch2.ToArray();
            foreach (var ch1Token in ch1Arr)
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

        #endregion
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
    }
}
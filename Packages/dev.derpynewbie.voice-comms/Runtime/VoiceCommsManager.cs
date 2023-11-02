﻿using System;
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon.Common;

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

        /// <summary>
        /// TX channel ID for local player. transmits voice over these channels.
        /// </summary>
        /// <remarks>
        /// Returned list is deep cloned. Use methods such as <see cref="_AddTxChannel"/> or <see cref="_RemoveTxChannel"/> to interact with it instead.
        /// It's initialized with { 0 }.
        /// </remarks>
        /// <seealso cref="VoiceCommsManager._AddTxChannel"/>
        /// <seealso cref="VoiceCommsManager._RemoveTxChannel"/>
        [PublicAPI]
        public DataList TxChannelId => _txChannelId.DeepClone();

        /// <summary>
        /// RX Channel ID for local player. other player's VC transmission can be heard if one of channel id matches.
        /// </summary>
        /// <remarks>
        /// Returned list is deep cloned. Use methods such as <see cref="_AddRxChannel"/> or <see cref="_RemoveRxChannel"/> to interact with it instead.
        /// It's initialized with { 0 }.
        /// </remarks>
        /// <seealso cref="VoiceCommsManager._AddRxChannel"/>
        /// <seealso cref="VoiceCommsManager._RemoveRxChannel"/>
        [PublicAPI]
        public DataList RxChannelId => _rxChannelId.DeepClone();

        /// <summary>
        /// Players who has their VC activated currently will be in this list.
        /// </summary>
        [PublicAPI]
        public DataList ActivePlayerId => _activePlayerId.DeepClone();

        /// <summary>
        /// Is local transmitting voice over channel?
        /// </summary>
        [PublicAPI]
        public bool IsTransmitting { get; private set; }

        /// <summary>
        /// Begins VC transmission in <see cref="TxChannelId"/> from local player.
        /// </summary>
        [PublicAPI]
        public void _BeginVCTransmission()
        {
            Debug.Log("[VCManager] _BeginVCTransmission");

            IsTransmitting = true;
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
        }

        /// <summary>
        /// Ends VC transmission from local player.
        /// </summary>
        [PublicAPI]
        public void _EndVCTransmission()
        {
            Debug.Log("[VCManager] _EndVCTransmission");

            IsTransmitting = false;
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
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
        /// <returns>List of TX channels <paramref name="playerId"/> is using to transmit. can be empty</returns>
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
        /// <returns>List of TX channels player with <paramref name="playerId"/> is using to transmit. can be empty</returns>
        /// <remarks>
        /// Empty list means player is not transmitting
        /// </remarks>
        [PublicAPI] [NotNull]
        public DataList _GetTxChannels(int playerId)
        {
            var data = _GetVCUserData(_vcUserDataJson);
            return _GetVCUserRecord(data, playerId, out var isTransmitting, out var txChannels)
                ? txChannels
                : new DataList();
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
            _GetVCUserRecord(data, playerId, out var isTransmitting, out var txChannels);
            return isTransmitting;
        }

        /// <summary>
        /// Gets <paramref name="player"/>'s transmitting state
        /// </summary>
        /// <param name="player">VRCPlayerApi to get transmitting state</param>
        /// <returns>
        /// <c>true</c> if <paramref name="player"/> is actively transmitting.
        /// <c>false</c> otherwise
        /// </returns>
        /// <seealso cref="_IsTransmitting"/>
        [PublicAPI]
        public bool _IsActivelyTransmitting(VRCPlayerApi player)
        {
            return _activePlayerId.Contains($"{player.playerId}");
        }

        #endregion

        #region NetworkEvents

        public override void OnPreSerialization()
        {
            var dict = _GetVCUserData(_vcUserDataJson);
            _UpdateVCUserRecord(dict, Networking.LocalPlayer.playerId, IsTransmitting, _txChannelId);
            if (!VRCJson.TrySerializeToJson(dict, JsonExportType.Minify, out var result))
            {
                Debug.LogError($"[VCManager] Unable to serialize to json {_vcUserDataJson}: {result.ToString()}");
                return;
            }

            _vcUserDataJson = result.String;
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
            _GetVCUserRecord(dict, Networking.LocalPlayer.playerId, out var isTransmitting, out var txChannelId);
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

        private void _SetVCVoice(VRCPlayerApi api)
        {
            _SetVoice(api, vcGain, vcNear, vcFar, vcVolumetricRadius, vcLowpass);
        }

        private void _SetDefaultVoice(VRCPlayerApi api)
        {
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

            if (dict.ContainsKey(key)) dict.Remove(key);
        }

        private static bool _GetVCUserRecord(DataDictionary dict, int playerId, out bool isTransmitting,
            out DataList txChannelId)
        {
            var key = $"{playerId}";
            if (!dict.ContainsKey(key))
            {
                isTransmitting = default;
                txChannelId = default;
                return false;
            }

            if (!dict.TryGetValue(key, TokenType.DataList, out var value))
            {
                isTransmitting = default;
                txChannelId = default;
                return false;
            }

            isTransmitting = true;
            txChannelId = value.DataList;
            return true;
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

        public void _SubscribeCallback(VoiceCommsManagerCallback callback)
        {
            _callbacks.Add(callback);
        }

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
                if (obj != null) obj.OnVoiceUpdated(player, activated);
            }
        }

        #endregion
    }

    public abstract class VoiceCommsManagerCallback : UdonSharpBehaviour
    {
        public virtual void OnVoiceUpdated(VRCPlayerApi player, bool activated)
        {
        }
    }
}
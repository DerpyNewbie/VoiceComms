using DerpyNewbie.Common;
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
namespace DerpyNewbie.VoiceComms
{
    /// <summary>
    /// Base for activators which begin/end VoiceComms transmission from an object.
    /// </summary>
    public abstract class VoiceCommsObjectActivator : UdonSharpBehaviour
    {
        [SerializeField] [NewbieInject]
        protected VoiceCommsManager voiceComms;

        [SerializeField]
        [Tooltip("Interaction type name passed to VoiceCommsManager when transmitting.")]
        protected string interactionName = "Object";

        [SerializeField]
        [Tooltip("Maximum duration for continuous transmission. Set this 0 or below for unlimited duration.")]
        protected float maxDuration;

        [SerializeField]
        [Tooltip("Replaces TX channels while transmitting from this activator, and restores them afterwards.")]
        protected bool doTxChannelOverrideOnTransmit;

        [SerializeField]
        protected int[] txChannelOverrideOnTransmit;

        private bool _isTransmitting;
        private float _endTime;
        private DataList _savedTxChannels;

        [PublicAPI]
        public bool IsTransmitting => _isTransmitting;

        protected virtual void Update()
        {
            if (_isTransmitting && HasDurationLimit() && _endTime < Time.timeSinceLevelLoad)
                _EndTransmission();
        }

        protected virtual void OnDisable()
        {
            _EndTransmission();
        }

        /// <summary>
        /// Begins transmission from this activator. Does nothing if already transmitting.
        /// </summary>
        [PublicAPI]
        public void _BeginTransmission()
        {
            if (_isTransmitting) return;
            if (!voiceComms)
            {
                Debug.LogError($"[VoiceCommsObjectActivator-{name}] VoiceCommsManager is not assigned");
                return;
            }

            _isTransmitting = true;
            if (HasDurationLimit()) _endTime = Time.timeSinceLevelLoad + maxDuration;

            ApplyTxChannelOverride();
            voiceComms._BeginVCTransmission(interactionName);
        }

        /// <summary>
        /// Ends transmission from this activator. Does nothing if not transmitting.
        /// </summary>
        [PublicAPI]
        public void _EndTransmission()
        {
            if (!_isTransmitting) return;
            if (!voiceComms)
            {
                Debug.LogError($"[VoiceCommsObjectActivator-{name}] VoiceCommsManager is not assigned");
                return;
            }

            _isTransmitting = false;
            voiceComms._EndVCTransmission(interactionName);
            RestoreTxChannel();
        }

        /// <summary>
        /// Toggles transmission from this activator.
        /// </summary>
        [PublicAPI]
        public void _ToggleTransmission()
        {
            if (_isTransmitting) _EndTransmission();
            else _BeginTransmission();
        }

        private void ApplyTxChannelOverride()
        {
            if (!doTxChannelOverrideOnTransmit || txChannelOverrideOnTransmit == null) return;

            _savedTxChannels = voiceComms.TxChannelId;
            voiceComms._ClearTxChannel();
            foreach (var channel in txChannelOverrideOnTransmit)
                voiceComms._AddTxChannel(channel);
        }

        private void RestoreTxChannel()
        {
            if (_savedTxChannels == null) return;

            voiceComms._ClearTxChannel();
            var channels = _savedTxChannels.ToArray();
            foreach (var channel in channels)
                voiceComms._AddTxChannel(channel.Int);

            _savedTxChannels = null;
        }

        private bool HasDurationLimit()
        {
            return maxDuration > 0;
        }
    }
}

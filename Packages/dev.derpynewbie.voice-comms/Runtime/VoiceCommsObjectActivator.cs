using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;

namespace DerpyNewbie.VoiceComms
{
    /// <summary>
    /// Base for activators which begin/end VoiceComms transmission from an object.
    /// </summary>
    public abstract class VoiceCommsObjectActivator : UdonSharpBehaviour
    {
        [SerializeField]
        protected VoiceCommsManager voiceComms;

        [SerializeField]
        [Tooltip("Interaction type name passed to VoiceCommsManager when transmitting.")]
        protected string interactionName = "Object";

        [SerializeField]
        [Tooltip("Maximum duration for continuous transmission. Set this 0 or below for unlimited duration.")]
        protected float maxDuration;

        private bool _isTransmitting;
        private float _endTime;

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

        private bool HasDurationLimit()
        {
            return maxDuration > 0;
        }
    }
}

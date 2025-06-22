using DerpyNewbie.Common;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace DerpyNewbie.VoiceComms.UI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VoiceCommsLocalSettingsUI : VoiceCommsManagerCallback
    {
        [SerializeField] [NewbieInject]
        private VoiceCommsManager voiceCommsManager;

        [SerializeField]
        private Slider gainSlider;

        [SerializeField]
        private Slider farSlider;

        [SerializeField]
        private Slider nearSlider;

        [SerializeField]
        private Slider volumetricRadiusSlider;

        [SerializeField]
        private Toggle lowpassToggle;

        private bool _ignoreEvents;

        private float _initialGain;
        private float _initialFar;
        private float _initialNear;
        private float _initialVolumetricRadius;
        private bool _initialLowpass;

        private void Start()
        {
            voiceCommsManager._SubscribeCallback(this);

            voiceCommsManager._GetDefaultVcSettings(
                out _initialGain, out _initialFar, out _initialNear,
                out _initialVolumetricRadius, out _initialLowpass
            );

            Refresh();
        }

        public void OnValueChanged()
        {
            if (_ignoreEvents) return;

            voiceCommsManager._GetDefaultVcSettings(
                out var gain, out var far, out var near,
                out var volumetricRadius, out var lowpass
            );

            if (gainSlider) gain = gainSlider.value;
            if (farSlider) far = farSlider.value;
            if (nearSlider) near = nearSlider.value;
            if (volumetricRadiusSlider) volumetricRadius = volumetricRadiusSlider.value;
            if (lowpassToggle) lowpass = lowpassToggle.isOn;

            voiceCommsManager._SetDefaultVcSettings(gain, near, far, volumetricRadius, lowpass);
        }

        public void Refresh()
        {
            if (_ignoreEvents) return;
            _ignoreEvents = true;

            voiceCommsManager._GetDefaultVcSettings(
                out var gain, out var far, out var near,
                out var volumetricRadius, out var lowpass
            );

            if (gainSlider) gainSlider.value = gain;
            if (farSlider) farSlider.value = far;
            if (nearSlider) nearSlider.value = near;
            if (volumetricRadiusSlider) volumetricRadiusSlider.value = volumetricRadius;
            if (lowpassToggle) lowpassToggle.isOn = lowpass;

            _ignoreEvents = false;
        }

        public void OnResetButton()
        {
            voiceCommsManager._SetDefaultVcSettings(
                _initialGain, _initialNear, _initialFar,
                _initialVolumetricRadius, _initialLowpass
            );

            Refresh();
        }
    }
}
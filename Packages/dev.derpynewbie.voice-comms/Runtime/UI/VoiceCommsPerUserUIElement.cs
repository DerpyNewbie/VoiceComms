using DerpyNewbie.Common;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace DerpyNewbie.VoiceComms.UI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VoiceCommsPerUserUIElement : UdonSharpBehaviour
    {
        [SerializeField] [NewbieInject]
        private VoiceCommsManager voiceCommsManager;

        [SerializeField]
        private TMP_Text displayNameText;

        [SerializeField]
        private Slider gainMultiplierSlider;

        [SerializeField]
        private Toggle suppressToggle;

        private bool _ignoreEvents;

        private string _targetDisplayName;

        public void Setup(string displayName)
        {
            _targetDisplayName = displayName;
            Refresh();
        }

        public void OnValueChanged()
        {
            if (_ignoreEvents) return;

            voiceCommsManager._GetDefaultVcSettings(
                out var gain, out var near, out var far, out var radius, out var lowpass
            );

            voiceCommsManager._SetUserVcSettings(
                _targetDisplayName,
                suppressToggle.isOn, gain * gainMultiplierSlider.value, near, far, radius, lowpass
            );
        }

        public void Refresh()
        {
            if (_ignoreEvents) return;
            _ignoreEvents = true;

            voiceCommsManager._GetDefaultVcSettings(
                out var gain, out var near, out var far, out var radius, out var lowpass
            );

            voiceCommsManager._GetUserVcSettings(
                _targetDisplayName,
                out var suppressed,
                out var userGain, out var userNear, out var userFar, out var userRadius, out var userLowpass
            );

            displayNameText.text = _targetDisplayName;
            gainMultiplierSlider.value = Mathf.Approximately(gain, 0) ? 1 : Mathf.Clamp01(userGain / gain);
            suppressToggle.isOn = suppressed;

            _ignoreEvents = false;
        }
    }
}
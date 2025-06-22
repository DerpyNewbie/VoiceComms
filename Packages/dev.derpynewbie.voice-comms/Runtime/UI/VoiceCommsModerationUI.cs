using DerpyNewbie.Common;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace DerpyNewbie.VoiceComms.UI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VoiceCommsModerationUI : UdonSharpBehaviour
    {
        [SerializeField] [NewbieInject]
        private VoiceCommsManager voiceCommsManager;

        [SerializeField]
        private Toggle voiceCommsEnabledToggle;

        public void OnValueChanged()
        {
            if (voiceCommsEnabledToggle) voiceCommsManager.IsVoiceCommsEnabled = voiceCommsEnabledToggle.isOn;
        }

        public void OnResetButton()
        {
            voiceCommsManager._ClearAllTransmissionsGlobally();
        }
    }
}
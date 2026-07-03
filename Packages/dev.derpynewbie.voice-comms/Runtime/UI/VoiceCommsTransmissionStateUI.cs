using DerpyNewbie.Common;
using UnityEngine;
namespace DerpyNewbie.VoiceComms.UI
{
    public class VoiceCommsTransmissionStateUI : VoiceCommsManagerCallback
    {
        [SerializeField] [NewbieInject]
        private VoiceCommsManager voiceCommsManager;

        [SerializeField]
        private GameObject transmissionStateDisplay;
        [SerializeField]
        private CanvasGroup transmissionStateDisplayCanvasGroup;

        private void Start()
        {
            voiceCommsManager._SubscribeCallback(this);
            SetDisplay(false);
        }

        private void SetDisplay(bool isEnabled)
        {
            if (transmissionStateDisplayCanvasGroup) transmissionStateDisplayCanvasGroup.alpha = isEnabled ? 1 : 0;
            if (transmissionStateDisplay) transmissionStateDisplay.SetActive(isEnabled);
        }

        public override void OnBeginTransmission(string interactionType)
        {
            SetDisplay(voiceCommsManager.IsTransmitting);
        }

        public override void OnEndTransmission(string interactionType)
        {
            SetDisplay(voiceCommsManager.IsTransmitting);
        }
    }
}

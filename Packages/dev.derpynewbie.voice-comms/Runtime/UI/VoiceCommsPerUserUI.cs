using DerpyNewbie.Common;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace DerpyNewbie.VoiceComms.UI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VoiceCommsPerUserUI : VoiceCommsManagerCallback
    {
        [SerializeField] [NewbieInject]
        private VoiceCommsManager voiceCommsManager;

        [SerializeField]
        private GameObject userElement;

        [SerializeField]
        private Transform userElementParent;

        private readonly DataList _userElements = new DataList();

        private void Start()
        {
            userElement.SetActive(false);
            voiceCommsManager._SubscribeCallback(this);
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (player.isLocal || HasUser(player.displayName)) return;

            CreateUser(player.displayName);
        }

        private bool HasUser(string displayName)
        {
            foreach (Transform child in userElementParent)
                if (child.name == displayName)
                    return true;
            return false;
        }

        private void CreateUser(string displayName)
        {
            var userElementCopy = Instantiate(userElement, userElementParent);
            userElementCopy.name = displayName;
            var element = userElementCopy.GetComponent<VoiceCommsPerUserUIElement>();
            element.Setup(displayName);
            _userElements.Add(element);
            userElementCopy.SetActive(true);
        }

        public void OnResetButton()
        {
            voiceCommsManager._ClearUserVcSettings();
        }

        public override void OnVoiceSettingsUpdated(string displayName)
        {
            foreach (var token in _userElements.ToArray())
            {
                var element = (VoiceCommsPerUserUIElement)token.Reference;
                if (element != null) element.Refresh();
            }
        }

        public override void OnVoiceDefaultSettingsUpdated(float prevGain, float prevNear, float prevFar,
            float prevVolumetricRadius,
            bool prevLowpass)
        {
            foreach (var token in _userElements.ToArray())
            {
                var element = (VoiceCommsPerUserUIElement)token.Reference;
                if (element != null) element.OnValueChanged();
            }
        }
    }
}
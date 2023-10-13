using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;

namespace DerpyNewbie.VoiceComms
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VoiceCommsSoundEffectPlayer : VoiceCommsManagerCallback
    {
        [SerializeField]
        private VoiceCommsManager voiceComms;
        [SerializeField]
        private AudioSource onActivatedLocalAudio;
        [SerializeField]
        private AudioSource onDeactivatedLocalAudio;
        [SerializeField]
        private AudioSource onActivatedRemoteAudio;
        [SerializeField]
        private AudioSource onDeactivatedRemoteAudio;
        [SerializeField]
        private AudioSource onRadioWhiteNoiseAudio;

        private void Start()
        {
            voiceComms._SubscribeCallback(this);
        }

        public override void PostLateUpdate()
        {
            var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            transform.SetPositionAndRotation(head.position, head.rotation);
        }

        public override void OnVoiceUpdated(VRCPlayerApi player, bool activated)
        {
            if (player == null || !Utilities.IsValid(player)) return;
            var local = player.isLocal;

            if (activated) (local ? onActivatedLocalAudio : onActivatedRemoteAudio).Play();
            else (local ? onDeactivatedLocalAudio : onDeactivatedRemoteAudio).Play();

            var active = voiceComms.ActivePlayerId;
            if (active.Count == 0 || active.Contains($"{Networking.LocalPlayer.playerId}") && active.Count == 1)
                onRadioWhiteNoiseAudio.Stop();
            else
                onRadioWhiteNoiseAudio.Play();
        }
    }
}
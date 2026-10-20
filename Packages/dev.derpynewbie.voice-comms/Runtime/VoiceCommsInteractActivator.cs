using UdonSharp;

namespace DerpyNewbie.VoiceComms
{
    /// <summary>
    /// Toggles VoiceComms transmission when this object is interacted.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VoiceCommsInteractActivator : VoiceCommsObjectActivator
    {
        public override void Interact()
        {
            _ToggleTransmission();
        }
    }
}

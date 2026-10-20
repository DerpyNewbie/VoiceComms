using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;

namespace DerpyNewbie.VoiceComms
{
    /// <summary>
    /// Activates VoiceComms transmission from VRCPickup events.
    /// </summary>
    /// <remarks>
    /// Transmission always ends when the pickup is dropped.
    /// </remarks>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VoiceCommsPickupActivator : VoiceCommsObjectActivator
    {
        [SerializeField]
        [Tooltip("Pickup: transmits while held.\n" +
                 "PickupUse: transmits while use is held. Requires AutoHold.\n" +
                 "PickupUseToggle: toggles transmission on each use. Requires AutoHold.")]
        private PickupActivatorMode mode;

        [PublicAPI]
        public PickupActivatorMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public override void OnPickup()
        {
            if (mode == PickupActivatorMode.Pickup) _BeginTransmission();
        }

        public override void OnDrop()
        {
            _EndTransmission();
        }

        public override void OnPickupUseDown()
        {
            if (mode == PickupActivatorMode.PickupUse) _BeginTransmission();
            else if (mode == PickupActivatorMode.PickupUseToggle) _ToggleTransmission();
        }

        public override void OnPickupUseUp()
        {
            if (mode == PickupActivatorMode.PickupUse) _EndTransmission();
        }
    }

    public enum PickupActivatorMode
    {
        Pickup,
        PickupUse,
        PickupUseToggle
    }
}

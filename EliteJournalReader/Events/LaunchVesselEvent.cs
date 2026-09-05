namespace EliteJournalReader.Events
{
    //When written: when launching a player-controlled vessel
    //Parameters:
    // VesselType
    // VesselType_Localised
    // Loadout
    // ID
    // PlayerControlled
    public class LaunchVesselEvent : JournalEvent<LaunchVesselEvent.LaunchVesselEventArgs>
    {
        public LaunchVesselEvent() : base("LaunchVessel") { }

        public class LaunchVesselEventArgs : JournalEventArgs
        {
            public string VesselType { get; set; }
            public string VesselType_Localised { get; set; }
            public string Loadout { get; set; }
            public long ID { get; set; }
            public bool PlayerControlled { get; set; }
        }
    }
}
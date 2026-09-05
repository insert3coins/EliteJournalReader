namespace EliteJournalReader.Events
{
    public class GameModeChangeEvent : JournalEvent<GameModeChangeEvent.GameModeChangeEventArgs>
    {
        public GameModeChangeEvent() : base("GameModeChange") { }

        public class GameModeChangeEventArgs : JournalEventArgs
        {
            public string GameMode { get; set; }
        }
    }
}
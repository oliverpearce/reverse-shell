namespace Game.World
{
    /// <summary>
    /// Chapter One's story flags, in the order the chapter sets them, and the objective they imply. The director
    /// (<see cref="ChapterOneDirector"/>) sets the flags; everything else only reads them.
    /// </summary>
    public static class Chapter1
    {
        public const string Named = "ch1.named";                 // the opening asked for a name
        public const string Awake = "ch1.awake";                 // woke up beside the procedure pod
        public const string Memory1 = "ch1.memory.1";            // optional memory fragments
        public const string Memory2 = "ch1.memory.2";
        public const string Memory3 = "ch1.memory.3";
        public const string Scanner = "ch1.scanner";             // picked up the optical scanner
        public const string CollectorMet = "ch1.collector.met";  // the Collector stopped you
        public const string CollectorBeaten = "ch1.collector.beaten";
        public const string Route = "ch1.route";                 // the scanner opened the hidden route
        public const string Tunnel = "ch1.tunnel";               // entered the buried tunnel
        public const string DragonSeen = "ch1.dragon.seen";      // saw the guardian
        public const string DragonBeaten = "ch1.dragon.beaten";
        public const string ResonanceVolt = "ch1.resonance.volt"; // the Soul Card bound to the Core
        public const string Complete = "ch1.complete";           // saw the ending

        /// <summary>Every flag in story order (the debug panel lists them).</summary>
        public static readonly string[] All =
        {
            Named, Awake, Memory1, Memory2, Memory3, Scanner, CollectorMet, CollectorBeaten, Route, Tunnel,
            DragonSeen, DragonBeaten, ResonanceVolt, Complete,
        };

        /// <summary>The objective shown on the overworld HUD, worked out from the flags alone ("" = none). The words are
        /// the objective.* blocks of Resources/Story/World.txt.</summary>
        public static string CurrentObjective()
        {
            string key = ObjectiveKey();
            return key == null ? "" : StoryText.Text(key);
        }

        static string ObjectiveKey()
        {
            if (!StoryState.Has(Awake)) return null;
            if (StoryState.Has(Complete)) return "objective.complete";
            if (StoryState.Has(ResonanceVolt)) return "objective.leave";
            if (StoryState.Has(DragonBeaten)) return "objective.card";
            if (StoryState.Has(DragonSeen)) return "objective.guardian";
            if (StoryState.Has(Tunnel)) return "objective.tunnel";
            if (StoryState.Has(Route)) return "objective.route";
            if (StoryState.Has(CollectorBeaten)) return "objective.scan";
            if (StoryState.Has(CollectorMet)) return "objective.collector";
            if (StoryState.Has(Scanner)) return "objective.scanner";
            return "objective.explore";
        }
    }
}

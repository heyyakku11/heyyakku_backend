namespace Yakku.Application.Users.AnonymousNames
{
    /// <summary>
    /// The word categories anonymous names are built from. Pure data: add words here (or add a category and
    /// extend <see cref="AnonymousNameFormat"/>) without touching the generation or profile logic. Every word is
    /// a single capitalised alphabetic token so the joined name stays readable (for example "SilentTigerMoon").
    /// </summary>
    public static class AnonymousNameWords
    {
        public static readonly IReadOnlyList<string> Adjectives =
        [
            "Silent", "Brave", "Clever", "Mighty", "Cosmic", "Lucky", "Swift", "Hidden", "Gentle", "Bold",
            "Calm", "Daring", "Eager", "Fierce", "Golden", "Happy", "Icy", "Jolly", "Keen", "Lively",
            "Mellow", "Noble", "Odd", "Proud", "Quick", "Rapid", "Shiny", "Silver", "Sunny", "Tiny",
            "Wild", "Witty", "Zesty", "Ancient", "Bright", "Crimson", "Dusty", "Electric", "Frosty", "Glowing",
            "Humble", "Iron", "Jade", "Kind", "Lunar", "Misty", "Neon", "Orange", "Purple", "Quiet",
            "Rustic", "Sleepy", "Stormy", "Velvet", "Wandering", "Amber", "Blazing", "Curious", "Dreamy", "Fuzzy"
        ];

        public static readonly IReadOnlyList<string> Animals =
        [
            "Tiger", "Panda", "Falcon", "Wolf", "Eagle", "Fox", "Dolphin", "Phoenix", "Otter", "Lynx",
            "Raven", "Bear", "Hawk", "Koala", "Lion", "Moose", "Owl", "Puma", "Seal", "Swan",
            "Turtle", "Whale", "Yak", "Zebra", "Badger", "Bison", "Cobra", "Crane", "Deer", "Gecko",
            "Heron", "Ibis", "Jaguar", "Lemur", "Manta", "Newt", "Orca", "Parrot", "Quail", "Robin",
            "Shark", "Stork", "Tapir", "Viper", "Walrus", "Wombat", "Finch", "Beaver", "Camel", "Dragon",
            "Eel", "Ferret", "Gull", "Hare", "Jackal", "Kiwi", "Llama", "Mole", "Panther", "Raccoon"
        ];

        public static readonly IReadOnlyList<string> NatureAndObjects =
        [
            "Moon", "Storm", "Shadow", "River", "Comet", "Mountain", "Thunder", "Galaxy", "Ocean", "Forest",
            "Meadow", "Canyon", "Desert", "Island", "Glacier", "Harbor", "Lantern", "Compass", "Anchor", "Arrow",
            "Beacon", "Blossom", "Breeze", "Cloud", "Crystal", "Dawn", "Dune", "Ember", "Flame", "Fog",
            "Frost", "Garden", "Horizon", "Lake", "Leaf", "Meteor", "Nebula", "Orbit", "Pebble", "Planet",
            "Rain", "Reef", "Ridge", "Sky", "Spark", "Star", "Summit", "Sunrise", "Tide", "Valley",
            "Wave", "Willow", "Wind", "Zephyr", "Aurora", "Bridge", "Castle", "Cinder", "Echo", "Prism"
        ];
    }
}

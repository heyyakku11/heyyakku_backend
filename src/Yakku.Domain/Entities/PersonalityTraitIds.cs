namespace Yakku.Domain.Entities
{
    /// <summary>
    /// The fixed ids of the predefined personality traits, in display order. The migration seeds the rows with these
    /// same values, so the database, the API and the code all refer to the same traits.
    /// </summary>
    public static class PersonalityTraitIds
    {
        public static readonly Guid Confident = new("b2e4d1a0-0000-4000-8000-000000000001");
        public static readonly Guid Introverted = new("b2e4d1a0-0000-4000-8000-000000000002");
        public static readonly Guid Creative = new("b2e4d1a0-0000-4000-8000-000000000003");
        public static readonly Guid Empathetic = new("b2e4d1a0-0000-4000-8000-000000000004");
        public static readonly Guid Ambitious = new("b2e4d1a0-0000-4000-8000-000000000005");
        public static readonly Guid Humorous = new("b2e4d1a0-0000-4000-8000-000000000006");
        public static readonly Guid Intelligent = new("b2e4d1a0-0000-4000-8000-000000000007");
        public static readonly Guid Calm = new("b2e4d1a0-0000-4000-8000-000000000008");
        public static readonly Guid Adventurous = new("b2e4d1a0-0000-4000-8000-000000000009");
        public static readonly Guid Reliable = new("b2e4d1a0-0000-4000-8000-000000000010");

        public static readonly IReadOnlyList<Guid> All = new[]
        {
            Confident, Introverted, Creative, Empathetic, Ambitious, Humorous, Intelligent, Calm, Adventurous, Reliable
        };
    }
}

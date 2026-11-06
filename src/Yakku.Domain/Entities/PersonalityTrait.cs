namespace Yakku.Domain.Entities
{
    /// <summary>A predefined personality trait. Ids are fixed (seeded by migration) so clients and data can rely on them.</summary>
    public class PersonalityTrait
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }
        public int DisplayOrder { get; private set; }

        private PersonalityTrait()
        {
        }
    }
}

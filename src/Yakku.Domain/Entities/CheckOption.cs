namespace Yakku.Domain.Entities
{
    public class CheckOption
    {
        public Guid Id { get; private set; }
        public Guid CheckId { get; private set; }
        public string Text { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }

        public Check Check { get; private set; } = null!;
        public List<UserCheckVote> Votes { get; private set; } = new();

        private CheckOption()
        {
        }

        public static CheckOption Create(Guid checkId, string text, int sortOrder)
        {
            return new CheckOption { Id = Guid.NewGuid(), CheckId = checkId, Text = text, SortOrder = sortOrder };
        }
    }
}

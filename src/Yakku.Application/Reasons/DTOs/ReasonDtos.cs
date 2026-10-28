namespace Yakku.Application.Reasons.DTOs
{
    /// <summary>One reason a voter gave for an option. Anonymous: it carries no author.</summary>
    public class ReasonResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}

namespace Yakku.Domain.Entities
{
    public class Comment
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public Guid AuthorId { get; private set; }
        public Guid? ParentCommentId { get; private set; }
        public string Content { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime? DeletedAt { get; private set; }
        public Guid? DeletedBy { get; private set; }
        public bool IsEdited { get; private set; }
        public DateTime? EditedAt { get; private set; }

        public Poll Poll { get; private set; } = null!;
        public User Author { get; private set; } = null!;
        public Comment? ParentComment { get; private set; }
        public ICollection<Comment> Replies { get; private set; } = new List<Comment>();

        private Comment()
        {
        }

        public Comment(Guid pollId, Guid authorId, string content, Guid? parentCommentId = null)
        {
            Id = Guid.NewGuid();
            PollId = pollId;
            AuthorId = authorId;
            ParentCommentId = parentCommentId;
            Content = content;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
            IsEdited = false;
        }
    }
}

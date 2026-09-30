using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class CommentConfiguration : IEntityTypeConfiguration<Comment>
    {
        public void Configure(EntityTypeBuilder<Comment> builder)
        {
            builder.ToTable("Comments");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PollId)
                .IsRequired();

            builder.Property(x => x.AuthorId)
                .IsRequired();

            builder.Property(x => x.Content)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(x => x.IsEdited)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasIndex(x => x.PollId)
                .HasDatabaseName("IX_Comments_PollId");

            builder.HasIndex(x => x.AuthorId)
                .HasDatabaseName("IX_Comments_AuthorId");

            builder.HasIndex(x => x.ParentCommentId)
                .HasDatabaseName("IX_Comments_ParentCommentId");

            builder.HasIndex(x => new { x.PollId, x.CreatedAt })
                .HasDatabaseName("IX_Comments_PollId_CreatedAt");

            builder.HasOne(x => x.Poll)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Author)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ParentComment)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentCommentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

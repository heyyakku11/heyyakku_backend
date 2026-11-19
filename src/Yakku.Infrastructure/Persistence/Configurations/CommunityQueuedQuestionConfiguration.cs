using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class CommunityQueuedQuestionConfiguration : IEntityTypeConfiguration<CommunityQueuedQuestion>
    {
        public void Configure(EntityTypeBuilder<CommunityQueuedQuestion> builder)
        {
            builder.ToTable("community_queued_questions", table =>
            {
                table.HasCheckConstraint(
                    "CK_community_queued_questions_question_not_blank",
                    "char_length(btrim(\"question\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(x => x.Question)
                .HasColumnName("question")
                .HasMaxLength(CommunityQueuedQuestion.QuestionMaxLength)
                .IsRequired();
            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired()
                .HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.UserId, x.CreatedAt })
                .HasDatabaseName("IX_community_queued_questions_user_id_created_at");

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Tags)
                .WithOne(x => x.Question)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal class CommunityQueuedQuestionTagConfiguration : IEntityTypeConfiguration<CommunityQueuedQuestionTag>
    {
        public void Configure(EntityTypeBuilder<CommunityQueuedQuestionTag> builder)
        {
            builder.ToTable("community_queued_question_tags", table =>
            {
                table.HasCheckConstraint(
                    "CK_community_queued_question_tags_tag",
                    "\"tag\" IN ('friendships', 'career', 'family', 'love', 'money')");
            });

            builder.HasKey(x => new { x.QuestionId, x.Tag });

            builder.Property(x => x.QuestionId).HasColumnName("question_id");
            builder.Property(x => x.Tag)
                .HasColumnName("tag")
                .HasMaxLength(32)
                .IsRequired();

            builder.HasIndex(x => x.Tag)
                .HasDatabaseName("IX_community_queued_question_tags_tag");
        }
    }
}

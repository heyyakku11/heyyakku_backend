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

    internal class CardTagConfiguration : IEntityTypeConfiguration<CardTag>
    {
        public void Configure(EntityTypeBuilder<CardTag> builder)
        {
            builder.ToTable("card_tags");

            builder.HasKey(x => new { x.QuestionId, x.CategoryId });

            builder.Property(x => x.QuestionId).HasColumnName("question_id");
            builder.Property(x => x.CategoryId).HasColumnName("category_id").IsRequired();

            builder.HasIndex(x => x.CategoryId)
                .HasDatabaseName("IX_card_tags_category_id");

            builder.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    internal class CardCategoryConfiguration : IEntityTypeConfiguration<CardCategory>
    {
        public void Configure(EntityTypeBuilder<CardCategory> builder)
        {
            builder.ToTable("card_categories", table =>
            {
                table.HasCheckConstraint(
                    "CK_card_categories_code_not_blank",
                    "char_length(btrim(\"code\")) > 0");
                table.HasCheckConstraint(
                    "CK_card_categories_name_not_blank",
                    "char_length(btrim(\"name\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("IX_card_categories_code");
            builder.HasIndex(x => x.SortOrder).HasDatabaseName("IX_card_categories_sort_order");

            var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            builder.HasData(
                new { Id = CardCategory.FamilyId, Code = "family", Name = "Family", SortOrder = 1, IsActive = true, CreatedAt = seededAt },
                new { Id = CardCategory.MoneyId, Code = "money", Name = "Money", SortOrder = 2, IsActive = true, CreatedAt = seededAt },
                new { Id = CardCategory.LoveId, Code = "love", Name = "Love", SortOrder = 3, IsActive = true, CreatedAt = seededAt },
                new { Id = CardCategory.CareerId, Code = "career", Name = "Career", SortOrder = 4, IsActive = true, CreatedAt = seededAt },
                new { Id = CardCategory.FriendshipId, Code = "friendship", Name = "Friendship", SortOrder = 5, IsActive = true, CreatedAt = seededAt });
        }
    }
}

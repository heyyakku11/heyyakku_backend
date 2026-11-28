using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class UserPlanConfiguration : IEntityTypeConfiguration<UserPlan>
    {
        public void Configure(EntityTypeBuilder<UserPlan> builder)
        {
            builder.ToTable("user_plans");

            // One row per user: the user id is both the key and the foreign key, which makes the 1:1 a hard rule.
            builder.HasKey(x => x.UserId);
            builder.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();

            // The plan's code (plans.code). Rows created without one are on the free plan.
            builder.Property(x => x.PlanCode)
                .HasColumnName("plan_code")
                .HasMaxLength(32)
                .IsRequired()
                .HasDefaultValue(Plan.FreeCode);

            builder.HasIndex(x => x.PlanCode).HasDatabaseName("IX_user_plans_plan_code");

            // Set while a paid plan is active; null on the free plan. After the end date the user counts as free.
            builder.Property(x => x.PlanActivatedAt).HasColumnName("plan_activated_at");
            builder.Property(x => x.PlanExpiresAt).HasColumnName("plan_expires_at");
        }
    }
}

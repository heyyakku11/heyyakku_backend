using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePremiumPlanToYakkuLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The second plan is now "Yakku Lite". Only its label and code change; its id, price and the users on it
            // stay as they are.
            migrationBuilder.Sql("""
                UPDATE plans
                SET code = 'yakku_lite', name = 'Yakku Lite', updated_at = now()
                WHERE id = 'a1f3c0de-0000-4000-8000-000000000002';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE plans
                SET code = 'premium', name = 'Premium', updated_at = now()
                WHERE id = 'a1f3c0de-0000-4000-8000-000000000002';
                """);
        }
    }
}

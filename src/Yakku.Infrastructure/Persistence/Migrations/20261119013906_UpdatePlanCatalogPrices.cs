using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePlanCatalogPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // free ₹0; this_card ₹49; yakku_lite ₹99 / 7 days / 3 tickets; yakku_love ₹299 / 30 days / unlimited.
            // Reorder sort_order safely (unique index), then insert the one-card SKU.
            migrationBuilder.Sql(
                """
                UPDATE plans SET sort_order = 40 WHERE code = 'yakku_love';
                UPDATE plans SET sort_order = 30 WHERE code = 'yakku_lite';

                UPDATE plans
                SET price_minor = 9900,
                    duration_days = 7,
                    unlock_tickets = 3,
                    ask_back_message_limit = 3,
                    updated_at = now()
                WHERE code = 'yakku_lite';

                UPDATE plans
                SET price_minor = 29900,
                    duration_days = 30,
                    unlock_tickets = NULL,
                    ask_back_message_limit = NULL,
                    updated_at = now()
                WHERE code = 'yakku_love';

                UPDATE plans
                SET price_minor = 0,
                    duration_days = NULL,
                    unlock_tickets = 0,
                    ask_back_message_limit = 1,
                    updated_at = now()
                WHERE code = 'free';

                INSERT INTO plans (
                    id, code, name, sort_order, price_minor, currency, is_active,
                    duration_days, unlock_tickets, ask_back_message_limit, created_at, updated_at
                )
                VALUES (
                    'a1f3c0de-0000-4000-8000-000000000004',
                    'this_card',
                    'This card',
                    2,
                    4900,
                    'INR',
                    true,
                    NULL,
                    1,
                    1,
                    now(),
                    now()
                )
                ON CONFLICT (id) DO UPDATE SET
                    code = EXCLUDED.code,
                    name = EXCLUDED.name,
                    sort_order = EXCLUDED.sort_order,
                    price_minor = EXCLUDED.price_minor,
                    unlock_tickets = EXCLUDED.unlock_tickets,
                    ask_back_message_limit = EXCLUDED.ask_back_message_limit,
                    updated_at = now();

                UPDATE plans SET sort_order = 3, updated_at = now() WHERE code = 'yakku_lite';
                UPDATE plans SET sort_order = 4, updated_at = now() WHERE code = 'yakku_love';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM plans WHERE id = 'a1f3c0de-0000-4000-8000-000000000004';

                UPDATE plans SET sort_order = 30 WHERE code = 'yakku_lite';
                UPDATE plans SET sort_order = 40 WHERE code = 'yakku_love';

                UPDATE plans
                SET price_minor = 4900,
                    duration_days = 7,
                    unlock_tickets = 3,
                    ask_back_message_limit = 3,
                    sort_order = 2,
                    updated_at = now()
                WHERE code = 'yakku_lite';

                UPDATE plans
                SET price_minor = 12900,
                    duration_days = 30,
                    unlock_tickets = NULL,
                    ask_back_message_limit = NULL,
                    sort_order = 3,
                    updated_at = now()
                WHERE code = 'yakku_love';
                """);
        }
    }
}

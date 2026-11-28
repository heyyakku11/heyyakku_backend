using Microsoft.EntityFrameworkCore;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence;

/// <summary>
/// Gives an asker a fresh question budget on their conversations when they buy a user plan (Lite or Love). Every path
/// that activates a paid plan calls this, so a purchase behaves the same whichever one ran. Cards are not touched and
/// the asker's top-up credits are kept; blocked chats stay as they are.
/// </summary>
internal static class AskBackBudgetReset
{
    public static Task ForPlanPurchaseAsync(
        YakkuDbContext context,
        Guid askerUserId,
        int? planMessageLimit,
        CancellationToken cancellationToken)
    {
        return context.AskBackConversations
            .Where(conversation => conversation.CreatorUserId == askerUserId
                                   && conversation.Status != AskBackStatus.Blocked)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(conversation => conversation.AskerQuotaOffset, conversation => conversation.AskerMessageCount)
                    .SetProperty(conversation => conversation.AppliedMessageLimit, planMessageLimit),
                cancellationToken);
    }
}

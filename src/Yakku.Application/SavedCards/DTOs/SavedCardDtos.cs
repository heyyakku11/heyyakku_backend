using Yakku.Application.Checks.DTOs;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.SavedCards.DTOs
{
    /// <summary>How a card kind is written in the API.</summary>
    public static class SavedCardTypes
    {
        public const string Poll = "poll";
        public const string FunCard = "fun_card";

        public static string ToApi(SavedCardType type) => type == SavedCardType.Poll ? Poll : FunCard;

        /// <summary>Accepts "poll", "fun_card", "fun-card" and "funcard", in any case.</summary>
        public static bool TryParse(string? value, out SavedCardType type)
        {
            switch (value?.Trim().ToLowerInvariant().Replace("-", "_"))
            {
                case "poll":
                case "polls":
                    type = SavedCardType.Poll;
                    return true;
                case "fun_card":
                case "funcard":
                case "fun_cards":
                    type = SavedCardType.FunCard;
                    return true;
                default:
                    type = default;
                    return false;
            }
        }
    }

    /// <summary>One saved card. <see cref="CardType"/> says which of <see cref="Poll"/> and <see cref="FunCard"/> is filled.</summary>
    public class SavedCardResponse
    {
        /// <summary>"poll" or "fun_card".</summary>
        public string CardType { get; set; } = string.Empty;
        public Guid CardId { get; set; }
        public DateTime SavedAt { get; set; }
        public PollResponse? Poll { get; set; }
        public FunCardResponse? FunCard { get; set; }
    }

    public class SavedCardStateResponse
    {
        public string CardType { get; set; } = string.Empty;
        public Guid CardId { get; set; }
        public bool Saved { get; set; }
    }

    public class SavedCardsPage
    {
        public List<SavedCardResponse> Items { get; set; } = [];
        public PaginationMeta Meta { get; set; } = new();
    }
}

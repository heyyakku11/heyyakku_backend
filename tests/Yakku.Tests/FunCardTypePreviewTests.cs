using Yakku.Application.Checks;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Checks.Services;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Checks.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

/// <summary>The fun card lists show a preview of at most 4 cards per type plus the total; "see all" returns every card.</summary>
public class FunCardTypePreviewTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task AppTypes_ShowAtMostFourCardsPerType_WithTheTotalCount()
    {
        var h = new Harness(fun: 6, emotional: 2);

        var types = await h.Fun.ListTypesAsync(null);

        var fun = Assert.Single(types, t => t.Name == "Just for fun");
        Assert.Equal(FunCardLimits.PreviewPerType, fun.Cards.Count);
        Assert.Equal(6, fun.TotalCount);
        var emotional = Assert.Single(types, t => t.Name == "Emotional");
        Assert.Equal(2, emotional.Cards.Count);
        Assert.Equal(2, emotional.TotalCount);
        Assert.Equal(h.FunTheme.Id, fun.TypeId);
    }

    [Fact]
    public async Task AppTypes_KeepTheOldestFirst_AndEachTypeStartsItsOwnGroup()
    {
        var h = new Harness(fun: 5, emotional: 5);

        var types = await h.Fun.ListTypesAsync(null);

        Assert.Equal(["Just for fun", "Emotional"], types.Select(t => t.Name));
        Assert.Equal(["Q1", "Q2", "Q3", "Q4"], types[0].Cards.Select(c => c.Question));
        Assert.All(types, t => Assert.All(t.Cards, c => Assert.Equal(t.Name, c.Theme.CheckType)));
    }

    [Fact]
    public async Task AppTypes_LeaveOutCardsTheCallerAlreadyMade_BeforeApplyingTheLimit()
    {
        var h = new Harness(fun: 6, emotional: 0);
        h.MarkMade(h.Cards.Where(c => c.CardThemeId == h.FunTheme.Id).Take(3));

        var fun = Assert.Single(await h.Fun.ListTypesAsync(UserId));

        // 3 left: all 3 shown and counted, not a half-empty preview of 4.
        Assert.Equal(3, fun.TotalCount);
        Assert.Equal(3, fun.Cards.Count);
        Assert.DoesNotContain(fun.Cards, c => c.Question is "Q1" or "Q2" or "Q3");
    }

    [Fact]
    public async Task AppLists_FlagTheCardsTheCallerSaved_AndNeverForSignedOutCallers()
    {
        var h = new Harness(fun: 3, emotional: 0);
        h.Saved.Ids.Add(h.Cards[1].Id);

        var signedIn = Assert.Single(await h.Fun.ListTypesAsync(UserId)).Cards;
        var all = await h.Fun.ListByTypeAsync(UserId, h.FunTheme.Id);
        var signedOut = Assert.Single(await h.Fun.ListTypesAsync(null)).Cards;

        Assert.Equal([false, true, false], signedIn.Select(c => c.IsSaved));
        Assert.Equal(1, all.Count(c => c.IsSaved));
        Assert.All(signedOut, c => Assert.False(c.IsSaved));
    }

    [Fact]
    public async Task AdminDelete_RemovesAnyFunCard_EvenOneUsersHaveMade_AndRefusesOtherKinds()
    {
        var h = new Harness(fun: 3, emotional: 0);
        var made = h.Cards[0];
        h.MarkMade([made]);
        var weekly = Check.Create(Yakku.Application.Checks.CheckWeek.Current(), "Weekly", "Q?", "#FFFFFF", ["A", "B"]);
        h.Cards.Add(weekly);

        await h.Admin.DeleteAsync(made.Id);

        Assert.DoesNotContain(h.Cards, c => c.Id == made.Id);
        // A weekly check id, or an unknown id, is not a fun card.
        var notFun = await Assert.ThrowsAsync<Yakku.Application.Common.Exceptions.AppException>(() => h.Admin.DeleteAsync(weekly.Id));
        var missing = await Assert.ThrowsAsync<Yakku.Application.Common.Exceptions.AppException>(() => h.Admin.DeleteAsync(Guid.NewGuid()));
        Assert.Equal(404, notFun.StatusCode);
        Assert.Equal(404, missing.StatusCode);
        Assert.Contains(h.Cards, c => c.Id == weekly.Id);
    }

    [Fact]
    public async Task SeeAll_ReturnsEveryCardOfThatTypeOnly()
    {
        var h = new Harness(fun: 6, emotional: 2);

        var all = await h.Fun.ListByTypeAsync(null, h.FunTheme.Id);
        var emotional = await h.Fun.ListByTypeAsync(null, h.EmotionalTheme.Id);

        Assert.Equal(6, all.Count);
        Assert.Equal(2, emotional.Count);
        Assert.Empty(await h.Fun.ListByTypeAsync(null, Guid.NewGuid()));
    }

    [Fact]
    public async Task AdminList_ShowsAtMostFourCardsPerType_WithTheTotal_AndSeeAllHasThemAll()
    {
        var h = new Harness(fun: 6, emotional: 1);

        var groups = await h.Admin.ListAsync();

        var fun = Assert.Single(groups, g => g.Theme.Heading == "Just for fun");
        Assert.Equal(4, fun.Cards.Count);
        Assert.Equal(6, fun.TotalCount);
        Assert.Equal(1, Assert.Single(groups, g => g.Theme.Heading == "Emotional").TotalCount);

        var seeAll = await h.Admin.GetByTypeAsync(h.FunTheme.Id);
        Assert.Equal(6, seeAll.Cards.Count);
        Assert.Equal(6, seeAll.TotalCount);
    }

    private sealed class Harness
    {
        public Harness(int fun, int emotional)
        {
            FunTheme = CardTheme.Create(CardThemeKind.Fun, "Just for fun", "#FFEFC2", "#6B4B00");
            EmotionalTheme = CardTheme.Create(CardThemeKind.Fun, "Emotional", "#86CCFF", "#0B3A66");

            var created = DateTime.UtcNow.AddHours(-10);
            for (var i = 1; i <= fun; i++)
            {
                Cards.Add(NewCard(FunTheme, $"Q{i}", created.AddMinutes(i)));
            }

            for (var i = 1; i <= emotional; i++)
            {
                Cards.Add(NewCard(EmotionalTheme, $"Q{i}", created.AddMinutes(100 + i)));
            }

            Repository = new FakeChecks(Cards);
            Fun = new FunCardService(Repository, null!, Saved);
            Admin = new AdminFunCardService(
                Repository,
                new FakeThemes([FunTheme, EmotionalTheme]),
                new SaveFunCardValidator(),
                new CreateCheckThemeValidator());
        }

        public FakeSaved Saved { get; } = new();
        public CardTheme FunTheme { get; }
        public CardTheme EmotionalTheme { get; }
        public List<Check> Cards { get; } = [];
        public FakeChecks Repository { get; }
        public FunCardService Fun { get; }
        public AdminFunCardService Admin { get; }

        public void MarkMade(IEnumerable<Check> cards)
        {
            foreach (var card in cards)
            {
                Repository.Used[card.Id] = UserCheck.Create(UserId, card.Id, $"code-{card.Id:N}"[..16]);
            }
        }

        private static Check NewCard(CardTheme theme, string question, DateTime createdAt)
        {
            var card = Check.CreateFun(theme.Name, question, theme.BackgroundColor, ["A", "B"], theme.Id);
            typeof(Check).GetProperty(nameof(Check.CardTheme))!.SetValue(card, theme);
            typeof(Check).GetProperty(nameof(Check.CreatedAt))!.SetValue(card, createdAt);
            return card;
        }
    }

    private sealed class FakeSaved : Yakku.Application.SavedCards.Interfaces.ISavedCardRepository
    {
        public HashSet<Guid> Ids { get; } = [];

        public Task<HashSet<Guid>> GetSavedCardIdsAsync(Guid userId, SavedCardType cardType, CancellationToken cancellationToken = default) =>
            Task.FromResult(cardType == SavedCardType.FunCard ? Ids : []);

        public Task<Check?> GetActiveFunCardAsync(Guid checkId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SavedCard?> GetAsync(Guid userId, SavedCardType cardType, Guid cardId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(SavedCard saved, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(SavedCard saved) => throw new NotSupportedException();
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SavedCard>> ListByUserAsync(Guid userId, DateTime? cursorSavedAt, Guid? cursorId, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeThemes(List<CardTheme> themes) : ICheckThemeRepository
    {
        public Task<List<CardTheme>> GetAllTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult(themes);

        public Task<List<CardTheme>> GetByKindAsync(CardThemeKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(themes.Where(t => t.Kind == kind).ToList());

        public Task<CardTheme?> GetTypeByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(themes.FirstOrDefault(t => t.Id == id));

        public Task<bool> TryAddTypeAsync(CardTheme type, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeChecks(List<Check> cards) : ICheckRepository
    {
        public Dictionary<Guid, UserCheck> Used { get; } = [];

        public Task<List<Check>> GetFunCardsAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
            Task.FromResult(cards.Where(c => c.Kind == CheckKind.Fun && (!activeOnly || c.IsActive)).OrderBy(c => c.CreatedAt).ToList());

        public Task<Dictionary<Guid, UserCheck>> GetUserChecksByCheckAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Used);

        public Task<List<Check>> GetForWeekAsync(DateOnly weekStart, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Check?> GetLiveByIdAsync(Guid id, DateOnly throughWeek, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<UserCheck>> GetUserChecksAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<UserCheck>> GetUserChecksPageAsync(Guid userId, DateTime? cursorCreatedAt, Guid? cursorId, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserCheck?> GetOwnedUserCheckForUpdateAsync(Guid userCheckId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteUserCheckAsync(UserCheck userCheck, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserCheck?> GetUserCheckByIdAsync(Guid userCheckId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserCheck?> GetUserCheckByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryAddUserCheckAsync(UserCheck userCheck, UserCheckVote vote, Func<CancellationToken, Task>? afterAdded = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddVoteAsync(UserCheckVote vote, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Check>> GetAllAsync(DateOnly? weekStart, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Check?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(cards.FirstOrDefault(c => c.Id == id));

        public Task DeleteWithUsageAsync(Check check, CancellationToken cancellationToken = default)
        {
            cards.Remove(check);
            return Task.CompletedTask;
        }
        public Task<bool> HasUserChecksAsync(Guid checkId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddCheckAsync(Check check, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveCheckAsync(Check check, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChecksAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

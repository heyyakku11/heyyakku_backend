using Yakku.Application.Common.Exceptions;
using Yakku.Application.SavedCards.DTOs;
using Yakku.Application.SavedCards.Interfaces;
using Yakku.Application.SavedCards.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

/// <summary>Saved polls and saved fun cards live in one list, told apart by their card type.</summary>
public class SavedCardServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Theory]
    [InlineData("poll", SavedCardType.Poll)]
    [InlineData("POLLS", SavedCardType.Poll)]
    [InlineData("fun_card", SavedCardType.FunCard)]
    [InlineData("fun-card", SavedCardType.FunCard)]
    [InlineData("FunCard", SavedCardType.FunCard)]
    public void CardTypes_AreParsedLeniently(string text, SavedCardType expected)
    {
        Assert.True(SavedCardTypes.TryParse(text, out var type));
        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("check")]
    [InlineData("personality")]
    [InlineData(null)]
    public void UnknownCardTypes_AreRejected(string? text)
    {
        Assert.False(SavedCardTypes.TryParse(text, out _));
    }

    [Fact]
    public void ACardsApiType_RoundTrips()
    {
        Assert.Equal("poll", SavedCardTypes.ToApi(SavedCardType.Poll));
        Assert.Equal("fun_card", SavedCardTypes.ToApi(SavedCardType.FunCard));
    }

    [Fact]
    public async Task SavingAPollAndAFunCard_PutsBothInOneList_WithTheirTypes()
    {
        var h = new Harness();
        var poll = h.OthersPoll();
        var fun = h.ActiveFunCard();

        await h.Service.SaveAsync(UserId, SavedCardType.Poll, poll.Id);
        await h.Service.SaveAsync(UserId, SavedCardType.FunCard, fun.Id);

        Assert.Equal(2, h.Store.Rows.Count);
        Assert.Contains(h.Store.Rows, r => r.CardType == SavedCardType.Poll && r.PollId == poll.Id && r.FunCardId is null);
        Assert.Contains(h.Store.Rows, r => r.CardType == SavedCardType.FunCard && r.FunCardId == fun.Id && r.PollId is null);
    }

    [Fact]
    public async Task Saving_IsIdempotent()
    {
        var h = new Harness();
        var poll = h.OthersPoll();

        var first = await h.Service.SaveAsync(UserId, SavedCardType.Poll, poll.Id);
        var second = await h.Service.SaveAsync(UserId, SavedCardType.Poll, poll.Id);

        Assert.True(first.Saved);
        Assert.True(second.Saved);
        Assert.Equal("poll", second.CardType);
        Assert.Single(h.Store.Rows);
    }

    [Fact]
    public async Task APollOfYourOwn_CannotBeSaved()
    {
        var h = new Harness();
        var own = h.OthersPoll(creator: UserId);

        var exception = await Assert.ThrowsAsync<AppException>(() => h.Service.SaveAsync(UserId, SavedCardType.Poll, own.Id));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(h.Store.Rows);
    }

    [Fact]
    public async Task UnknownPollsAndInactiveOrWeeklyChecks_AreNotFound()
    {
        var h = new Harness();

        var poll = await Assert.ThrowsAsync<AppException>(() => h.Service.SaveAsync(UserId, SavedCardType.Poll, Guid.NewGuid()));
        var fun = await Assert.ThrowsAsync<AppException>(() => h.Service.SaveAsync(UserId, SavedCardType.FunCard, Guid.NewGuid()));

        Assert.Equal(404, poll.StatusCode);
        Assert.Equal(404, fun.StatusCode);
    }

    [Fact]
    public async Task Unsaving_RemovesOnlyThatCardType_AndIsIdempotent()
    {
        var h = new Harness();
        var poll = h.OthersPoll();
        var fun = h.ActiveFunCard();
        await h.Service.SaveAsync(UserId, SavedCardType.Poll, poll.Id);
        await h.Service.SaveAsync(UserId, SavedCardType.FunCard, fun.Id);

        var removed = await h.Service.UnsaveAsync(UserId, SavedCardType.Poll, poll.Id);
        var again = await h.Service.UnsaveAsync(UserId, SavedCardType.Poll, poll.Id);

        Assert.False(removed.Saved);
        Assert.False(again.Saved);
        var left = Assert.Single(h.Store.Rows);
        Assert.Equal(SavedCardType.FunCard, left.CardType);
    }

    [Fact]
    public async Task ASavedPollList_ShowsMostRecentFirst_AndPagesByCursor()
    {
        var h = new Harness();
        var polls = Enumerable.Range(0, 12).Select(_ => h.OthersPoll()).ToList();
        foreach (var poll in polls)
        {
            await h.Service.SaveAsync(UserId, SavedCardType.Poll, poll.Id);
            await Task.Delay(2);
        }

        var first = await h.Service.ListAsync(UserId, null);
        var second = await h.Service.ListAsync(UserId, first.Meta.NextCursor);

        Assert.Equal(10, first.Items.Count);
        Assert.True(first.Meta.HasMore);
        Assert.Equal(2, second.Items.Count);
        Assert.False(second.Meta.HasMore);
        Assert.All(first.Items.Concat(second.Items), item =>
        {
            Assert.Equal("poll", item.CardType);
            Assert.NotNull(item.Poll);
            Assert.Null(item.FunCard);
        });
        // Newest save first: the last poll saved leads, the first saved comes last.
        Assert.Equal(polls[^1].Id, first.Items[0].CardId);
        Assert.Equal(polls[0].Id, second.Items[^1].CardId);
    }

    [Fact]
    public async Task OneUsersSaves_NeverAppearForAnother()
    {
        var h = new Harness();
        var poll = h.OthersPoll();
        await h.Service.SaveAsync(UserId, SavedCardType.Poll, poll.Id);

        var other = await h.Service.ListAsync(Guid.NewGuid(), null);

        Assert.Empty(other.Items);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Polls = new InMemoryPollRepository([]);
            Store.PollsById = id => Polls.Polls.FirstOrDefault(p => p.Id == id);
            Service = new SavedCardService(Store, Polls, null!);
        }

        public InMemorySavedCards Store { get; } = new();
        public InMemoryPollRepository Polls { get; }
        public SavedCardService Service { get; }

        public Poll OthersPoll(Guid? creator = null)
        {
            var poll = new Poll(creator ?? Guid.NewGuid(), "Which?", PollContentType.Question);
            poll.AddTextOption("A", 1);
            poll.AddTextOption("B", 2);
            poll.Publish();
            Polls.AddAsync(poll).Wait();
            return poll;
        }

        public Check ActiveFunCard()
        {
            var card = Check.CreateFun("Just for fun", "Q?", "#FFEFC2", ["A", "B"], Guid.NewGuid());
            Store.FunCards.Add(card);
            return card;
        }
    }
}

/// <summary>An in-memory saved_cards table.</summary>
internal sealed class InMemorySavedCards : ISavedCardRepository
{
    public List<SavedCard> Rows { get; } = [];
    public List<Check> FunCards { get; } = [];

    public Task<Check?> GetActiveFunCardAsync(Guid checkId, CancellationToken cancellationToken = default) =>
        Task.FromResult(FunCards.FirstOrDefault(c => c.Id == checkId && c.Kind == CheckKind.Fun && c.IsActive));

    public Task<SavedCard?> GetAsync(Guid userId, SavedCardType cardType, Guid cardId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.UserId == userId && r.CardType == cardType && r.CardId == cardId));

    public Task<HashSet<Guid>> GetSavedCardIdsAsync(Guid userId, SavedCardType cardType, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.Where(r => r.UserId == userId && r.CardType == cardType).Select(r => r.CardId).ToHashSet());

    public Task AddAsync(SavedCard saved, CancellationToken cancellationToken = default)
    {
        Rows.Add(saved);
        return Task.CompletedTask;
    }

    public void Remove(SavedCard saved) => Rows.Remove(saved);

    public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<IReadOnlyList<SavedCard>> ListByUserAsync(
        Guid userId, DateTime? cursorSavedAt, Guid? cursorId, int take, CancellationToken cancellationToken = default)
    {
        var query = Rows.Where(r => r.UserId == userId);
        if (cursorSavedAt is not null && cursorId is not null)
        {
            query = query.Where(r => r.SavedAt < cursorSavedAt || (r.SavedAt == cursorSavedAt && r.Id.CompareTo(cursorId.Value) < 0));
        }

        var page = query.OrderByDescending(r => r.SavedAt).ThenByDescending(r => r.Id).Take(take).ToList();
        // Like the database include: the poll is loaded with the row.
        foreach (var row in page.Where(r => r.CardType == SavedCardType.Poll))
        {
            typeof(SavedCard).GetProperty(nameof(SavedCard.Poll))!.SetValue(row, PollsById?.Invoke(row.PollId!.Value));
        }

        return Task.FromResult<IReadOnlyList<SavedCard>>(page);
    }

    public Func<Guid, Poll?>? PollsById { get; set; }
}

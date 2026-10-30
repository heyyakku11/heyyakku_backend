using Yakku.Application.AskBack;
using Yakku.Application.Common.Exceptions;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class AskBackDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private static AskBackConversation NewConversation(Guid? asker = null, Guid? voter = null)
    {
        return AskBackConversation.Create(
            ShareContentType.Poll, Guid.NewGuid(), Guid.NewGuid(), asker ?? Guid.NewGuid(), voter ?? Guid.NewGuid(), Now);
    }

    [Fact]
    public void Create_MakesAnActiveConversationWithExactlyTwoParticipants()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();

        var conversation = NewConversation(asker, voter);

        Assert.Equal(AskBackStatus.Active, conversation.Status);
        Assert.Equal(2, conversation.Participants.Count);
        Assert.Contains(conversation.Participants, p => p.UserId == asker);
        Assert.Contains(conversation.Participants, p => p.UserId == voter);
        Assert.True(conversation.CanSend);
        Assert.Equal(Now, conversation.LastActivityAt);
        Assert.Null(conversation.LastMessageAt);
    }

    [Fact]
    public void Create_RefusesAConversationWithOneself()
    {
        var user = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => NewConversation(user, user));
    }

    [Fact]
    public void IsParticipant_IsTrueOnlyForTheTwoUsers()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);

        Assert.True(conversation.IsParticipant(asker));
        Assert.True(conversation.IsParticipant(voter));
        Assert.False(conversation.IsParticipant(Guid.NewGuid()));
        Assert.False(conversation.IsParticipant(Guid.Empty));
        Assert.Equal(voter, conversation.OtherParticipant(asker));
        Assert.Equal(asker, conversation.OtherParticipant(voter));
    }

    [Fact]
    public void Close_ByEitherParticipant_MakesItReadOnly_AndOnlyTheAskerCanReopen()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);

        Assert.True(conversation.TryClose(Now));
        Assert.Equal(AskBackStatus.Closed, conversation.Status);
        Assert.False(conversation.CanSend);
        Assert.False(conversation.TryClose(Now));

        Assert.False(conversation.TryReopen(voter));
        Assert.True(conversation.TryReopen(asker));
        Assert.Equal(AskBackStatus.Active, conversation.Status);
        Assert.Null(conversation.ClosedAt);
        Assert.True(conversation.CanSend);
    }

    [Fact]
    public void Block_IsSeparateFromClose_AndOnlyTheBlockerCanLiftIt()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);

        Assert.True(conversation.TryBlock(voter, Now));
        Assert.Equal(AskBackStatus.Blocked, conversation.Status);
        Assert.Equal(voter, conversation.BlockedByUserId);
        Assert.False(conversation.CanSend);

        // The asker cannot reopen a block, close it, or lift it.
        Assert.False(conversation.TryReopen(asker));
        Assert.False(conversation.TryClose(Now));
        Assert.False(conversation.TryUnblock(asker));
        Assert.False(conversation.TryBlock(asker, Now));

        Assert.True(conversation.TryUnblock(voter));
        Assert.Equal(AskBackStatus.Active, conversation.Status);
        Assert.Null(conversation.BlockedByUserId);
    }

    [Fact]
    public void Block_ByAStranger_IsRefused()
    {
        var conversation = NewConversation();

        Assert.False(conversation.TryBlock(Guid.NewGuid(), Now));
        Assert.Equal(AskBackStatus.Active, conversation.Status);
    }

    [Fact]
    public void Block_OfAClosedConversation_WinsOverClosing()
    {
        var asker = Guid.NewGuid();
        var conversation = NewConversation(asker);
        conversation.TryClose(Now);

        Assert.True(conversation.TryBlock(asker, Now.AddMinutes(1)));

        Assert.Equal(AskBackStatus.Blocked, conversation.Status);
        Assert.True(conversation.TryUnblock(asker));
        Assert.Equal(AskBackStatus.Active, conversation.Status);
    }

    [Fact]
    public void RecordMessage_MovesTheListSortKey()
    {
        var conversation = NewConversation();
        var later = Now.AddMinutes(5);

        conversation.RecordMessage(later);

        Assert.Equal(later, conversation.LastMessageAt);
        Assert.Equal(later, conversation.LastActivityAt);
    }

    [Fact]
    public void Participant_ReadPosition_OnlyMovesForward()
    {
        var conversation = NewConversation();
        var participant = conversation.Participants.First();
        var first = (Id: Guid.NewGuid(), At: Now);
        var second = (Id: Guid.NewGuid(), At: Now.AddSeconds(1));

        Assert.True(participant.TryAdvanceRead(second.Id, second.At, Now));
        Assert.False(participant.TryAdvanceRead(first.Id, first.At, Now));
        Assert.False(participant.TryAdvanceRead(second.Id, second.At, Now));

        Assert.Equal(second.Id, participant.LastReadMessageId);
        Assert.True(participant.TryAdvanceRead(Guid.NewGuid(), second.At.AddSeconds(1), Now));
    }

    [Fact]
    public void Participant_ReadPosition_BreaksTiesByIdInUuidOrder()
    {
        var conversation = NewConversation();
        var participant = conversation.Participants.First();
        var lower = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higher = Guid.Parse("00000000-0000-0000-0000-000000000002");

        Assert.True(participant.TryAdvanceRead(lower, Now, Now));
        Assert.True(participant.TryAdvanceRead(higher, Now, Now));
        Assert.False(participant.TryAdvanceRead(lower, Now, Now));
    }

    [Fact]
    public void Cursor_RoundTrips_AndRejectsGarbage()
    {
        var id = Guid.NewGuid();

        var decoded = AskBackCursor.TryDecode(AskBackCursor.Encode(Now, id));

        Assert.Equal((Now, id), decoded);
        Assert.Null(AskBackCursor.TryDecode(null));
        Assert.Null(AskBackCursor.TryDecode("  "));
        Assert.Throws<AppException>(() => AskBackCursor.TryDecode("not-base64!"));
        Assert.Throws<AppException>(() => AskBackCursor.TryDecode(Convert.ToBase64String("nope"u8.ToArray())));
    }
}

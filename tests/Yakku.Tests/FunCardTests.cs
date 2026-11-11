using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Checks.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

/// <summary>Just-for-fun cards: no live week, own theme kind, simple admin validation. No database needed.</summary>
public class FunCardTests
{
    private static SaveFunCardRequest Valid() => new()
    {
        Question = "Which silly superpower would you pick?",
        Options = ["Talking to pigeons", "Perfect snacks"]
    };

    [Fact]
    public void AFunCard_HasNoLiveWeek_AndUsesItsTheme()
    {
        var theme = CardTheme.Create(CardThemeKind.Fun, "Just for fun", "#FFEFC2", "#6B4B00", "movies, jail, silly ones");

        var card = Check.CreateFun(theme.Name, "Q?", theme.BackgroundColor, ["A", "B"], theme.Id);

        Assert.Equal(CheckKind.Fun, card.Kind);
        Assert.Null(card.LiveWeekStart);
        Assert.True(card.IsActive);
        Assert.Equal(theme.Id, card.CardThemeId);
        Assert.Equal(2, card.Options.Count);
    }

    [Fact]
    public void AWeeklyCheck_StillHasALiveWeek()
    {
        var check = Check.Create(Yakku.Application.Checks.CheckWeek.Current(), "T", "Q?", "#FFFFFF", "", ["A", "B"]);

        Assert.Equal(CheckKind.Weekly, check.Kind);
        Assert.NotNull(check.LiveWeekStart);
    }

    [Fact]
    public void TheThemeConfig_CarriesTheTag_AndNoSticker()
    {
        var theme = CardTheme.Create(CardThemeKind.Fun, "Just for fun", "#FFEFC2", "#6B4B00", "movies, jail, silly ones");

        var config = CheckThemeMapper.ConfigOf(theme);

        Assert.Equal("movies, jail, silly ones", config.Tag);
        Assert.Equal("#6B4B00", config.LabelColor);
        Assert.DoesNotContain(typeof(CheckThemeConfig).GetProperties(), p => p.Name.Contains("Sticker"));
    }

    [Fact]
    public void TheValidator_AcceptsAGoodCard_AndRejectsBadOnes()
    {
        var validator = new SaveFunCardValidator();

        Assert.True(validator.Validate(Valid()).IsValid);

        var tooFew = Valid();
        tooFew.Options = ["Only one"];
        Assert.False(validator.Validate(tooFew).IsValid);

        var tooMany = Valid();
        tooMany.Options = ["A", "B", "C", "D"];
        Assert.False(validator.Validate(tooMany).IsValid);

        var duplicate = Valid();
        duplicate.Options = ["Same", "same"];
        Assert.False(validator.Validate(duplicate).IsValid);

        var noQuestion = Valid();
        noQuestion.Question = " ";
        Assert.False(validator.Validate(noQuestion).IsValid);
    }
}

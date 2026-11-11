using FluentValidation;
using Yakku.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Yakku.API.Controllers;
using Yakku.Application.Checks;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Services;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Checks.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

internal static class ThemeSamples
{
    public static CreateCheckThemeRequest Request(string name = "Vibe Check") => new()
    {
        Name = name,
        BackgroundColor = "#ecE3ff",
        LabelColor = "#4a3a8c",
        Tag = "Read the room"
    };
}

/// <summary>Theme definition and resolution rules that need no database.</summary>
public class CheckThemeUnitTests
{
    private sealed class FakeThemeRepository : ICheckThemeRepository
    {
        public readonly List<CardTheme> Types = new();

        public Task<List<CardTheme>> GetAllTypesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Types.Where(t => t.Kind == CardThemeKind.Check).OrderBy(t => t.Name).ToList());

        public Task<List<CardTheme>> GetByKindAsync(CardThemeKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(Types.Where(t => t.Kind == kind).OrderBy(t => t.Name).ToList());

        public Task<CardTheme?> GetTypeByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Types.FirstOrDefault(t => t.Id == id && t.Kind == CardThemeKind.Check));

        public Task<bool> TryAddTypeAsync(CardTheme type, CancellationToken cancellationToken = default)
        {
            if (Types.Any(t => string.Equals(t.Name, type.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(false);
            }

            Types.Add(type);
            return Task.FromResult(true);
        }
    }

    private static CheckThemeService NewService(FakeThemeRepository? repo = null) =>
        new(repo ?? new FakeThemeRepository(), new CreateCheckThemeValidator());

    [Fact]
    public async Task Create_StoresOnlyTheFourAdminFields_AndListReturnsThem()
    {
        var service = NewService();

        var created = await service.CreateAsync(ThemeSamples.Request());
        var listed = Assert.Single(await service.ListAsync());

        Assert.Equal(created.Id, listed.Id);
        Assert.Equal("Vibe Check", listed.Name);
        Assert.Equal("#ECE3FF", listed.BackgroundColor);
        Assert.Equal("#4A3A8C", listed.LabelColor);
    }

    [Fact]
    public async Task Create_RejectsADuplicateName_IgnoringCase_WithConflict()
    {
        var service = NewService();
        await service.CreateAsync(ThemeSamples.Request("Vibe Check"));

        var error = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(ThemeSamples.Request("vibe check")));

        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsInvalidInput()
    {
        var service = NewService();

        var noName = ThemeSamples.Request();
        noName.Name = " ";
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(noName));

        var badBackground = ThemeSamples.Request("B");
        badBackground.BackgroundColor = "pink";
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(badBackground));

        var badLabel = ThemeSamples.Request("E");
        badLabel.LabelColor = "#12345";
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(badLabel));

        var longSticker = ThemeSamples.Request("D");
        longSticker.Tag = new string('x', 101);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(longSticker));
    }

    [Fact]
    public async Task Resolve_UsesTheNameAsTheHeading_AndRejectsUnknownIds()
    {
        var service = NewService();
        var created = await service.CreateAsync(ThemeSamples.Request("Confidence Check"));

        var resolved = await service.ResolveForNewCheckAsync(created.Id);

        Assert.Equal(created.Id, resolved.CardThemeId);
        Assert.Equal("Confidence Check", resolved.Config.Heading);
        Assert.Equal("#ECE3FF", resolved.Config.BackgroundColor);
        Assert.Equal("#4A3A8C", resolved.Config.LabelColor);
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(() => service.ResolveForNewCheckAsync(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public void LegacyChecks_RenderFromTheirOwnFields()
    {
        var legacy = Check.Create(CheckWeek.Current(), "Loyalty Check", "Q?", "#FFEFC2", ["A", "B"]);
        var theme = CheckThemeMapper.ToResponse(legacy);

        Assert.True(theme.IsLegacy);
        Assert.Null(theme.CheckTypeId);
        Assert.Equal("Loyalty Check", theme.Config.Heading);
        Assert.Equal("#FFEFC2", theme.Config.BackgroundColor);
    }

    [Fact]
    public void SaveCheckRequest_HasNoVisualOrThemeProperties()
    {
        var names = typeof(SaveCheckRequest).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("Title", names);
        Assert.DoesNotContain("BackgroundColor", names);
        Assert.DoesNotContain("Sticker", names);
        Assert.DoesNotContain("Theme", names);
        Assert.Contains("CheckTypeId", names);
    }

    [Fact]
    public void ThemeRequest_ExposesOnlyNameBackgroundLabelColorAndTag()
    {
        var names = typeof(CreateCheckThemeRequest).GetProperties().Select(p => p.Name).OrderBy(n => n).ToList();

        Assert.Equal(["BackgroundColor", "LabelColor", "Name", "Tag"], names);
    }

    [Fact]
    public void ThemeRoutes_AreAdminOnly_AndOnlyListAndCreateExist()
    {
        var controller = typeof(AdminCheckThemesController);

        var authorize = controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();
        Assert.Equal("Admin", authorize.Policy);
        Assert.Empty(controller.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));

        var actions = controller.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(m => m.Name).OrderBy(n => n).ToList();
        Assert.Equal(["Create", "List"], actions);
    }
}

[Collection(PostgresCollection.Name)]
public class CheckThemeIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public CheckThemeIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static CheckThemeService NewThemes(YakkuDbContext context) =>
        new(new CheckThemeRepository(context), new CreateCheckThemeValidator());

    private static AdminCheckService NewAdmin(YakkuDbContext context) =>
        new(new CheckRepository(context), new SaveCheckValidator(), NewThemes(context));

    private static string NewName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..24];

    [SkippableFact]
    public async Task Admin_CreatesATheme_ThenACheckUsesIt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var name = NewName("Made");

        var theme = await NewThemes(context).CreateAsync(ThemeSamples.Request(name));
        var check = await NewAdmin(context).CreateAsync(
            CheckWeekTests.NewRequest(CheckWeek.Current().AddDays(28), "mine", theme.Id));

        Assert.Contains(await NewThemes(context).ListAsync(), t => t.Id == theme.Id && t.Name == name);
        Assert.Equal(theme.Id, check.CheckTypeId);
        Assert.Equal(name, check.Title);
        Assert.Equal(name, check.Theme.Config.Heading);
        Assert.Equal("#ECE3FF", check.BackgroundColor);
        Assert.False(check.Theme.IsLegacy);
        var stored = await context.Checks.AsNoTracking().SingleAsync(c => c.Id == check.Id);
        Assert.Equal(theme.Id, stored.CardThemeId);
    }

    [SkippableFact]
    public async Task CreatingADuplicateName_IsAConflict()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var name = NewName("Dup");
        await NewThemes(context).CreateAsync(ThemeSamples.Request(name));

        var error = await Assert.ThrowsAsync<AppException>(
            () => NewThemes(context).CreateAsync(ThemeSamples.Request(name.ToUpperInvariant())));

        Assert.Equal(409, error.StatusCode);
    }

    [SkippableFact]
    public async Task Create_RejectsMissingAndUnknownTypes_AndPersistsNothing()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var admin = NewAdmin(context);
        var week = CheckWeek.Current().AddDays(35);
        var before = await context.Checks.CountAsync();

        var missing = CheckWeekTests.NewRequest(week, "t");
        missing.CheckTypeId = null;
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(() => admin.CreateAsync(missing))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(
            () => admin.CreateAsync(CheckWeekTests.NewRequest(week, "t", Guid.NewGuid())))).StatusCode);

        // An invalid request (4 options) fails before anything is written.
        var tooMany = CheckWeekTests.NewRequest(week, "t");
        tooMany.Options = ["A", "B", "C", "D"];
        await Assert.ThrowsAsync<ValidationException>(() => admin.CreateAsync(tooMany));

        Assert.Equal(before, await context.Checks.CountAsync());
    }

    [SkippableFact]
    public async Task ThemeEdits_ReachExistingChecksOfThatType()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var week = CheckWeek.Current().AddDays(42);
        var theme = await NewThemes(context).CreateAsync(ThemeSamples.Request(NewName("Edit")));
        var created = await NewAdmin(context).CreateAsync(CheckWeekTests.NewRequest(week, "first", theme.Id));

        // One theme per type: changing it changes how every check of that type renders.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE card_themes SET background_color = '#112233', name = 'Renamed' WHERE id = {theme.Id}");

        await using var fresh = _fixture.CreateContext();
        var listed = (await NewAdmin(fresh).ListAsync(week)).Single(c => c.Id == created.Id);
        Assert.Equal("Renamed", listed.Theme.Config.Heading);
        Assert.Equal("#112233", listed.Theme.Config.BackgroundColor);
    }

    [SkippableFact]
    public async Task Database_RejectsBadColorsBlankNamesAndDuplicateNames()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        await using var badColor = _fixture.CreateContext();
        badColor.CardThemes.Add(CardTheme.Create(CardThemeKind.Check, NewName("Bad"), "pink", "#000000"));
        await Assert.ThrowsAsync<DbUpdateException>(() => badColor.SaveChangesAsync());

        await using var blank = _fixture.CreateContext();
        blank.CardThemes.Add(CardTheme.Create(CardThemeKind.Check, "   ", "#FFFFFF", "#000000"));
        await Assert.ThrowsAsync<DbUpdateException>(() => blank.SaveChangesAsync());

        var name = NewName("Same");
        await using var first = _fixture.CreateContext();
        first.CardThemes.Add(CardTheme.Create(CardThemeKind.Check, name, "#FFFFFF", "#000000"));
        await first.SaveChangesAsync();
        await using var second = _fixture.CreateContext();
        second.CardThemes.Add(CardTheme.Create(CardThemeKind.Check, name, "#FFFFFF", "#000000"));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }
}

public class DatabaseSchemaErrorTests
{
    [Fact]
    public void AMissingTableOrColumn_IsRecognized_EvenWhenWrapped()
    {
        var missingTable = new Npgsql.PostgresException("relation \"card_themes\" does not exist", "ERROR", "ERROR", "42P01");
        var missingColumn = new Npgsql.PostgresException("column does not exist", "ERROR", "ERROR", "42703");

        Assert.True(Yakku.API.Middleware.DatabaseSchemaErrors.IsSchemaMismatch(missingTable));
        Assert.True(Yakku.API.Middleware.DatabaseSchemaErrors.IsSchemaMismatch(missingColumn));
        Assert.True(Yakku.API.Middleware.DatabaseSchemaErrors.IsSchemaMismatch(new InvalidOperationException("wrapped", missingTable)));
    }

    [Fact]
    public void OtherErrors_AreNotTreatedAsSchemaMismatch()
    {
        var uniqueViolation = new Npgsql.PostgresException("duplicate key", "ERROR", "ERROR", "23505");

        Assert.False(Yakku.API.Middleware.DatabaseSchemaErrors.IsSchemaMismatch(uniqueViolation));
        Assert.False(Yakku.API.Middleware.DatabaseSchemaErrors.IsSchemaMismatch(new InvalidOperationException("x")));
        Assert.False(Yakku.API.Middleware.DatabaseSchemaErrors.IsSchemaMismatch(null));
    }
}

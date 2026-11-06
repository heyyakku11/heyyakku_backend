namespace Yakku.Tests;

/// <summary>
/// Tests think of traits as numbers 1 to 10 (their display order). These map them to the real GUIDs: trait n is
/// b2e4d1a0-0000-4000-8000-00000000000n, the same ids the migration and PersonalityTraitIds use. Any other number
/// (for example 999) gives an id that is not a trait.
/// </summary>
internal static class PersonalityTestIds
{
    public static Guid T(int number) => new($"b2e4d1a0-0000-4000-8000-{number:D12}");

    public static int N(Guid id) => int.Parse(id.ToString()[^12..]);
}

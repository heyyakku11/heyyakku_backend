using System.Security.Cryptography;

namespace Yakku.Application.Users.AnonymousNames
{
    /// <summary>
    /// How a name is assembled from the word categories. Change the categories or the join here only; callers
    /// just ask <see cref="Generate"/> for a candidate.
    /// </summary>
    public static class AnonymousNameFormat
    {
        /// <summary>The longest name this format can produce, so it always fits the column.</summary>
        public static int MaxLength =>
            Longest(AnonymousNameWords.Adjectives) + Longest(AnonymousNameWords.Animals) + Longest(AnonymousNameWords.NatureAndObjects);

        public static string Generate()
        {
            return Pick(AnonymousNameWords.Adjectives) + Pick(AnonymousNameWords.Animals) + Pick(AnonymousNameWords.NatureAndObjects);
        }

        // RandomNumberGenerator is cryptographically secure and unbiased for a bounded range.
        private static string Pick(IReadOnlyList<string> words) => words[RandomNumberGenerator.GetInt32(words.Count)];

        private static int Longest(IReadOnlyList<string> words) => words.Max(word => word.Length);
    }
}

namespace Yakku.Application.PushNotifications
{
    /// <summary>
    /// Inbox wording for a card's votes and its close. The activity list shows these titles and bodies as stored.
    /// </summary>
    public static class CardActivityCopy
    {
        public const string ResultReadyTitle = "Your result is ready";
        public const string PersonalityTitle = "Who you are is in";
        public const string ClosedTitle = "Closed";

        /// <summary>
        /// A vote from someone other than the owner. Below the reveal threshold it is the live progress line.
        /// At and above the threshold it is the result-ready line, for a poll, a fun card and a weekly check alike.
        /// </summary>
        public static (string Title, string Body) ForVote(
            string question,
            int friendVotes,
            int threshold,
            DateTime? expiresAt,
            DateTime utcNow)
        {
            if (friendVotes >= threshold)
            {
                return (ResultReadyTitle, ResultReady(friendVotes, question));
            }

            var remaining = threshold - friendVotes;
            if (remaining < 1)
            {
                remaining = 1;
            }

            var need = remaining == 1 ? "One more vote" : $"{remaining} more votes";
            var quoted = Quote(question);
            var left = TimeLeft(expiresAt, utcNow);
            var body = left is null
                ? $"{need} on {quoted}."
                : $"{need} on {quoted} Closes in {left}.";
            return ($"{friendVotes} of {threshold} votes", Fit(body));
        }

        /// <summary>
        /// The card stopped taking votes. When enough friends answered, the line points at the final result.
        /// Otherwise it says the card closed short of a result.
        /// </summary>
        public static (string Title, string Body) ForClosed(string question, int friendVotes, int threshold)
        {
            var quoted = Quote(question);
            if (friendVotes >= threshold)
            {
                return (ClosedTitle, Fit($"{quoted} view final result."));
            }

            var votesWord = friendVotes == 1 ? "vote" : "votes";
            return (ClosedTitle, Fit($"{quoted} closed with {friendVotes} {votesWord}. Not enough to show a result."));
        }

        /// <summary>The personality card just reached its last vote and the top three traits can be shown.</summary>
        public static (string Title, string Body) ForPersonality(int friendVotes)
        {
            return (PersonalityTitle, Fit($"{Friends(friendVotes)} described you. Your top 3 are ready."));
        }

        private static string ResultReady(int friendVotes, string question)
        {
            return Fit($"{Friends(friendVotes)} answered {Quote(question)} See if they agree with you.");
        }

        private static string Friends(int count) => count == 1 ? "1 friend" : $"{count} friends";

        /// <summary>"2h", "18h", "1d", "5m". Null when the card has no end, so the line leaves "Closes in" out.</summary>
        public static string? TimeLeft(DateTime? expiresAt, DateTime utcNow)
        {
            if (expiresAt is null)
            {
                return null;
            }

            var left = expiresAt.Value - utcNow;
            if (left <= TimeSpan.Zero)
            {
                return "0m";
            }

            if (left.TotalDays >= 1)
            {
                return $"{(int)left.TotalDays}d";
            }

            if (left.TotalHours >= 1)
            {
                return $"{(int)left.TotalHours}h";
            }

            var minutes = (int)Math.Ceiling(left.TotalMinutes);
            return $"{Math.Max(minutes, 1)}m";
        }

        private static string Quote(string question)
        {
            var text = (question ?? string.Empty).Trim();
            if (text.Length > 160)
            {
                text = text[..157] + "...";
            }

            return $"\"{text}\"";
        }

        private static string Fit(string body) => body.Length <= 1000 ? body : body[..1000];
    }
}

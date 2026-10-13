namespace Yakku.Application.Polls
{
    public static class MediaFailure
    {
        public static string Sanitize(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return "Upload failed.";
            }

            var characters = reason.Where(character => !char.IsControl(character)).ToArray();
            var cleaned = new string(characters).Trim();
            if (cleaned.Length == 0)
            {
                return "Upload failed.";
            }

            return cleaned.Length <= 300 ? cleaned : cleaned[..300];
        }
    }
}

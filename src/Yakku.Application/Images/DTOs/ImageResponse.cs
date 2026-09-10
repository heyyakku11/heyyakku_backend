namespace Yakku.Application.Images.DTOs
{
    public class ImageResponse
    {
        public Guid Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string SecureUrl { get; set; } = string.Empty;
    }
}

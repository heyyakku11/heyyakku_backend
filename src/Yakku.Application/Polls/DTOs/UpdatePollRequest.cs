namespace Yakku.Application.Polls.DTOs
{
    public class UpdatePollRequest
    {
        public string? Question { get; set; }


        public DateTime? ExpiresAt { get; set; }

        public List<CreatePollOptionRequest>? Options { get; set; }

        /// <summary>
        /// Tri-state media update: omitted keeps current attachment; null clears draft media;
        /// a session id attaches or replaces media.
        /// </summary>
        public Guid? MediaUploadSessionId
        {
            get => _mediaUploadSessionId;
            set
            {
                MediaUploadSessionIdSpecified = true;
                _mediaUploadSessionId = value;
            }
        }

        [global::System.Text.Json.Serialization.JsonIgnore]
        public bool MediaUploadSessionIdSpecified { get; private set; }

        private Guid? _mediaUploadSessionId;
    }
}

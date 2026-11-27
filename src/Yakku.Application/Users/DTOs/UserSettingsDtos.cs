namespace Yakku.Application.Users.DTOs
{
    /// <summary>The signed-in user's account-level settings.</summary>
    public class UserSettingsResponse
    {
        /// <summary>Whether people the user answers may ask them back. Independent of push notifications.</summary>
        public bool AllowAskBack { get; set; } = true;
    }
}

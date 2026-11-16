namespace Yakku.Domain.Enums
{
    /// <summary>Coarse class of device a guest was created from, derived from the User-Agent.</summary>
    public enum GuestDeviceType
    {
        Unknown = 0,
        Desktop = 1,
        Mobile = 2,
        Tablet = 3,
        Bot = 4
    }
}

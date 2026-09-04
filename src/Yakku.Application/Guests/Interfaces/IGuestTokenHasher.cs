namespace Yakku.Application.Guests.Interfaces
{
    public interface IGuestTokenHasher
    {
        string Hash(string rawToken);
    }
}

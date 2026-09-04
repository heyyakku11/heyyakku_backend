using Yakku.Application.Guests.Services;
using Xunit;

namespace Yakku.Application.Tests.Guests;

public class GuestTokenHasherTests
{
    [Fact]
    public void Hash_IsSha256HexAndNotTheRawToken()
    {
        var hasher = new GuestTokenHasher();
        var generator = new GuestTokenGenerator();
        var raw = generator.Generate();

        var hash = hasher.Hash(raw);

        Assert.NotEqual(raw, hash);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, hasher.Hash(raw));
        Assert.Equal(64, raw.Length);
    }
}

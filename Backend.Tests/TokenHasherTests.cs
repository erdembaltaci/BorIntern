using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class TokenHasherTests
{
    [Fact]
    public void Hash_AyniGirdiAyniOzeti_FarkliGirdiFarkliOzeti_Uretir()
    {
        Assert.Equal(TokenHasher.Hash("token-a"), TokenHasher.Hash("token-a"));
        Assert.NotEqual(TokenHasher.Hash("token-a"), TokenHasher.Hash("token-b"));
    }

    [Fact]
    public void Hash_HamTokendenFarklidirVeSabitUzunluktadir()
    {
        string hash = TokenHasher.Hash("ham-token");

        Assert.NotEqual("ham-token", hash);
        Assert.Equal(44, hash.Length); // SHA-256 = 32 bayt = Base64'te 44 karakter
    }
}

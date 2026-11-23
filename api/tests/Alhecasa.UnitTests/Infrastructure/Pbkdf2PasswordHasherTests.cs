using Alhecasa.Infrastructure.Identity;

namespace Alhecasa.UnitTests.Infrastructure;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerify_WithSamePassword_Succeeds()
    {
        var hash = _hasher.Hash("Correct-Horse-9");

        Assert.True(_hasher.Verify("Correct-Horse-9", hash));
    }

    [Fact]
    public void Verify_WithDifferentPassword_Fails()
    {
        var hash = _hasher.Hash("Correct-Horse-9");

        Assert.False(_hasher.Verify("Correct-Horse-8", hash));
    }

    [Fact]
    public void Hash_UsesRandomSalt_SoSamePasswordProducesDifferentHashes()
    {
        Assert.NotEqual(_hasher.Hash("Correct-Horse-9"), _hasher.Hash("Correct-Horse-9"));
    }

    [Fact]
    public void Hash_HasDocumentedFormat()
    {
        var parts = _hasher.Hash("Correct-Horse-9").Split('$');

        Assert.Equal(5, parts.Length);
        Assert.Equal("PBKDF2", parts[0]);
        Assert.Equal("SHA512", parts[1]);
        Assert.Equal("210000", parts[2]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2$SHA512$210000$@@@$@@@")]
    [InlineData("PBKDF2$SHA512$99999999$AAAA$AAAA")]
    [InlineData("OTHER$SHA512$210000$AAAA$AAAA")]
    public void Verify_WithMalformedHash_ReturnsFalseWithoutThrowing(string malformed)
    {
        Assert.False(_hasher.Verify("Correct-Horse-9", malformed));
    }

    [Fact]
    public void VerifyDummy_DoesNotThrow()
    {
        var exception = Record.Exception(() => _hasher.VerifyDummy("anything"));

        Assert.Null(exception);
    }
}

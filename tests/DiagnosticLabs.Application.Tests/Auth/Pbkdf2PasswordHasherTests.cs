using System.Security.Cryptography;
using System.Text;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Infrastructure.Security;

namespace DiagnosticLabs.Application.Tests.Auth;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = _hasher.Hash("s3cret!");

        Assert.Equal(PasswordVerification.Success, _hasher.Verify("s3cret!", hash));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        var hash = _hasher.Hash("s3cret!");

        Assert.Equal(PasswordVerification.Failed, _hasher.Verify("other", hash));
    }

    [Fact]
    public void Same_password_produces_different_hashes()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Fact]
    public void Legacy_sha1_hash_verifies_and_requests_rehash()
    {
        var legacy = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes("old-pass")));

        Assert.Equal(PasswordVerification.SuccessRehashNeeded, _hasher.Verify("old-pass", legacy));
        Assert.Equal(PasswordVerification.Failed, _hasher.Verify("nope", legacy));
    }

    [Fact]
    public void Legacy_sha1_hash_is_accepted_in_lower_case()
    {
        var legacy = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes("old-pass"))).ToLowerInvariant();

        Assert.Equal(PasswordVerification.SuccessRehashNeeded, _hasher.Verify("old-pass", legacy));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("v1$abc$def$ghi")]
    [InlineData("v1$600000$not-base64$also-not")]
    public void Unrecognised_stored_value_fails(string stored)
    {
        Assert.Equal(PasswordVerification.Failed, _hasher.Verify("anything", stored));
    }
}

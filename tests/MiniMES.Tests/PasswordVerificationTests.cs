using Microsoft.AspNetCore.Identity;
using MiniMES.Web.Models;
using MiniMES.Web.Security;
using Xunit;

namespace MiniMES.Tests;

public sealed class PasswordVerificationTests
{
    [Theory]
    [InlineData("not-a-base64-hash!")]
    [InlineData("")]
    [InlineData("AQ==")]
    public void MalformedHashIsRejectedWithoutExceptionOrPlaintextFallback(string storedHash)
    {
        var hasher = new PasswordHasher<UserAccount>();
        Assert.Equal(PasswordVerificationResult.Failed,
            PasswordVerification.Verify(hasher, new UserAccount(), storedHash, storedHash));
    }

    [Fact]
    public void GeneratedHashAcceptsCorrectPasswordAndRejectsWrongPassword()
    {
        var hasher = new PasswordHasher<UserAccount>();
        var user = new UserAccount();
        var password = Guid.NewGuid().ToString();
        var hash = hasher.HashPassword(user, password);
        Assert.NotEqual(password, hash);
        Assert.Equal(PasswordVerificationResult.Success, PasswordVerification.Verify(hasher, user, hash, password));
        Assert.Equal(PasswordVerificationResult.Failed, PasswordVerification.Verify(hasher, user, hash, "incorrect-password"));
    }
}

using Microsoft.AspNetCore.Identity;
using MiniMES.Web.Models;

namespace MiniMES.Web.Security;

public static class PasswordVerification
{
    public static PasswordVerificationResult Verify(PasswordHasher<UserAccount> hasher,
        UserAccount user, string storedHash, string password)
    {
        try
        {
            return hasher.VerifyHashedPassword(user, storedHash, password);
        }
        catch (FormatException)
        {
            // Dữ liệu nhập tay không phải Base64 hợp lệ: từ chối, không so sánh plaintext.
            return PasswordVerificationResult.Failed;
        }
    }
}

using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace Fido2Identity;

public class Fido2UserTwoFactorTokenProvider : IUserTwoFactorTokenProvider<AppUser>
{
    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<AppUser> manager, AppUser user)
    {
        return Task.FromResult(true);
    }

    public Task<string> GenerateAsync(string purpose, UserManager<AppUser> manager, AppUser user)
    {
        return Task.FromResult("fido2");
    }

    public Task<bool> ValidateAsync(string purpose, string token, UserManager<AppUser> manager, AppUser user)
    {
        return Task.FromResult(true);
    }
}
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Timinute.Server.Models;

namespace Timinute.Server.Areas.Identity
{
    public class AppSingInManager : SignInManager<ApplicationUser>
    {
        public AppSingInManager(UserManager<ApplicationUser> userManager, IHttpContextAccessor contextAccessor, IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory, IOptions<IdentityOptions> optionsAccessor, ILogger<SignInManager<ApplicationUser>> logger, IAuthenticationSchemeProvider schemes, IUserConfirmation<ApplicationUser> confirmation)
            : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
        {
        }

        public override async Task<bool> CanSignInAsync(ApplicationUser applicationUser)
        {
            if (applicationUser.LockoutEnabled && applicationUser.LockoutEnd != null)
            {
                Logger.LogError(0, "ApplicationUser {userId} cannot sign in because is not enabled.", await UserManager.GetUserIdAsync(applicationUser));
                return false;
            }

            return await base.CanSignInAsync(applicationUser);
        }

        public override async Task SignInAsync(ApplicationUser applicationUser, AuthenticationProperties authenticationProperties, string? authenticationMethod = null)
        {
            await base.SignInAsync(applicationUser, authenticationProperties, authenticationMethod);

            applicationUser.LastLoginDate = DateTimeOffset.UtcNow;
            var updateResult = await UserManager.UpdateAsync(applicationUser);
            if (!updateResult.Succeeded)
            {
                var errorList = updateResult.Errors.Select(x => $"{x.Code}: {x.Description}");
                throw new Exception("Failed to update applicationUser last login: " + string.Join("; ", errorList));
            }
        }

        public override async Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent, bool lockoutOnFailure)
        {
            var result = await base.PasswordSignInAsync(userName, password, isPersistent, lockoutOnFailure);

            if (result.Succeeded)
            {
                var applicationUser = await UserManager.FindByNameAsync(userName);
                applicationUser.LastLoginDate = DateTimeOffset.UtcNow;
                await UserManager.UpdateAsync(applicationUser);
            }
            return result;
        }
    }
}

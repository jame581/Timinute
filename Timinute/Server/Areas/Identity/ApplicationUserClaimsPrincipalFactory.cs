using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Timinute.Server.Helpers;
using Timinute.Server.Models;

namespace Timinute.Server.Areas.Identity
{
    public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
    {
        UserManager<ApplicationUser> userManager;
        public ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IOptions<IdentityOptions> options
            ) : base(userManager, roleManager, options)
        {
            this.userManager = userManager;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            ClaimsIdentity claims = await base.GenerateClaimsAsync(user);
                        
            claims.AddClaims(new List<Claim>
            {
                new Claim(Constants.Claims.Fullname, $"{user.FirstName} {user.LastName}"),
                new Claim(Constants.Claims.LastLogin, user.LastLoginDate.HasValue ? user.LastLoginDate.Value.ToString("hh:mm dd/mm/yyyy") : string.Empty),
            });

            var roles = await userManager.GetRolesAsync(user);
            
            foreach (var role in roles)
            {
                claims.AddClaim(new Claim(Constants.Claims.Role, role));
            }

            return claims;
        }
    }
}

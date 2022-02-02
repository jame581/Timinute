using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Timinute.Server.Models;

namespace Timinute.Server.Areas.Identity
{
    public class ApplicationUserClaimsTransformation : IClaimsTransformation
    {
        private readonly UserManager<ApplicationUser> userManager;
        public ApplicationUserClaimsTransformation(UserManager<ApplicationUser> userManager)
        {
            this.userManager = userManager;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            var identity = principal.Identities.FirstOrDefault(c => c.IsAuthenticated);
            if (identity == null) return principal;

            var user = await userManager.GetUserAsync(principal);

            if (user == null) return principal;

            if (!principal.HasClaim(c => c.Type == ClaimTypes.GivenName))
            {
                identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName));

            }
            if (!principal.HasClaim(c => c.Type == ClaimTypes.Surname))
            {
                identity.AddClaim(new Claim(ClaimTypes.Surname, user.LastName));
            }


            if (!principal.HasClaim(c => c.Type == ClaimTypes.Role))
            {
                var roles = await userManager.GetRolesAsync(user);
                foreach (var role in roles)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }

            return new ClaimsPrincipal(identity);
        }
    }
}

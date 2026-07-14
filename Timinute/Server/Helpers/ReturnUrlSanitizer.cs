using Microsoft.AspNetCore.Mvc;

namespace Timinute.Server.Helpers
{
    /// <summary>
    /// Normalizes an untrusted <c>returnUrl</c> to a URL that is guaranteed to be local
    /// to this application.
    ///
    /// Identity's scaffolded pages only validate returnUrl at redirect time (LocalRedirect
    /// throws on a foreign URL), but they assign the raw query-string value to a public
    /// property that is rendered into the page before any redirect happens. Sanitizing on
    /// GET closes the open-redirect window and keeps untrusted input out of the markup.
    /// </summary>
    public static class ReturnUrlSanitizer
    {
        public static string Sanitize(IUrlHelper url, string? returnUrl)
        {
            ArgumentNullException.ThrowIfNull(url);

            if (!string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            return url.Content("~/");
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Timinute.Server.Services.App
{
    /// <summary>
    /// Thrown when an inbound DTO fails its Data Annotations at the app-service boundary.
    /// REST controllers are already validated by the <c>[ApiController]</c> 422 short-circuit
    /// before the service runs, so the controller-side translation is a safety net that the
    /// existing REST suite never hits; the MCP tools, however, construct DTOs directly and would
    /// otherwise bypass <c>StringLength</c>, the color regex, <c>MinDuration</c>, etc. — this is
    /// the single choke point that enforces them for every caller. Named
    /// <c>AppValidationException</c> (not <c>ValidationException</c>, which collides with
    /// <see cref="System.ComponentModel.DataAnnotations.ValidationException"/>).
    /// </summary>
    public class AppValidationException : Exception
    {
        public AppValidationException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Runs Data Annotations on inbound DTOs at the app-service choke point. The Shared
    /// validators (<c>MinDurationAttribute</c>, <c>NonDefaultDateTimeOffsetAttribute</c>) do not
    /// consult the <see cref="ValidationContext"/> service provider, so a null one is fine.
    /// </summary>
    internal static class DtoValidator
    {
        public static void Validate(object dto)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true))
            {
                var message = string.Join(" ", results
                    .Select(r => r.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m)));
                throw new AppValidationException(message);
            }
        }
    }
}

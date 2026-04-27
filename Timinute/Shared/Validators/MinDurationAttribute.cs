using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Validators
{
    public class MinDurationAttribute : ValidationAttribute
    {
        public MinDurationAttribute() : base("Duration must be greater than zero.")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Treat null as valid here — let [Required] be the single source of "missing
            // value" errors. Otherwise a null nullable duration surfaces both messages.
            if (value is null)
            {
                return ValidationResult.Success;
            }

            if (value is not TimeSpan duration)
            {
                return new ValidationResult("A valid duration is required.");
            }

            if (duration <= TimeSpan.Zero)
            {
                return new ValidationResult(ErrorMessage);
            }

            return ValidationResult.Success;
        }
    }
}

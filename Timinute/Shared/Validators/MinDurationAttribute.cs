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

using System.ComponentModel.DataAnnotations;

namespace WaterTemperatures.Models;

/// <summary>
/// Validation attribute that rejects a <see cref="DateOnly"/> later than today.
/// The browser also caps the date picker via <c>max</c>, but this enforces the
/// same rule server-side so a crafted request cannot store a future reading.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class NotInFutureAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is DateOnly date && date > DateOnly.FromDateTime(DateTime.Today))
        {
            return new ValidationResult(
                ErrorMessage ?? "Date cannot be in the future.",
                [validationContext.MemberName!]);
        }

        return ValidationResult.Success;
    }
}

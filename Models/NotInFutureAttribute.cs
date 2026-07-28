using System.ComponentModel.DataAnnotations;
using WaterTemperatures.Resources;

namespace WaterTemperatures.Models;

/// <summary>
/// Validation attribute that rejects a <see cref="DateOnly"/> later than today.
/// The browser also caps the date picker via <c>max</c>, but this enforces the
/// same rule server-side so a crafted request cannot store a future reading.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class NotInFutureAttribute : ValidationAttribute
{
    /// <summary>
    /// Attribute arguments have to be compile-time constants, so the message
    /// cannot be an <see cref="AppText"/> lookup at the call site. DataAnnotations'
    /// resource accessor reads the static property on each validation instead,
    /// which is what makes the message follow the request's language.
    /// </summary>
    public NotInFutureAttribute()
    {
        ErrorMessageResourceType = typeof(AppText);
        ErrorMessageResourceName = nameof(AppText.ValidationDateInFuture);
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is DateOnly date && date > DateOnly.FromDateTime(DateTime.Today))
        {
            return new ValidationResult(
                FormatErrorMessage(validationContext.DisplayName),
                [validationContext.MemberName!]);
        }

        return ValidationResult.Success;
    }
}

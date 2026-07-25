using System.ComponentModel.DataAnnotations;

namespace WaterTemperatures.Models;

/// <summary>
/// A single water-temperature measurement from Torniojoki at Jarhoinen.
/// </summary>
public class TemperatureReading
{
    /// <summary>Unique id. Doubles as the Cosmos DB item id later.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The day the measurement was taken (no time component).</summary>
    [NotInFuture(ErrorMessage = "Date cannot be in the future.")]
    public DateOnly MeasuredOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Water temperature in degrees Celsius.</summary>
    [Range(0, 40, ErrorMessage = "Temperature must be between 0 and 40 °C.")]
    public double Celsius { get; set; }

    /// <summary>Optional note (weather, who measured, etc.).</summary>
    [MaxLength(500)]
    public string? Note { get; set; }
}

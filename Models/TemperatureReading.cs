using System.ComponentModel.DataAnnotations;

namespace WaterTemperatures.Models;

/// <summary>
/// A single water-temperature measurement from Torniojoki at Järhöinen.
/// </summary>
public class TemperatureReading
{
    /// <summary>Unique id. Doubles as the Cosmos DB item id later.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>When the measurement was taken (UTC).</summary>
    public DateTimeOffset MeasuredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Water temperature in degrees Celsius.</summary>
    [Range(-5, 40, ErrorMessage = "Temperature must be between -5 and 40 °C.")]
    public double Celsius { get; set; }

    /// <summary>Free-text location detail, e.g. "by the bridge". Optional.</summary>
    [MaxLength(120)]
    public string? Spot { get; set; }

    /// <summary>Optional note (weather, who measured, etc.).</summary>
    [MaxLength(500)]
    public string? Note { get; set; }
}

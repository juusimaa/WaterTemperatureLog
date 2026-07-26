using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WaterTemperatures.Models;

/// <summary>
/// A single water-temperature measurement from Torniojoki at Jarhoinen.
/// </summary>
public class TemperatureReading
{
    /// <summary>
    /// Unique id, derived from <see cref="MeasuredOn"/> as <c>yyyy-MM-dd</c>. Serialized as
    /// the lowercase <c>id</c> that Cosmos DB requires, and also used as the partition key
    /// value. Deriving it from the date is what enforces one reading per day: a second
    /// insert for the same date collides on the id and is rejected by the store.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id => IdFor(MeasuredOn);

    /// <summary>The day the measurement was taken (no time component).</summary>
    [NotInFuture(ErrorMessage = "Date cannot be in the future.")]
    public DateOnly MeasuredOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// Water temperature in degrees Celsius. <see cref="decimal"/> rather than
    /// <see cref="double"/> so a reading like 12.5 is stored and compared exactly.
    /// </summary>
    [Range(0.0, 40.0, ErrorMessage = "Temperature must be between 0 and 40 °C.")]
    public decimal Celsius { get; set; }

    /// <summary>Optional note (weather, who measured, etc.).</summary>
    [MaxLength(500)]
    public string? Note { get; set; }

    /// <summary>Email of the editor who first added this reading.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>When this reading was first added.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Email of the editor who last changed this reading, or null if never edited.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>When this reading was last changed, or null if never edited.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// True once the reading has been deleted. Deletes are soft: the item stays in the
    /// store so an accidental delete can be undone, but it is hidden from every list and
    /// lookup. The date stays occupied, so re-adding that day revives this item.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>Email of the editor who deleted this reading, or null if it is not deleted.</summary>
    public string? DeletedBy { get; set; }

    /// <summary>When this reading was deleted, or null if it is not deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>The editor responsible for the current values — the last editor, or the creator.</summary>
    [JsonIgnore]
    public string? LastEditedBy => UpdatedBy ?? CreatedBy;

    /// <summary>The item id a reading on <paramref name="date"/> has (or would have).</summary>
    public static string IdFor(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// A single AI-extracted field from a document, with confidence and source
/// location. Users may override the value with a recorded reason.
/// </summary>
public class ExtractedField : BaseEntity
{
    public Guid DocumentId { get; set; }

    public string FieldName { get; set; } = string.Empty;

    public string? FieldValue { get; set; }

    /// <summary>Model confidence 0–100.</summary>
    public decimal? Confidence { get; set; }

    public int? SourcePage { get; set; }

    /// <summary>Bounding box on the source page as JSON.</summary>
    public string? SourceBboxJson { get; set; }

    public string? OverrideValue { get; set; }

    public string? OverrideReason { get; set; }

    public Guid? OverrideBy { get; set; }

    public Document? Document { get; set; }
}

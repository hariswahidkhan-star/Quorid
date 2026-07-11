namespace Quorid.Application.Documents;

/// <summary>Result of AI document classification (Domain &gt; Category &gt; Type).</summary>
public record ClassificationResult(
    string DomainCode,
    string DomainName,
    string? CategoryCode,
    string? CategoryName,
    string? TypeCode,
    string? TypeName,
    decimal Confidence)
{
    /// <summary>Spec threshold: ≥ 85% is auto-accepted, below is flagged for review.</summary>
    public bool IsAutoAccepted => Confidence >= 85m;
}

/// <summary>A single field the extractor pulled from a document.</summary>
public record ExtractedFieldResult(
    string FieldName,
    string? FieldValue,
    decimal Confidence,
    int? SourcePage);

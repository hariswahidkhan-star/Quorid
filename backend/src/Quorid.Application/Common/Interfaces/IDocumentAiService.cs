using Quorid.Application.Documents;

namespace Quorid.Application.Common.Interfaces;

/// <summary>
/// Document understanding: classification (Claude Haiku in production) and field
/// extraction (Claude Sonnet). The dev implementation is a deterministic,
/// offline heuristic so the capture flow works without API keys.
/// </summary>
public interface IDocumentAiService
{
    Task<ClassificationResult> ClassifyAsync(
        string fileName, string contentType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExtractedFieldResult>> ExtractFieldsAsync(
        string fileName, ClassificationResult classification,
        CancellationToken cancellationToken = default);
}

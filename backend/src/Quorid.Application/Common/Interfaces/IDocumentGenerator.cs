using Quorid.Application.DocumentCreation;

namespace Quorid.Application.Common.Interfaces;

/// <summary>
/// Document authoring for the creation studio (Module 4, spec §42 AI touchpoints).
/// The dev implementation is a deterministic, offline composer so drafting works
/// without API keys; the production seam hands the request to Claude Sonnet.
/// </summary>
public interface IDocumentGenerator
{
    /// <summary>Compose a full draft body from a prompt, optionally seeded by a template.</summary>
    Task<string> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Rewrite an existing draft body under a free-text instruction.</summary>
    Task<string> ImproveAsync(string body, string instruction, CancellationToken cancellationToken = default);
}

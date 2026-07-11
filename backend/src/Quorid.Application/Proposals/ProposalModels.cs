namespace Quorid.Application.Proposals;

public record CreateProposalRequest(string Title, string? RecipientName, decimal? Value, DateOnly? DueDate, string? CoverLetter);

public record UpdateProposalRequest(
    string? Title, string? RecipientName, string? Stage, decimal? Value, string? CoverLetter, DateOnly? DueDate);

public record AddProposalDocumentsRequest(IReadOnlyList<Guid> DocumentIds);

public record ReorderRequest(IReadOnlyList<Guid> ProposalDocumentIds);

public record ProposalListItemDto(
    Guid Id, string Title, string? RecipientName, string Stage, decimal? Value,
    DateOnly? DueDate, int DocumentCount, DateTime CreatedAt);

public record ProposalDocumentDto(Guid Id, Guid DocumentId, string FileName, string? Title, int DisplayOrder);

public record ProposalDetailDto(
    Guid Id, string Title, string? RecipientName, string Stage, decimal? Value,
    string? CoverLetter, DateOnly? DueDate, IReadOnlyList<ProposalDocumentDto> Documents);

public record ProposalAnalyticsDto(
    int Total, int Won, int Lost, int Open, int WinRatePercent, decimal PipelineValue, decimal WonValue);

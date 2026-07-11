using Quorid.Application.Compliance;
using Quorid.Domain.Enums;
using Xunit;

namespace Quorid.Domain.Tests;

public class ComplianceEvaluatorTests
{
    private static readonly DateOnly Today = new(2026, 1, 1);

    [Fact]
    public void Matching_valid_document_marks_requirement_present()
    {
        var reqs = new[] { new RequirementInfo(Guid.NewGuid(), "GL Insurance", new[] { "INS" }, VerificationTier.T1) };
        var docs = new[] { new ComplianceDoc(Guid.NewGuid(), "GL", "gl.pdf", "INS", VerificationTier.T2, null) };

        var (statuses, score) = ComplianceEvaluator.Evaluate(reqs, docs, Today);

        Assert.Equal("present", Assert.Single(statuses).Status);
        Assert.Equal(100, score);
    }

    [Fact]
    public void No_matching_document_marks_missing_and_zero_score()
    {
        var reqs = new[] { new RequirementInfo(Guid.NewGuid(), "GL Insurance", new[] { "INS" }, VerificationTier.T1) };

        var (statuses, score) = ComplianceEvaluator.Evaluate(reqs, System.Array.Empty<ComplianceDoc>(), Today);

        Assert.Equal("missing", Assert.Single(statuses).Status);
        Assert.Equal(0, score);
    }

    [Fact]
    public void Expired_document_marks_requirement_expired()
    {
        var reqs = new[] { new RequirementInfo(Guid.NewGuid(), "GL Insurance", new[] { "INS" }, VerificationTier.T1) };
        var docs = new[] { new ComplianceDoc(Guid.NewGuid(), "GL", "gl.pdf", "INS", VerificationTier.T1, new DateOnly(2025, 1, 1)) };

        var (statuses, _) = ComplianceEvaluator.Evaluate(reqs, docs, Today);

        Assert.Equal("expired", Assert.Single(statuses).Status);
    }

    [Fact]
    public void Below_min_tier_document_does_not_satisfy_requirement()
    {
        var reqs = new[] { new RequirementInfo(Guid.NewGuid(), "Audited Financials", new[] { "FIN" }, VerificationTier.T2) };
        var docs = new[] { new ComplianceDoc(Guid.NewGuid(), "P&L", "pl.pdf", "FIN", VerificationTier.T1, null) };

        var (statuses, score) = ComplianceEvaluator.Evaluate(reqs, docs, Today);

        Assert.Equal("missing", Assert.Single(statuses).Status);
        Assert.Equal(0, score);
    }
}

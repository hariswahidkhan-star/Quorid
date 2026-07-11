using Quorid.Domain.Enums;

namespace Quorid.Application.Admin;

/// <summary>
/// The four subscription tiers (spec §9 Pricing) with their limits. Used by the
/// billing screen to show plan comparison and to gauge usage against the caller's
/// current plan. <see cref="int.MaxValue"/> represents "unlimited".
/// </summary>
public static class PlanCatalog
{
    public static readonly IReadOnlyList<PlanDto> Plans = new List<PlanDto>
    {
        new(nameof(PlanTier.Starter), "Starter", 0m, UserLimit: 3, DocumentLimit: 100, StorageGb: 5, RoomLimit: 1),
        new(nameof(PlanTier.Business), "Business", 99m, UserLimit: 10, DocumentLimit: 1_000, StorageGb: 50, RoomLimit: 10),
        new(nameof(PlanTier.Professional), "Professional", 299m, UserLimit: 50, DocumentLimit: 10_000, StorageGb: 250, RoomLimit: 50),
        new(nameof(PlanTier.Enterprise), "Enterprise", 999m, UserLimit: int.MaxValue, DocumentLimit: int.MaxValue, StorageGb: 2_048, RoomLimit: int.MaxValue),
    };

    public static PlanDto For(PlanTier tier) =>
        Plans.FirstOrDefault(p => p.Key == tier.ToString()) ?? Plans[0];
}

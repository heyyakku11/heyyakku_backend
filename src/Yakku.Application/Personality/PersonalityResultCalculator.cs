namespace Yakku.Application.Personality
{
    /// <summary>A trait with its vote count across all valid votes of a card.</summary>
    public sealed record TraitTally(int TraitId, string Name, int DisplayOrder, int Votes);

    /// <summary>A trait the owner chose for themselves.</summary>
    public sealed record SelfTraitRef(int TraitId, string Name, int DisplayOrder);

    public sealed record PersonalityComparison(
        IReadOnlyList<SelfTraitRef> SelfTraits,
        IReadOnlyList<TraitTally> CrowdTop,
        IReadOnlyList<SelfTraitRef> Common,
        IReadOnlyList<SelfTraitRef> SelfOnly,
        IReadOnlyList<TraitTally> CrowdOnly);

    /// <summary>
    /// The "you vs the crowd" calculation. Pure: it works only from the persisted selections, so nothing is stored.
    /// </summary>
    public static class PersonalityResultCalculator
    {
        public const int CrowdSize = 3;

        public static PersonalityComparison Compare(
            IEnumerable<SelfTraitRef> selfTraits,
            IEnumerable<TraitTally> tallies)
        {
            var self = selfTraits
                .OrderBy(trait => trait.DisplayOrder)
                .ThenBy(trait => trait.TraitId)
                .ToList();

            // Most votes first. Ties go to the lower display order, then the lower id, so the result never changes
            // between calls.
            var crowd = tallies
                .Where(tally => tally.Votes > 0)
                .OrderByDescending(tally => tally.Votes)
                .ThenBy(tally => tally.DisplayOrder)
                .ThenBy(tally => tally.TraitId)
                .Take(CrowdSize)
                .ToList();

            var selfIds = self.Select(trait => trait.TraitId).ToHashSet();
            var crowdIds = crowd.Select(tally => tally.TraitId).ToHashSet();

            return new PersonalityComparison(
                self,
                crowd,
                self.Where(trait => crowdIds.Contains(trait.TraitId)).ToList(),
                self.Where(trait => !crowdIds.Contains(trait.TraitId)).ToList(),
                crowd.Where(tally => !selfIds.Contains(tally.TraitId)).ToList());
        }
    }
}

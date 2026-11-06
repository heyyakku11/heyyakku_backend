namespace Yakku.Domain.Enums
{
    /// <summary>Lifecycle of a personality card. Only moves forward: Draft, then Collecting, then Completed.</summary>
    public enum PersonalityCardStatus
    {
        /// <summary>Created at registration; the owner has not described themselves yet. Takes no votes.</summary>
        Draft = 0,

        /// <summary>The owner picked their traits; other users can vote.</summary>
        Collecting = 1,

        /// <summary>The vote threshold was reached. No more votes; results are available.</summary>
        Completed = 2
    }
}

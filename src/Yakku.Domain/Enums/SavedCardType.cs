namespace Yakku.Domain.Enums
{
    /// <summary>Which kind of card a saved card points at.</summary>
    public enum SavedCardType
    {
        /// <summary>A poll made by another user.</summary>
        Poll = 0,

        /// <summary>A just-for-fun card (stored as a check of kind Fun).</summary>
        FunCard = 1
    }
}

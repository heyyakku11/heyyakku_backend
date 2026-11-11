namespace Yakku.Domain.Enums
{
    /// <summary>
    /// Which kind of card a row in <c>card_themes</c> styles. One table holds the themes of every card kind;
    /// add a value here when a new kind of card needs themes.
    /// </summary>
    public enum CardThemeKind
    {
        /// <summary>Weekly system checks (Mirror, Vibe, Trust...).</summary>
        Check = 0,

        /// <summary>"Just for fun" cards: system-made, no live or expiry date, users pick one and make it their own.</summary>
        Fun = 1
    }
}

namespace SoulBind.Core
{
    /// <summary>
    /// Anything that can pick a bot's next action. SimpleBot follows a priority list; SmartBot tries
    /// each play on a copy of the match first. Both return actions SimpleBot.Apply can carry out.
    /// </summary>
    public interface IBot
    {
        /// <summary>A short name for reports ("SimpleBot", "SmartBot").</summary>
        string Name { get; }

        /// <summary>The next action for whoever must act now (the current Apostle, or the Phase chooser).</summary>
        BotAction Decide(Match match);
    }
}

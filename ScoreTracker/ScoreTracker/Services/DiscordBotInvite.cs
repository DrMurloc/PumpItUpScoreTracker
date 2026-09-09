namespace ScoreTracker.Web.Services;

/// <summary>
///     The bot's invite URL, in one place because two surfaces now offer it: the Communities
///     directory, and the re-invite pointer on a community's Discord page.
/// </summary>
public static class DiscordBotInvite
{
    /// <summary>
    ///     View Channel + Send Messages + Embed Links + Read History + Use External Emojis
    ///     (347136, what the feeds need) plus Manage Roles (268435456), which the community
    ///     title-role feature needs.
    ///     <para>
    ///         ⚠ Raising this number grants nothing to a server that already added the bot.
    ///         Discord applies an invite's permissions when the invite is accepted, so an existing
    ///         install keeps whatever it was added with — which is why every server invited before
    ///         the role feature reports <c>BotCannotManageRoles</c> until somebody re-runs this URL.
    ///     </para>
    /// </summary>
    public const ulong Permissions = 268782592;

    public static string UrlFor(string clientId) =>
        $"https://discord.com/oauth2/authorize?client_id={clientId}" +
        $"&scope=bot+applications.commands&permissions={Permissions}";
}

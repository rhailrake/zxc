using System.Text.Json.Serialization;

namespace Zxc.Bot.Players;

public sealed record DiscordLinkInfo(
    [property: JsonPropertyName("ss14_player_id")] Guid Ss14PlayerId,
    [property: JsonPropertyName("user_id")] int UserId,
    [property: JsonPropertyName("discord_id")] string DiscordId,
    [property: JsonPropertyName("discord_username")] string DiscordUsername);

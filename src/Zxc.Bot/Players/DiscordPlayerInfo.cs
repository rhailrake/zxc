using System.Text.Json.Serialization;

namespace Zxc.Bot.Players;

public sealed record DiscordPlayerInfo(
    [property: JsonPropertyName("discord_id")] string DiscordId,
    [property: JsonPropertyName("discord_username")] string DiscordUsername,
    [property: JsonPropertyName("ss14_player_id")] Guid Ss14PlayerId,
    [property: JsonPropertyName("user_id")] int UserId,
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("player_user_name")] string? PlayerUserName);

using System.Text.Json.Serialization;

namespace Zxc.Bot.Auth;

public sealed record AuthUserInfo(
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("patronTier")] string? PatronTier,
    [property: JsonPropertyName("createdTime")] DateTimeOffset CreatedTime);

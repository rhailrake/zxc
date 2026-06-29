namespace Zxc.Bot.Access;

public sealed class AccessStoreDocument
{
    public Dictionary<string, List<ulong>> CommandRoleIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<ulong> AllowedRoleIds { get; set; } = [];
}

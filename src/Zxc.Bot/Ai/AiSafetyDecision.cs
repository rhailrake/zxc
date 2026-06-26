namespace Zxc.Bot.Ai;

public sealed record AiSafetyDecision(bool Allowed, string? Reply)
{
    public static AiSafetyDecision Allow { get; } = new(true, null);

    public static AiSafetyDecision Block(string reply)
    {
        return new AiSafetyDecision(false, reply);
    }
}

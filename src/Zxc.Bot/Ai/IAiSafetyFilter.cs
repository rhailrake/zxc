namespace Zxc.Bot.Ai;

public interface IAiSafetyFilter
{
    AiSafetyDecision CheckUserPrompt(string content);

    AiSafetyDecision CheckModelReply(string content);
}

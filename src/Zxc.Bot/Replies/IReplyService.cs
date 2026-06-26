namespace Zxc.Bot.Replies;

public interface IReplyService
{
    string Pick(ReplyKind kind);

    string Format(ReplyKind kind, string? details = null);
}

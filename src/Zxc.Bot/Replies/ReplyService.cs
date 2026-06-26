namespace Zxc.Bot.Replies;

public sealed class ReplyService : IReplyService
{
    private readonly object _gate = new();
    private readonly Dictionary<ReplyKind, int> _lastIndexes = new();

    public string Pick(ReplyKind kind)
    {
        var phrases = GetPhrases(kind);

        lock (_gate)
        {
            var index = Random.Shared.Next(phrases.Length);
            if (phrases.Length > 1 && _lastIndexes.TryGetValue(kind, out var lastIndex))
            {
                while (index == lastIndex)
                    index = Random.Shared.Next(phrases.Length);
            }

            _lastIndexes[kind] = index;
            return phrases[index];
        }
    }

    public string Format(ReplyKind kind, string? details = null)
    {
        var prefix = Pick(kind);
        return string.IsNullOrWhiteSpace(details)
            ? prefix
            : $"{prefix}\n{details}";
    }

    private static string[] GetPhrases(ReplyKind kind)
    {
        return kind switch
        {
            ReplyKind.Success => ["Мяу.", "Мяуууу.", "Няяя.", "Няяяя.", "Мррр мяу.", "Мяу-мяу."],
            ReplyKind.Denied => ["Мяу!", "Мяу?", "Няяя.", "Няяяя...", "Мррр.", "Мррр мяу."],
            ReplyKind.Empty => ["Мяу...", "Няяя...", "Няяяя.", "Мррр...", "Мяу-мяу?"],
            ReplyKind.Error => ["Мяу?!", "Няяя?!", "Мррр!", "Мяуууу...", "Няяяя..."],
            _ => ["Мяу."],
        };
    }
}

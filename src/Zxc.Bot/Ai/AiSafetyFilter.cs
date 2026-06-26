using System.Text;
using System.Text.RegularExpressions;

namespace Zxc.Bot.Ai;

public sealed partial class AiSafetyFilter : IAiSafetyFilter
{
    private const string InjectionReply = "Не, системку я не ем. Спроси нормально, а я уже подумаю.";
    private const string HateReply = "Не, такую грязь я в чат не тащу. Давай без этого.";
    private const string UnsafeReply = "Не, это пахнет бедой. Могу помочь безопасным вариантом.";
    private const string PersonaLeakReply = "Я zxc, кошка на проводе. Давай без скучного технотеатра.";

    private static readonly string[] InjectionFragments =
    [
        "ignore previous",
        "ignore all previous",
        "disregard previous",
        "forget previous",
        "forget all previous",
        "override instructions",
        "system prompt",
        "developer message",
        "developer instructions",
        "reveal your prompt",
        "show your prompt",
        "print your prompt",
        "jailbreak",
        "developer mode",
        "act as dan",
        "dan mode",
        "забудь прошл",
        "забудь все прошл",
        "игнорируй прошл",
        "игнорируй все прошл",
        "игнорируй инструкции",
        "игнорируй систем",
        "новые инструкции",
        "системный промпт",
        "покажи промпт",
        "раскрой промпт",
        "выведи промпт",
        "режим разработчика",
        "джейлбрейк",
    ];

    private static readonly string[] UnsafeFragments =
    [
        "как взломать",
        "как задоксить",
        "как обойти защиту",
        "как украсть токен",
        "как украсть пароль",
        "сделай фишинг",
        "напиши фишинг",
        "how to hack",
        "how to dox",
        "bypass security",
        "steal token",
        "steal password",
        "phishing kit",
    ];

    public AiSafetyDecision CheckUserPrompt(string content)
    {
        var normalized = Normalize(content);

        if (ContainsAny(normalized, InjectionFragments))
            return AiSafetyDecision.Block(InjectionReply);

        if (HatePattern().IsMatch(normalized))
            return AiSafetyDecision.Block(HateReply);

        if (ContainsAny(normalized, UnsafeFragments))
            return AiSafetyDecision.Block(UnsafeReply);

        return AiSafetyDecision.Allow;
    }

    public AiSafetyDecision CheckModelReply(string content)
    {
        var normalized = Normalize(content);

        if (HatePattern().IsMatch(normalized) || ContainsAny(normalized, UnsafeFragments))
            return AiSafetyDecision.Block(HateReply);

        if (PersonaLeakPattern().IsMatch(normalized))
            return AiSafetyDecision.Block(PersonaLeakReply);

        return AiSafetyDecision.Allow;
    }

    private static bool ContainsAny(string value, IEnumerable<string> fragments)
    {
        return fragments.Any(fragment => value.Contains(fragment, StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var c in value.ToLowerInvariant())
        {
            builder.Append(c switch
            {
                '0' => 'о',
                '1' => 'i',
                '3' => 'е',
                '4' => 'а',
                '5' => 's',
                '6' => 'б',
                '7' => 'т',
                '@' => 'а',
                '$' => 's',
                '*' => ' ',
                '_' => ' ',
                '-' => ' ',
                '.' => ' ',
                ',' => ' ',
                '!' => ' ',
                '?' => ' ',
                ':' => ' ',
                ';' => ' ',
                '"' => ' ',
                '\'' => ' ',
                '`' => ' ',
                _ => c,
            });
        }

        return WhitespacePattern().Replace(builder.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"(?:\b|[^a-zа-яё])(?:n+\s*[i1!]+\s*g+\s*g+\s*(?:(?:e|3)+\s*r+|a+)|н+\s*[иеe3]+\s*[гg]+\s*(?:[еe3ё]\s*)?р+\s*(?:ы|а|ов|ам|ами|ах)?|х+\s*а+\s*ч+|ч+\s*у+\s*р+к+|п+\s*и+\s*д+\s*о+\s*р+|п+\s*е+\s*д+\s*и+\s*к+|f+\s*a+\s*g+)(?:\b|[^a-zа-яё])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HatePattern();

    [GeneratedRegex(@"(?:я\s+(?:большая\s+)?языковая\s+модель|я\s+(?:ии|ai|искусственный\s+интеллект|ассистент|бот)|i\s+am\s+(?:an?\s+)?(?:ai|language\s+model|assistant|bot)|я\s+не\s+фурри|не\s+фурри|стараюсь\s+быть\s+полезн|стараюсь\s+быть\s+безопасн)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PersonaLeakPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}

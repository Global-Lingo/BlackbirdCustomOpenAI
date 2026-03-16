using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Apps.OpenAI.Utils.Xliff;

public static class XliffTagValidator
{
    private static readonly Regex PhraseTagRegex = new(
        @"\{\d+(?:>|&gt;)\}?|(?:<|&lt;)\d+\}|\{\d+\}",
        RegexOptions.Compiled);

    public static bool HasValidTagStructure(string source, string target)
    {
        var sourceTags = ExtractTags(source);
        if (sourceTags.Count == 0)
        {
            return true;
        }

        var targetTags = ExtractTags(target);
        if (targetTags.Count == 0)
        {
            return false;
        }

        if (!HaveSameStandaloneTags(sourceTags, targetTags))
        {
            return false;
        }

        if (!HaveSamePairedTags(sourceTags, targetTags))
        {
            return false;
        }

        if (!HaveSamePairSequence(sourceTags, targetTags))
        {
            return false;
        }

        return IsWellNested(targetTags);
    }

    private static List<TagToken> ExtractTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new();
        }

        var tags = new List<TagToken>();
        foreach (Match match in PhraseTagRegex.Matches(text))
        {
            if (!match.Success)
            {
                continue;
            }

            var raw = match.Value;
            var idMatch = Regex.Match(raw, @"\d+");
            if (!idMatch.Success || !int.TryParse(idMatch.Value, out var id))
            {
                continue;
            }

            tags.Add(new TagToken(ResolveTagType(raw), id));
        }

        return tags;
    }

    private static TagTokenType ResolveTagType(string raw)
    {
        if (raw.StartsWith("{") && (raw.Contains(">") || raw.Contains("&gt;")))
        {
            return TagTokenType.Open;
        }

        if (raw.StartsWith("{") && raw.EndsWith("}"))
        {
            return TagTokenType.Standalone;
        }

        return TagTokenType.Close;
    }

    private static bool HaveSameStandaloneTags(List<TagToken> sourceTags, List<TagToken> targetTags)
    {
        var sourceStandalone = sourceTags
            .Where(x => x.Type == TagTokenType.Standalone)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        var targetStandalone = targetTags
            .Where(x => x.Type == TagTokenType.Standalone)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        return sourceStandalone.Count == targetStandalone.Count
               && sourceStandalone.All(x => targetStandalone.TryGetValue(x.Key, out var count) && count == x.Value);
    }

    private static bool HaveSamePairedTags(List<TagToken> sourceTags, List<TagToken> targetTags)
    {
        var sourceOpen = sourceTags
            .Where(x => x.Type == TagTokenType.Open)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        var sourceClose = sourceTags
            .Where(x => x.Type == TagTokenType.Close)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        var targetOpen = targetTags
            .Where(x => x.Type == TagTokenType.Open)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        var targetClose = targetTags
            .Where(x => x.Type == TagTokenType.Close)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        return DictionariesEqual(sourceOpen, targetOpen)
               && DictionariesEqual(sourceClose, targetClose);
    }

    private static bool DictionariesEqual(Dictionary<int, int> left, Dictionary<int, int> right)
    {
        return left.Count == right.Count
               && left.All(x => right.TryGetValue(x.Key, out var count) && count == x.Value);
    }

    private static bool HaveSamePairSequence(List<TagToken> sourceTags, List<TagToken> targetTags)
    {
        var sourcePairSequence = sourceTags
            .Where(x => x.Type != TagTokenType.Standalone)
            .ToList();

        var targetPairSequence = targetTags
            .Where(x => x.Type != TagTokenType.Standalone)
            .ToList();

        if (sourcePairSequence.Count != targetPairSequence.Count)
        {
            return false;
        }

        for (var i = 0; i < sourcePairSequence.Count; i++)
        {
            if (sourcePairSequence[i].Type != targetPairSequence[i].Type
                || sourcePairSequence[i].Id != targetPairSequence[i].Id)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWellNested(List<TagToken> tags)
    {
        var stack = new Stack<int>();

        foreach (var tag in tags)
        {
            if (tag.Type == TagTokenType.Open)
            {
                stack.Push(tag.Id);
                continue;
            }

            if (tag.Type != TagTokenType.Close)
            {
                continue;
            }

            if (stack.Count == 0)
            {
                return false;
            }

            var openedId = stack.Pop();
            if (openedId != tag.Id)
            {
                return false;
            }
        }

        return stack.Count == 0;
    }

    private enum TagTokenType
    {
        Standalone,
        Open,
        Close
    }

    private record TagToken(TagTokenType Type, int Id);
}
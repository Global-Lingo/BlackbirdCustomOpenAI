using System;
using System.Collections.Generic;
using System.Linq;

namespace Apps.OpenAI.Utils;

public static class GlossaryExtractionUtils
{
    public static List<Dictionary<string, string>> RemoveDuplicateEntries(
        IEnumerable<Dictionary<string, string>> items,
        bool caseSensitive)
    {
        var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var seenKeys = new HashSet<string>(comparer);
        var deduplicated = new List<Dictionary<string, string>>();

        foreach (var item in items)
        {
            var normalizedItem = item
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}:{pair.Value}");

            var signature = string.Join("|", normalizedItem);
            if (!seenKeys.Add(signature))
            {
                continue;
            }

            deduplicated.Add(item);
        }

        return deduplicated;
    }
}

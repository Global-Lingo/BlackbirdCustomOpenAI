using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.OpenAI.Models.Requests.Chat;

public class GlossaryRequest
{
    public FileReference? Glossary { get; set; }

    [Display("Case-sensitive glossary", Description = "When enabled, glossary term matching against source text is case-sensitive. Defaults to false (case-insensitive).")]
    public bool? CaseSensitiveGlossary { get; set; }
}
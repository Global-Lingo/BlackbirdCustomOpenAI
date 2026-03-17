using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Filters.Transformations;
using System.Collections.Generic;

namespace Apps.OpenAI.Models.PostEdit;

public record BatchProcessingOptions(
    string ModelId,
    string SourceLanguage,
    string TargetLanguage,
    string? Prompt,
    string? SystemPrompt,
    bool OverwritePrompts,
    FileReference? Glossary,
    bool FilterGlossary,
    bool CaseSensitiveGlossary,
    int MaxRetryAttempts,
    int? MaxTokens,
    string? ReasoningEffort,
    List<Note>? Notes,
    string? ClientProfile = null,
    string? SummarisedStyleGuide = null,
    string? ToneOfVoice = null,
    string? FormalityLevel = null);
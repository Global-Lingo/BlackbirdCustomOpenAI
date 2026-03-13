using System.Collections.Generic;
using Apps.OpenAI.Dtos;
using Apps.OpenAI.Models.Entities;

namespace Apps.OpenAI.Models.PostEdit;

public class BatchResult
{
    public List<TranslationEntity> UpdatedTranslations { get; set; } = new();
    public UsageDto Usage { get; set; } = new();
    public List<string> ErrorMessages { get; set; } = new();
    public bool IsSuccess { get; set; } = true;
    public string SystemPrompt { get; set; }
    public bool WasTruncated { get; set; }
    public int ExpectedTranslationCount { get; set; }
    public int ReturnedTranslationCount { get; set; }
    public List<string> MissingTranslationIds { get; set; } = new();
    public bool HasIncompleteResponse => MissingTranslationIds.Count > 0;
}
using Apps.OpenAI.DataSourceHandlers;
using Apps.OpenAI.DataSourceHandlers.ModelDataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Blueprints.Interfaces.Translate;

namespace Apps.OpenAI.Models.Requests.Content;
public class TranslateContentRequest : ITranslateFileInput
{
    public FileReference File { get; set; }

    [Display("Source language")]
    [StaticDataSource(typeof(LocaleDataSourceHandler))]
    public string? SourceLanguage { get; set; }

    [Display("Target language")]
    [StaticDataSource(typeof(LocaleDataSourceHandler))]
    public string TargetLanguage { get; set; }

    [Display("Output file handling", Description = "Determine the format of the output file. The default Blackbird behavior is to convert to XLIFF for future steps."), StaticDataSource(typeof(OpenAiProcessFileFormatHandler))]
    public string? OutputFileHandling { get; set; }

    [Display("Process draft segments", Description = "If enabled, non-empty target segments that are not in translated or signed-off state can be overwritten. By default, these draft segments are skipped.")]
    public bool? ProcessDraftSegments { get; set; }

    [Display("Client profile")]
    public string? ClientProfile { get; set; }

    [Display("Summarised style guide")]
    public string? SummarisedStyleGuide { get; set; }

    [Display("Tone of voice")]
    [StaticDataSource(typeof(ToneOfVoiceHandler))]
    public string? ToneOfVoice { get; set; }

    [Display("Formality level")]
    public string? FormalityLevel { get; set; }
}

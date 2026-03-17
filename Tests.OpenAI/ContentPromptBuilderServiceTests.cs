using Apps.OpenAI.Services;

namespace Tests.OpenAI;

[TestClass]
public class ContentPromptBuilderServiceTests
{
    private ContentPromptBuilderService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new ContentPromptBuilderService();
    }

    [TestMethod]
    public void BuildSystemPrompt_WithStyleContextFields_IncludesStyleContextSection()
    {
        var result = _service.BuildSystemPrompt(
            sourceLanguage: "en-US",
            targetLanguage: "fr-FR",
            additionalPrompt: null,
            glossaryPrompt: null,
            isPostEdit: false,
            notes: null,
            clientProfile: "B2B SaaS company with legal-heavy content",
            summarisedStyleGuide: "Use concise terminology and avoid idioms.",
            toneOfVoice: "Confident Commander",
            formalityLevel: "Formal");

        Assert.IsTrue(result.Contains("### STYLE CONTEXT"));
        Assert.IsTrue(result.Contains("Client profile: B2B SaaS company with legal-heavy content"));
        Assert.IsTrue(result.Contains("Summarised style guide: Use concise terminology and avoid idioms."));
        Assert.IsTrue(result.Contains("Tone of voice: Confident Commander"));
        Assert.IsTrue(result.Contains("Formality level: Formal"));
    }

    [TestMethod]
    public void BuildSystemPrompt_WithoutStyleContextFields_DoesNotIncludeStyleContextSection()
    {
        var result = _service.BuildSystemPrompt(
            sourceLanguage: "en-US",
            targetLanguage: "fr-FR",
            additionalPrompt: null,
            glossaryPrompt: null,
            isPostEdit: false,
            notes: null);

        Assert.IsFalse(result.Contains("### STYLE CONTEXT"));
        Assert.IsFalse(result.Contains("Client profile:"));
        Assert.IsFalse(result.Contains("Summarised style guide:"));
        Assert.IsFalse(result.Contains("Tone of voice:"));
        Assert.IsFalse(result.Contains("Formality level:"));
    }
}

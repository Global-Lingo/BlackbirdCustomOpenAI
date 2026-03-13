using System.Reflection;
using Apps.OpenAI.Actions;

namespace Tests.OpenAI;

[TestClass]
public class TranslationDroppedBucketsMessageTests
{
    [TestMethod]
    public void BuildDroppedBucketsMessage_WhenNoWarnings_ReturnsNull()
    {
        var result = InvokeBuildDroppedBucketsMessage([], 0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void BuildDroppedBucketsMessage_WhenWarningsPresent_ReturnsSummary()
    {
        var warnings = new List<string>
        {
            "Batch 1: response truncated, missing 2 translation(s)",
            "Batch 3: missing 1 translation(s)"
        };

        var result = InvokeBuildDroppedBucketsMessage(warnings, untranslatedSegmentsInDroppedBuckets: 3);

        Assert.IsNotNull(result);
        Assert.Contains("status=warning", result);
        Assert.Contains("dropped_buckets=2", result);
        Assert.Contains("unchanged_segments=3", result);
        Assert.Contains("details=Batch 1:", result);
        Assert.Contains("Batch 3:", result);
    }

    private static string? InvokeBuildDroppedBucketsMessage(List<string> warnings, int untranslatedSegmentsInDroppedBuckets)
    {
        var method = typeof(TranslationActions).GetMethod(
            "BuildDroppedBucketsMessage",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);

        var result = method.Invoke(null, new object[] { warnings, untranslatedSegmentsInDroppedBuckets });
        return result as string;
    }
}

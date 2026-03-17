using Apps.OpenAI.Utils;

namespace Tests.OpenAI;

[TestClass]
public class GlossaryExtractionUtilsTests
{
    [TestMethod]
    public void RemoveDuplicateEntries_CaseSensitive_TreatsDifferentCasingAsDistinct()
    {
        var items = new List<Dictionary<string, string>>
        {
            new() { ["en"] = "API", ["fr"] = "API" },
            new() { ["en"] = "api", ["fr"] = "api" },
            new() { ["en"] = "API", ["fr"] = "API" }
        };

        var result = GlossaryExtractionUtils.RemoveDuplicateEntries(items, true);

        Assert.HasCount(2, result);
        Assert.AreEqual("API", result[0]["en"]);
        Assert.AreEqual("api", result[1]["en"]);
    }

    [TestMethod]
    public void RemoveDuplicateEntries_CaseInsensitive_DeduplicatesDifferentCasing()
    {
        var items = new List<Dictionary<string, string>>
        {
            new() { ["en"] = "API", ["fr"] = "API" },
            new() { ["en"] = "api", ["fr"] = "api" },
            new() { ["en"] = "Api", ["fr"] = "Api" }
        };

        var result = GlossaryExtractionUtils.RemoveDuplicateEntries(items, false);

        Assert.HasCount(1, result);
        Assert.AreEqual("API", result[0]["en"]);
    }

    [TestMethod]
    public void RemoveDuplicateEntries_UsesDictionaryKeyOrderIndependently()
    {
        var items = new List<Dictionary<string, string>>
        {
            new() { ["en"] = "term", ["fr"] = "terme" },
            new() { ["fr"] = "terme", ["en"] = "term" }
        };

        var result = GlossaryExtractionUtils.RemoveDuplicateEntries(items, true);

        Assert.HasCount(1, result);
    }
}

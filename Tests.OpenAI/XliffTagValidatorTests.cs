using Apps.OpenAI.Utils.Xliff;

namespace Tests.OpenAI;

[TestClass]
public class XliffTagValidatorTests
{
    [TestMethod]
    public void HasValidTagStructure_WithStandaloneAndNestedPairs_ReturnsTrue()
    {
        var source = "Start {1} and {2>outer {3>inner<3} outer<2} end";
        var target = "Debut {1} puis {2>externe {3>interne<3} externe<2} fin";

        var isValid = XliffTagValidator.HasValidTagStructure(source, target);

        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public void HasValidTagStructure_MissingStandaloneTag_ReturnsFalse()
    {
        var source = "Hello {1} world";
        var target = "Bonjour monde";

        var isValid = XliffTagValidator.HasValidTagStructure(source, target);

        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void HasValidTagStructure_ClosingBeforeOpening_ReturnsFalse()
    {
        var source = "{1>text<1}";
        var target = "<1}text{1>";

        var isValid = XliffTagValidator.HasValidTagStructure(source, target);

        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void HasValidTagStructure_BrokenNestingOrder_ReturnsFalse()
    {
        var source = "{1>{2>text<2}<1}";
        var target = "{1>{2>text<1}<2}";

        var isValid = XliffTagValidator.HasValidTagStructure(source, target);

        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void HasValidTagStructure_EncodedTags_ReturnsTrue()
    {
        var source = "{1&gt;bold&lt;1}";
        var target = "{1&gt;gras&lt;1}";

        var isValid = XliffTagValidator.HasValidTagStructure(source, target);

        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public void HasValidTagStructure_NoSourceTags_ReturnsTrue()
    {
        var source = "Simple source text";
        var target = "Texte cible simple";

        var isValid = XliffTagValidator.HasValidTagStructure(source, target);

        Assert.IsTrue(isValid);
    }
}
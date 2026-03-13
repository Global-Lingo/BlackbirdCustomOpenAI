using System.Reflection;
using Apps.OpenAI.Actions;

namespace Tests.OpenAI;

[TestClass]
public class TranslationAdaptiveSplitTests
{
    [TestMethod]
    public void ShouldSplitFailedBatch_WhenResponseTruncated_ReturnsTrue()
    {
        var result = InvokeShouldSplitFailedBatch(
            isSuccess: false,
            errorCount: 1,
            batchSize: 8);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldSplitFailedBatch_WhenJsonParsingFails_ReturnsTrue()
    {
        var result = InvokeShouldSplitFailedBatch(
            isSuccess: false,
            errorCount: 1,
            batchSize: 6);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldSplitFailedBatch_WhenTransientErrorOnly_ReturnsTrue()
    {
        var result = InvokeShouldSplitFailedBatch(
            isSuccess: false,
            errorCount: 1,
            batchSize: 8);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldSplitFailedBatch_WhenBucketHasSingleSegment_ReturnsFalse()
    {
        var result = InvokeShouldSplitFailedBatch(
            isSuccess: false,
            errorCount: 1,
            batchSize: 1);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldSplitFailedBatch_WhenNoErrors_ReturnsFalse()
    {
        var result = InvokeShouldSplitFailedBatch(
            isSuccess: false,
            errorCount: 0,
            batchSize: 8);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldSplitFailedBatch_WhenSuccess_ReturnsFalse()
    {
        var result = InvokeShouldSplitFailedBatch(
            isSuccess: true,
            errorCount: 0,
            batchSize: 8);

        Assert.IsFalse(result);
    }

    private static bool InvokeShouldSplitFailedBatch(bool isSuccess, int errorCount, int batchSize)
    {
        var method = typeof(TranslationActions).GetMethod(
            "ShouldSplitFailedBatch",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);

        var result = method.Invoke(null, [isSuccess, errorCount, batchSize]);
        Assert.IsNotNull(result);

        return (bool)result;
    }
}

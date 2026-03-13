using System.Reflection;
using System.Text;
using Apps.OpenAI.Actions;
using Blackbird.Filters.Transformations;

namespace Tests.OpenAI;

[TestClass]
public class TranslationContentDraftSegmentsTests
{
    [TestMethod]
    public void ShouldProcessSegmentForTranslateContent_DefaultMode_SkipsNonFinalNonEmptyDraft()
    {
        var segments = GetSegmentsFromFixture();

        var included = segments
            .Where(segment => InvokeShouldProcessSegmentForTranslateContent(segment, processDraftSegments: false))
            .Select(segment => segment.GetSource())
            .ToList();

        Assert.Contains("S1-empty-initial", included);
        Assert.DoesNotContain("S2-draft-reviewed-with-target", included);
        Assert.DoesNotContain("S3-translated", included);
        Assert.DoesNotContain("S4-final", included);
    }

    [TestMethod]
    public void ShouldProcessSegmentForTranslateContent_DraftMode_ProcessesNonFinalNonEmptyDraft()
    {
        var segments = GetSegmentsFromFixture();

        var included = segments
            .Where(segment => InvokeShouldProcessSegmentForTranslateContent(segment, processDraftSegments: true))
            .Select(segment => segment.GetSource())
            .ToList();

        Assert.Contains("S1-empty-initial", included);
        Assert.Contains("S2-draft-reviewed-with-target", included);
        Assert.DoesNotContain("S3-translated", included);
        Assert.DoesNotContain("S4-final", included);
    }

    private static List<Segment> GetSegmentsFromFixture()
    {
        const string xliff = """
<?xml version="1.0" encoding="UTF-8"?>
<xliff xmlns="urn:oasis:names:tc:xliff:document:2.2" version="2.2" srcLang="en" trgLang="de">
  <file id="f1">
    <unit id="u1">
      <segment state="initial">
        <source>S1-empty-initial</source>
        <target></target>
      </segment>
    </unit>
    <unit id="u2">
      <segment state="reviewed">
        <source>S2-draft-reviewed-with-target</source>
        <target>Existing draft target</target>
      </segment>
    </unit>
    <unit id="u3">
      <segment state="translated">
        <source>S3-translated</source>
        <target>Already translated</target>
      </segment>
    </unit>
    <unit id="u4">
      <segment state="final">
        <source>S4-final</source>
        <target>Signed off text</target>
      </segment>
    </unit>
  </file>
</xliff>
""";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xliff));
        var content = Transformation.Parse(stream, "fixture.xlf").GetAwaiter().GetResult();

        return content.GetUnits().SelectMany(unit => unit.Segments).ToList();
    }

    private static bool InvokeShouldProcessSegmentForTranslateContent(Segment segment, bool processDraftSegments)
    {
        var method = typeof(TranslationActions).GetMethod(
            "ShouldProcessSegmentForTranslateContent",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);

        var result = method.Invoke(null, [segment, processDraftSegments]);
        Assert.IsNotNull(result);

        return (bool)result;
    }
}

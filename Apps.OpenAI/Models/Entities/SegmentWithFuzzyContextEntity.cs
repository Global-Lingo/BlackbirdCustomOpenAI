using Blackbird.Filters.Transformations;


namespace Apps.OpenAI.Services;

public class FuzzyMatchContext
{
    public string? MatchedText { get; set; }
    public double Similarity { get; set; }
}   

public class SegmentWithFuzzyContext
{
    public Segment Segment { get; set; }
    public FuzzyMatchContext? FuzzyContext { get; set; }
}
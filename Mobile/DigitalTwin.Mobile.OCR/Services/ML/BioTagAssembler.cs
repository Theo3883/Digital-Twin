namespace DigitalTwin.Mobile.OCR.Services.ML;

/// <summary>
/// Converts a BIO (Begin-Inside-Outside) tag sequence from BERT token classification
/// into spans of (startWordIdx, endWordIdx, entityLabel).
/// </summary>
public static class BioTagAssembler
{
    public sealed record EntitySpan(int StartWord, int EndWord, string Label, float AvgScore);

    public static IReadOnlyList<EntitySpan> Assemble(
        string[] tags,
        float[] scores,
        int[] tokenToWord)
    {
        var spans = new List<EntitySpan>();

        string? currentLabel = null;
        int currentStart = -1;
        int currentEnd = -1;
        var currentScores = new List<float>();

        for (int i = 0; i < tags.Length; i++)
        {
            int wordIdx = i < tokenToWord.Length ? tokenToWord[i] : -1;
            if (wordIdx < 0) continue;

            var tag = tags[i];

            if (tag.StartsWith("B-", StringComparison.Ordinal))
            {
                FlushSpan(spans, currentLabel, currentStart, currentEnd, currentScores);
                currentLabel = tag[2..];
                currentStart = wordIdx;
                currentEnd = wordIdx;
                currentScores = [scores[i]];
            }
            else if (tag.StartsWith("I-", StringComparison.Ordinal)
                && currentLabel == tag[2..]
                && currentEnd == wordIdx - 1)
            {
                currentEnd = wordIdx;
                currentScores.Add(scores[i]);
            }
            else if (tag == "O")
            {
                FlushSpan(spans, currentLabel, currentStart, currentEnd, currentScores);
                currentLabel = null;
                currentScores = [];
            }
        }

        FlushSpan(spans, currentLabel, currentStart, currentEnd, currentScores);
        return spans;
    }

    private static void FlushSpan(
        List<EntitySpan> spans,
        string? label, int start, int end,
        List<float> scores)
    {
        if (label is null || start < 0) return;
        spans.Add(new EntitySpan(
            start, end, label,
            scores.Count > 0 ? scores.Average() : 0f));
    }
}

using System.Text;

namespace DigitalTwin.OCR.Services.ML;

/// <summary>
/// Pure-C# WordPiece tokenizer compatible with BERT multilingual cased/uncased models.
///
/// Vocab file (vocab.txt) is loaded from the app bundle when a BERT model is present.
/// When the vocab file is absent the tokenizer falls back to whitespace tokenization
/// so the BERT extractor can still produce approximate token IDs (all [UNK]).
///
/// This tokenizer does NOT call any native APIs and is fully cross-platform.
/// </summary>
public sealed class WordPieceTokenizer
{
    private const string UnknownToken  = "[UNK]";
    private const string ContinuePrefix = "##";
    private const int MaxInputCharsPerWord = 100;

    private readonly Dictionary<string, int> _vocab;
    private readonly int _unkId;
    private readonly int _padId;
    private readonly int _clsId;
    private readonly int _sepId;

    public int PadId => _padId;
    public int ClsId => _clsId;
    public int SepId => _sepId;
    public int UnkId => _unkId;

    public static WordPieceTokenizer FromBundledVocab(string? vocabBundlePath = null)
    {
        string? vocabPath = null;

#if IOS || MACCATALYST
        if (vocabPath is null)
        {
            var bundle = Foundation.NSBundle.MainBundle;
            vocabPath = bundle.PathForResource("bert_vocab", "txt")
                     ?? bundle.PathForResource("vocab", "txt");
        }
#endif

        if (vocabPath is null && vocabBundlePath is not null && File.Exists(vocabBundlePath))
            vocabPath = vocabBundlePath;

        Dictionary<string, int> vocab;
        if (vocabPath is not null && File.Exists(vocabPath))
        {
            vocab = LoadVocab(vocabPath);
        }
        else
        {
            // Minimal stub vocab — all real words will map to [UNK]
            vocab = new Dictionary<string, int>
            {
                ["[PAD]"] = 0, ["[UNK]"] = 100, ["[CLS]"] = 101, ["[SEP]"] = 102, ["[MASK]"] = 103
            };
        }

        return new WordPieceTokenizer(vocab);
    }

    private WordPieceTokenizer(Dictionary<string, int> vocab)
    {
        _vocab = vocab;
        _unkId = vocab.TryGetValue(UnknownToken, out var u) ? u : 100;
        _padId = vocab.TryGetValue("[PAD]", out var p) ? p : 0;
        _clsId = vocab.TryGetValue("[CLS]", out var c) ? c : 101;
        _sepId = vocab.TryGetValue("[SEP]", out var s) ? s : 102;
    }

    /// <summary>
    /// Tokenizes text into WordPiece token IDs with [CLS] prefix and [SEP] suffix.
    /// Returns (inputIds, tokenToWordMapping) where tokenToWordMapping[i] is the
    /// original whitespace-word index for token i (or -1 for [CLS]/[SEP]).
    /// </summary>
    public (int[] InputIds, int[] TokenToWord) Tokenize(string text, int maxSeqLen = 512)
    {
        var words = BasicTokenize(text);
        var ids = new List<int> { _clsId };
        var tokenToWord = new List<int> { -1 };

        for (int wi = 0; wi < words.Count && ids.Count < maxSeqLen - 1; wi++)
        {
            var wordPieces = WordPieceTokenizeWord(words[wi].ToLowerInvariant());
            foreach (var piece in wordPieces)
            {
                if (ids.Count >= maxSeqLen - 1) break;
                ids.Add(_vocab.TryGetValue(piece, out var id) ? id : _unkId);
                tokenToWord.Add(wi);
            }
        }

        ids.Add(_sepId);
        tokenToWord.Add(-1);

        return (ids.ToArray(), tokenToWord.ToArray());
    }

    private IEnumerable<string> WordPieceTokenizeWord(string word)
    {
        if (word.Length > MaxInputCharsPerWord)
            return [UnknownToken];

        var subTokens = new List<string>();
        int start = 0;

        while (start < word.Length)
        {
            int end = word.Length;
            string? curSubStr = null;

            while (start < end)
            {
                var substr = (start == 0 ? "" : ContinuePrefix) + word[start..end];
                if (_vocab.ContainsKey(substr)) { curSubStr = substr; break; }
                end--;
            }

            if (curSubStr is null)
            {
                subTokens.Add(UnknownToken);
                break;
            }

            subTokens.Add(curSubStr);
            start = end;
        }

        return subTokens;
    }

    private static List<string> BasicTokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
            }
            else if (IsPunctuation(c))
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                tokens.Add(c.ToString());
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0) tokens.Add(current.ToString());
        return tokens;
    }

    private static bool IsPunctuation(char c)
        => char.IsPunctuation(c) || char.IsSymbol(c);

    private static Dictionary<string, int> LoadVocab(string path)
    {
        var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        int idx = 0;
        foreach (var line in File.ReadLines(path))
            vocab[line] = idx++;
        return vocab;
    }
}

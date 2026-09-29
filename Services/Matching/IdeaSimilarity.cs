using System.Text;

namespace StartupConnect.Services.Matching;

/// <summary>The text fields of an idea used for similarity (plus category/market for boosts).</summary>
public sealed class IdeaTextFields
{
    public int Id { get; init; }
    public int CategoryId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Tagline { get; init; } = string.Empty;
    public string ProblemStatement { get; init; } = string.Empty;
    public string Solution { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TargetMarket { get; init; } = string.Empty;
    public string BusinessModel { get; init; } = string.Empty;
}

/// <summary>Weighted term frequencies of one idea (stem → weight) plus a display form per stem.</summary>
public sealed class IdeaTermVector
{
    public int IdeaId { get; init; }
    public int CategoryId { get; init; }
    public Dictionary<string, double> Terms { get; init; } = new();
    /// <summary>Stem → most frequent original word (used for "common keywords").</summary>
    public Dictionary<string, string> Display { get; init; } = new();
    public HashSet<string> MarketTerms { get; init; } = new();
}

/// <summary>Tokeniser shared by similar-idea scoring and the "common keywords" shown in the UI.</summary>
public static class IdeaTextAnalyzer
{
    // Field weights: title/tagline > problem/solution > description/market > business model.
    public const double TitleWeight = 3.0, TaglineWeight = 2.5, ProblemWeight = 2.0, SolutionWeight = 2.0,
        DescriptionWeight = 1.0, MarketWeight = 1.0, BusinessWeight = 0.5;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a","about","above","after","again","against","all","also","am","an","and","any","app","are","as","at","be","because","been",
        "before","being","below","between","both","but","by","can","could","did","do","does","doing","down","during","each","easy",
        "etc","even","every","few","for","from","further","get","gets","had","has","have","having","he","help","helps","her","here",
        "hers","him","his","how","i","if","in","into","is","it","its","itself","just","let","like","lot","lots","make","makes","many",
        "may","me","more","most","much","must","my","new","no","nor","not","now","of","off","on","once","one","only","or","other",
        "our","ours","out","over","own","per","platform","same","she","should","so","some","such","than","that","the","their",
        "theirs","them","then","there","these","they","this","those","through","to","too","under","until","up","use","used","using",
        "very","via","was","we","well","were","what","when","where","which","while","who","whom","why","will","with","within",
        "without","would","you","your","yours","based","provide","provides","providing","simple","solution","problem","startup",
        "idea","people","users","user","across","around","way","ways","want","need","needs","better","best","good","great","first"
    };

    /// <summary>Lower-cased words (letters/digits), stopwords and 1–2 letter tokens removed, paired with their stem.</summary>
    public static IEnumerable<(string Stem, string Word)> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var sb = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant().Append(' '))
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); continue; }
            if (sb.Length == 0) continue;
            var word = sb.ToString();
            sb.Clear();
            if (word.Length < 3 || StopWords.Contains(word) || word.All(char.IsDigit)) continue;
            var stem = Stem(word);
            if (stem.Length < 3 || StopWords.Contains(stem)) continue;
            yield return (stem, word);
        }
    }

    /// <summary>Light suffix stripping (plurals, -ing, -ed, -ly, -ation…) — enough to join "farmers"/"farming"/"farm".</summary>
    public static string Stem(string w)
    {
        if (w.Length <= 4) return w.EndsWith('s') && !w.EndsWith("ss") && w.Length > 3 ? w[..^1] : w;
        string Strip(string suffix, string replacement = "") => w[..^suffix.Length] + replacement;

        if (w.EndsWith("ies") && w.Length > 5) w = Strip("ies", "y");
        else if (w.EndsWith("sses")) w = Strip("es");
        else if (w.EndsWith("es") && (w.EndsWith("ches") || w.EndsWith("shes") || w.EndsWith("xes"))) w = Strip("es");
        else if (w.EndsWith('s') && !w.EndsWith("ss") && !w.EndsWith("us") && !w.EndsWith("is")) w = w[..^1];

        foreach (var suffix in new[] { "isation", "ization", "ational", "ation", "ments", "ment", "ness", "ingly", "ing", "edly", "ed", "ers", "er", "ly", "ity", "ive", "al" })
        {
            if (w.EndsWith(suffix) && w.Length - suffix.Length >= 4)
            {
                w = w[..^suffix.Length];
                // "shopping" → "shopp" → "shop"; "planned" → "plann" → "plan"
                if (w.Length >= 4 && w[^1] == w[^2] && !"lsz".Contains(w[^1])) w = w[..^1];
                break;
            }
        }
        return w;
    }

    public static IdeaTermVector Vectorize(IdeaTextFields idea)
    {
        var terms = new Dictionary<string, double>();
        var wordCounts = new Dictionary<string, Dictionary<string, int>>();
        void Add(string? text, double weight)
        {
            foreach (var (stem, word) in Tokenize(text))
            {
                terms[stem] = terms.GetValueOrDefault(stem) + weight;
                if (!wordCounts.TryGetValue(stem, out var words)) wordCounts[stem] = words = new Dictionary<string, int>();
                words[word] = words.GetValueOrDefault(word) + 1;
            }
        }
        Add(idea.Title, TitleWeight);
        Add(idea.Tagline, TaglineWeight);
        Add(idea.ProblemStatement, ProblemWeight);
        Add(idea.Solution, SolutionWeight);
        Add(idea.Description, DescriptionWeight);
        Add(idea.TargetMarket, MarketWeight);
        Add(idea.BusinessModel, BusinessWeight);

        return new IdeaTermVector
        {
            IdeaId = idea.Id,
            CategoryId = idea.CategoryId,
            Terms = terms,
            Display = wordCounts.ToDictionary(kv => kv.Key, kv => kv.Value.OrderByDescending(w => w.Value).ThenBy(w => w.Key.Length).First().Key),
            MarketTerms = Tokenize(idea.TargetMarket).Select(t => t.Stem).ToHashSet()
        };
    }
}

public sealed class IdeaSimilarityResult
{
    public int IdeaId { get; init; }
    /// <summary>0–100.</summary>
    public int Score { get; init; }
    public double TextSimilarity { get; init; }
    public bool SameCategory { get; init; }
    public bool SimilarMarket { get; init; }
    public List<string> CommonKeywords { get; init; } = new();
}

/// <summary>
/// TF-IDF cosine similarity over weighted fields (pure; IDF computed from the candidate pool passed in).
/// Score = 100 × (0.75 × text cosine + 0.15 × same category + 0.10 × target-market overlap).
/// Ideas need some textual overlap to count as similar: category/market alone never produce a match.
/// </summary>
public static class IdeaSimilarityScorer
{
    public const double TextWeight = 0.75, CategoryWeight = 0.15, MarketWeight = 0.10;
    public const double MinTextSimilarity = 0.06;
    public const int MinScore = 12;

    public static Dictionary<string, double> InverseDocumentFrequencies(IReadOnlyCollection<IdeaTermVector> corpus)
    {
        var df = new Dictionary<string, int>();
        foreach (var doc in corpus)
            foreach (var term in doc.Terms.Keys)
                df[term] = df.GetValueOrDefault(term) + 1;
        var n = corpus.Count;
        // Smoothed IDF (always > 0) so small corpora still work.
        return df.ToDictionary(kv => kv.Key, kv => Math.Log(1.0 + (n + 1.0) / (kv.Value + 0.5)));
    }

    public static List<IdeaSimilarityResult> Rank(IdeaTermVector query, IReadOnlyCollection<IdeaTermVector> candidates,
        IReadOnlyDictionary<string, double> idf, int count)
    {
        var q = Weigh(query, idf, out var qNorm);
        if (qNorm == 0) return new List<IdeaSimilarityResult>();

        var results = new List<IdeaSimilarityResult>();
        foreach (var cand in candidates)
        {
            if (cand.IdeaId == query.IdeaId) continue;
            var d = Weigh(cand, idf, out var dNorm);
            if (dNorm == 0) continue;

            double dot = 0;
            var contributions = new List<(string Stem, double Value)>();
            foreach (var (term, wq) in q)
            {
                if (d.TryGetValue(term, out var wd))
                {
                    dot += wq * wd;
                    contributions.Add((term, wq * wd));
                }
            }
            var cosine = dot / (qNorm * dNorm);
            if (cosine < MinTextSimilarity) continue;

            var sameCategory = query.CategoryId == cand.CategoryId;
            var market = Jaccard(query.MarketTerms, cand.MarketTerms);
            var score = 100 * (TextWeight * cosine + CategoryWeight * (sameCategory ? 1 : 0) + MarketWeight * market);
            var rounded = (int)Math.Round(Math.Clamp(score, 0, 100));
            if (rounded < MinScore) continue;

            results.Add(new IdeaSimilarityResult
            {
                IdeaId = cand.IdeaId,
                Score = rounded,
                TextSimilarity = cosine,
                SameCategory = sameCategory,
                SimilarMarket = market >= 0.3,
                CommonKeywords = contributions.OrderByDescending(c => c.Value).Take(5)
                    .Select(c => query.Display.GetValueOrDefault(c.Stem) ?? c.Stem).ToList()
            });
        }
        return results.OrderByDescending(r => r.Score).ThenByDescending(r => r.TextSimilarity).Take(count).ToList();
    }

    private static Dictionary<string, double> Weigh(IdeaTermVector v, IReadOnlyDictionary<string, double> idf, out double norm)
    {
        var weights = new Dictionary<string, double>(v.Terms.Count);
        double sum = 0;
        foreach (var (term, tf) in v.Terms)
        {
            // Sub-linear TF so a word repeated in every field doesn't dominate.
            var w = (1 + Math.Log(tf)) * idf.GetValueOrDefault(term, 1.0);
            if (w <= 0) continue;
            weights[term] = w;
            sum += w * w;
        }
        norm = Math.Sqrt(sum);
        return weights;
    }

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var inter = a.Count(b.Contains);
        return inter / (double)(a.Count + b.Count - inter);
    }
}

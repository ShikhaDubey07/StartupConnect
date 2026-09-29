namespace StartupConnect.Services.Matching;

/// <summary>Broad kinds of work a startup team needs. Skills and "roles needed" are mapped onto these.</summary>
public enum RoleFamily
{
    Tech,
    Design,
    Growth,
    Sales,
    Finance,
    Operations,
    Domain
}

/// <summary>
/// Small, maintainable mapping from skill / role names to <see cref="RoleFamily"/>.
/// Profile skills and idea roles come from <c>ProfileViewModel.AvailableSkills</c> (exact matches below);
/// free-text team roles (e.g. "Backend engineer", "Growth lead") fall back to keyword matching.
/// Generic roles such as "Co-founder", "Member" or "Founder" map to nothing.
/// </summary>
public static class RoleFamilies
{
    private static readonly Dictionary<string, RoleFamily> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Developer"] = RoleFamily.Tech,
        ["Designer"] = RoleFamily.Design,
        ["Marketing"] = RoleFamily.Growth,
        ["Content Creator"] = RoleFamily.Growth,
        ["Sales"] = RoleFamily.Sales,
        ["Finance"] = RoleFamily.Finance,
        ["Investor"] = RoleFamily.Finance,
        ["Operations"] = RoleFamily.Operations,
        ["Medical Advisor"] = RoleFamily.Domain,
        ["Advisor"] = RoleFamily.Domain,
    };

    // Checked in order; first keyword contained in the (lower-cased) name wins.
    private static readonly (string Keyword, RoleFamily Family)[] Keywords =
    [
        ("develop", RoleFamily.Tech), ("engineer", RoleFamily.Tech), ("programm", RoleFamily.Tech), ("coder", RoleFamily.Tech),
        ("software", RoleFamily.Tech), ("backend", RoleFamily.Tech), ("frontend", RoleFamily.Tech), ("full-stack", RoleFamily.Tech),
        ("full stack", RoleFamily.Tech), (" cto ", RoleFamily.Tech), ("data scien", RoleFamily.Tech), ("devops", RoleFamily.Tech),
        ("mobile", RoleFamily.Tech), ("react", RoleFamily.Tech), (".net", RoleFamily.Tech), ("flutter", RoleFamily.Tech),
        ("design", RoleFamily.Design), (" ux", RoleFamily.Design), (" ui", RoleFamily.Design), ("illustrat", RoleFamily.Design),
        ("market", RoleFamily.Growth), ("growth", RoleFamily.Growth), ("content", RoleFamily.Growth), ("seo", RoleFamily.Growth),
        ("social media", RoleFamily.Growth), ("brand", RoleFamily.Growth), ("community", RoleFamily.Growth), (" pr ", RoleFamily.Growth),
        ("sales", RoleFamily.Sales), ("business development", RoleFamily.Sales), ("biz dev", RoleFamily.Sales), (" bd ", RoleFamily.Sales),
        ("partnership", RoleFamily.Sales), ("account manag", RoleFamily.Sales),
        ("financ", RoleFamily.Finance), ("account", RoleFamily.Finance), ("cfo", RoleFamily.Finance), ("fundrais", RoleFamily.Finance),
        ("invest", RoleFamily.Finance),
        ("operation", RoleFamily.Operations), ("logistic", RoleFamily.Operations), ("supply", RoleFamily.Operations),
        (" coo ", RoleFamily.Operations), ("project manag", RoleFamily.Operations), ("product manag", RoleFamily.Operations),
        ("medical", RoleFamily.Domain), ("doctor", RoleFamily.Domain), ("legal", RoleFamily.Domain), ("lawyer", RoleFamily.Domain),
        ("advisor", RoleFamily.Domain), ("expert", RoleFamily.Domain), ("agronom", RoleFamily.Domain), ("teacher", RoleFamily.Domain),
        ("researcher", RoleFamily.Domain)
    ];

    /// <summary>Human label used in match reasons ("Covers Growth & marketing").</summary>
    public static string Label(RoleFamily f) => f switch
    {
        RoleFamily.Tech => "Tech",
        RoleFamily.Design => "Design",
        RoleFamily.Growth => "Marketing & growth",
        RoleFamily.Sales => "Sales & biz-dev",
        RoleFamily.Finance => "Finance",
        RoleFamily.Operations => "Operations",
        RoleFamily.Domain => "Domain expertise",
        _ => f.ToString()
    };

    public static RoleFamily? Classify(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var trimmed = name.Trim();
        if (Exact.TryGetValue(trimmed, out var exact)) return exact;
        var lower = " " + trimmed.ToLowerInvariant() + " ";
        foreach (var (keyword, family) in Keywords)
        {
            if (lower.Contains(keyword)) return family;
        }
        return null;
    }

    public static HashSet<RoleFamily> ClassifyAll(IEnumerable<string?> names)
    {
        var set = new HashSet<RoleFamily>();
        foreach (var n in names)
        {
            if (Classify(n) is RoleFamily f) set.Add(f);
        }
        return set;
    }

    /// <summary>Canonical profile skill names that belong to any of <paramref name="families"/> (used to pre-filter in SQL).</summary>
    public static List<string> SkillNamesFor(IEnumerable<RoleFamily> families)
    {
        var wanted = families.ToHashSet();
        return Exact.Where(kv => wanted.Contains(kv.Value)).Select(kv => kv.Key).ToList();
    }
}

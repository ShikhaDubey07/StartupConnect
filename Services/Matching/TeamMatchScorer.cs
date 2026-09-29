using StartupConnect.Models;

namespace StartupConnect.Services.Matching;

/// <summary>A role one of the seeker's ideas still needs (needed roles minus roles already on its team).</summary>
public sealed record OpenRoleNeed(int IdeaId, string IdeaTitle, string RoleName, RoleFamily Family);

/// <summary>The person looking for teammates (or, in "for idea" mode, the idea they are staffing).</summary>
public sealed class MatchSeeker
{
    public IReadOnlyCollection<string> Skills { get; init; } = Array.Empty<string>();
    /// <summary>Industry category ids the seeker cares about (profile interests + their ideas' categories).</summary>
    public IReadOnlyDictionary<int, string> Categories { get; init; } = new Dictionary<int, string>();
    public TimeAvailability Availability { get; init; }
    public int HoursPerWeek { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public IReadOnlyList<OpenRoleNeed> OpenNeeds { get; init; } = Array.Empty<OpenRoleNeed>();
    /// <summary>True in Find Team's "for idea" mode: only the idea's needs count, the seeker's own skill gaps don't.</summary>
    public bool IdeaMode { get; init; }
}

/// <summary>A discoverable member being scored. Location must be null when the member hides it.</summary>
public sealed class MatchCandidate
{
    public string UserId { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Skills { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<int, string> Categories { get; init; } = new Dictionary<int, string>();
    public TimeAvailability Availability { get; init; }
    public int HoursPerWeek { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public bool IsVerified { get; init; }
    public int ProfileCompletion { get; init; }
    public DateTime? LastActiveAt { get; init; }
}

public enum MatchReasonKind { Covers, Complements, Domain, Commitment, Location, Quality }

public sealed record MatchReason(MatchReasonKind Kind, string Text)
{
    public string Icon => Kind switch
    {
        MatchReasonKind.Covers => "bi-puzzle-fill",
        MatchReasonKind.Complements => "bi-plus-circle",
        MatchReasonKind.Domain => "bi-bullseye",
        MatchReasonKind.Commitment => "bi-clock",
        MatchReasonKind.Location => "bi-geo-alt",
        _ => "bi-patch-check"
    };
}

public sealed class TeamMatchScore
{
    /// <summary>0–100, the plain sum of the component points below (no floors or caps).</summary>
    public int Score { get; init; }
    public double Complementarity { get; init; }
    public double Domain { get; init; }
    public double Commitment { get; init; }
    public double Location { get; init; }
    public double Quality { get; init; }
    public List<MatchReason> Reasons { get; init; } = new();
    /// <summary>Families this candidate brings that the seeker needs or lacks (for UI chips).</summary>
    public List<RoleFamily> Brings { get; init; } = new();
}

/// <summary>
/// Pure, dependency-free co-founder/team scorer (easy to unit test). Points out of 100:
/// <list type="bullet">
/// <item><b>Complementarity 45</b> — covering roles the seeker's ideas still need (up to 30) plus skill families the seeker
/// lacks (up to 15). With no open needs, the full 45 go to filling the seeker's gaps; in "for idea" mode the full 45 go to the
/// idea's open roles. Identical skill sets earn nothing.</item>
/// <item><b>Shared domain 20</b> — overlapping industry interests (1 shared = 14, 2+ = 20).</item>
/// <item><b>Commitment 15</b> — same availability (9) and similar weekly hours (≤5h apart 6, ≤15h 3).</item>
/// <item><b>Location 10</b> — same city 10, else same state 6 (only when the candidate shares location).</item>
/// <item><b>Quality 10</b> — verified founder 4, profile completion (≥80% 3, ≥50% 1.5), active in last 30 days 3 (90 days 1.5).</item>
/// </list>
/// </summary>
public static class TeamMatchScorer
{
    public const double ComplementarityMax = 45, NeedsMax = 30, GapMax = 15, DomainMax = 20, CommitmentMax = 15, LocationMax = 10, QualityMax = 10;

    public static TeamMatchScore Score(MatchSeeker seeker, MatchCandidate candidate, DateTime nowUtc)
    {
        var reasons = new List<MatchReason>();
        var seekerFamilies = RoleFamilies.ClassifyAll(seeker.Skills);
        var candidateFamilies = RoleFamilies.ClassifyAll(candidate.Skills);
        var brings = new List<RoleFamily>();

        // ---- Complementarity ----
        var openFamilies = seeker.OpenNeeds.Select(n => n.Family).Distinct().ToList();
        double needsPoints = 0, gapPoints = 0;
        if (openFamilies.Count > 0)
        {
            var covered = openFamilies.Where(candidateFamilies.Contains).ToList();
            // Covering two open needs (or the only one) earns the full share.
            var needsMax = seeker.IdeaMode ? ComplementarityMax : NeedsMax;
            needsPoints = needsMax * Math.Min(1.0, covered.Count / (double)Math.Min(2, openFamilies.Count));
            foreach (var family in covered)
            {
                // Prefer the idea whose needed role literally matches one of the candidate's skills.
                var familySkills = candidate.Skills.Where(sk => RoleFamilies.Classify(sk) == family).ToList();
                var need = seeker.OpenNeeds.Where(n => n.Family == family)
                    .OrderByDescending(n => familySkills.Contains(n.RoleName, StringComparer.OrdinalIgnoreCase))
                    .First();
                var exact = familySkills.Contains(need.RoleName, StringComparer.OrdinalIgnoreCase);
                var roleLabel = exact || familySkills.Count == 0 ? need.RoleName : $"{need.RoleName} ({familySkills[0]})";
                reasons.Add(new MatchReason(MatchReasonKind.Covers, $"Covers {roleLabel} — which your idea '{need.IdeaTitle}' needs"));
                brings.Add(family);
            }
        }

        var gapMax = openFamilies.Count > 0 ? GapMax : ComplementarityMax;
        if (!seeker.IdeaMode || openFamilies.Count == 0)
        {
            var newFamilies = candidateFamilies.Where(f => !seekerFamilies.Contains(f) && !brings.Contains(f)).ToList();
            // Families already counted as covered needs still show the seeker lacks them, so they count towards the gap too.
            var gapCount = candidateFamilies.Count(f => !seekerFamilies.Contains(f));
            gapPoints = gapMax * Math.Min(1.0, gapCount / 2.0);
            if (newFamilies.Count > 0)
            {
                var labels = newFamilies.Select(f => candidate.Skills.FirstOrDefault(s => RoleFamilies.Classify(s) == f) ?? RoleFamilies.Label(f));
                reasons.Add(new MatchReason(MatchReasonKind.Complements,
                    seekerFamilies.Count > 0 ? $"Adds skills you don't have: {string.Join(", ", labels)}" : $"Brings {string.Join(", ", labels)}"));
                brings.AddRange(newFamilies);
            }
        }
        var complementarity = Math.Min(ComplementarityMax, needsPoints + gapPoints);

        // ---- Shared domain ----
        var shared = candidate.Categories.Where(c => seeker.Categories.ContainsKey(c.Key)).Select(c => c.Value).ToList();
        var domain = shared.Count switch { 0 => 0, 1 => 14, _ => DomainMax };
        if (shared.Count > 0)
            reasons.Add(new MatchReason(MatchReasonKind.Domain, $"Both into {JoinNatural(shared.Take(3).ToList())}"));

        // ---- Commitment ----
        double commitment = 0;
        if (candidate.Availability == seeker.Availability)
        {
            commitment += 9;
            reasons.Add(new MatchReason(MatchReasonKind.Commitment, $"Same commitment: both {AvailabilityLabel(seeker.Availability)}"));
        }
        var hourDiff = Math.Abs(candidate.HoursPerWeek - seeker.HoursPerWeek);
        if (hourDiff <= 5) commitment += 6;
        else if (hourDiff <= 15) commitment += 3;
        if (candidate.Availability != seeker.Availability && hourDiff <= 5 && candidate.HoursPerWeek > 0)
            reasons.Add(new MatchReason(MatchReasonKind.Commitment, $"Similar hours: ~{candidate.HoursPerWeek}h/week"));

        // ---- Location (bonus, never dominant) ----
        double location = 0;
        if (!string.IsNullOrWhiteSpace(candidate.City) && string.Equals(candidate.City?.Trim(), seeker.City?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            location = LocationMax;
            reasons.Add(new MatchReason(MatchReasonKind.Location, $"Same city: {candidate.City!.Trim()}"));
        }
        else if (!string.IsNullOrWhiteSpace(candidate.State) && string.Equals(candidate.State?.Trim(), seeker.State?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            location = 6;
            reasons.Add(new MatchReason(MatchReasonKind.Location, $"Same state: {candidate.State!.Trim()}"));
        }

        // ---- Quality tie-breakers ----
        double quality = 0;
        if (candidate.IsVerified)
        {
            quality += 4;
            reasons.Add(new MatchReason(MatchReasonKind.Quality, "Verified founder"));
        }
        quality += candidate.ProfileCompletion >= 80 ? 3 : candidate.ProfileCompletion >= 50 ? 1.5 : 0;
        if (candidate.LastActiveAt is DateTime last)
        {
            var days = (nowUtc - last).TotalDays;
            quality += days <= 30 ? 3 : days <= 90 ? 1.5 : 0;
        }

        var total = complementarity + domain + commitment + location + quality;
        return new TeamMatchScore
        {
            Score = (int)Math.Round(Math.Clamp(total, 0, 100), MidpointRounding.AwayFromZero),
            Complementarity = complementarity,
            Domain = domain,
            Commitment = commitment,
            Location = location,
            Quality = quality,
            Reasons = reasons,
            Brings = brings.Distinct().ToList()
        };
    }

    /// <summary>
    /// Open needs of an idea: families of its needed roles minus families already held by team members (their team role
    /// or, failing that, their profile skills). The founder's own skills don't close a need — the founder listed it.
    /// </summary>
    public static List<OpenRoleNeed> OpenNeeds(int ideaId, string ideaTitle, IEnumerable<string> rolesNeeded, IEnumerable<string> teamRoles)
    {
        var filled = RoleFamilies.ClassifyAll(teamRoles);
        var result = new List<OpenRoleNeed>();
        foreach (var role in rolesNeeded.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (RoleFamilies.Classify(role) is RoleFamily f && !filled.Contains(f) && result.All(r => r.Family != f))
                result.Add(new OpenRoleNeed(ideaId, ideaTitle, role, f));
        }
        return result;
    }

    public static string AvailabilityLabel(TimeAvailability a) => a switch
    {
        TimeAvailability.FullTime => "full-time",
        TimeAvailability.PartTime => "part-time",
        TimeAvailability.WeekendsOnly => "weekends only",
        _ => a.ToString()
    };

    private static string JoinNatural(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " & " + items[^1]
    };
}

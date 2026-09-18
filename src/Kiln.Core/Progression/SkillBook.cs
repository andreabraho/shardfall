namespace Kiln.Core.Progression;

/// <summary>Mastery ranks, kept from the original (doc 01 §2.2).</summary>
public enum MasteryRank
{
    Normal,
    Master,
    GrandMaster,
    Perfect,
}

/// <summary>What the player knows, and how well (PRG-03/04/05).</summary>
public sealed class SkillBook
{
    /// <summary>Uses required to reach each rank.</summary>
    public const int MasterUses = 80;
    public const int GrandMasterUses = 240;
    public const int PerfectUses = 600;

    private sealed class Entry
    {
        public int Uses { get; set; }
    }

    private readonly Dictionary<string, Entry> _known = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Known => _known.Keys;

    public int Count => _known.Count;

    public bool IsUnlocked(string skillId) => _known.ContainsKey(skillId);

    /// <summary>
    /// Learns a skill. Returns false if already known — the caller should not have spent a
    /// point.
    /// </summary>
    public bool Unlock(string skillId)
    {
        if (string.IsNullOrEmpty(skillId) || _known.ContainsKey(skillId)) return false;

        _known[skillId] = new Entry();
        return true;
    }

    public bool Forget(string skillId) => _known.Remove(skillId);

    public void Clear() => _known.Clear();

    /// <summary>
    /// Records a cast toward mastery.
    /// <para>
    /// Mastery comes from use rather than from rare books with a random success chance.
    /// The original's book gating is a grind and monetisation lever, not a design — but the
    /// long tail of a skill slowly becoming stronger is worth keeping, so it is earned by
    /// playing the skill instead (doc 02 §5).
    /// </para>
    /// </summary>
    /// <returns>The new rank when this use caused a promotion, otherwise null.</returns>
    public MasteryRank? RecordUse(string skillId)
    {
        if (!_known.TryGetValue(skillId, out var entry)) return null;

        var before = RankFor(entry.Uses);
        entry.Uses++;
        var after = RankFor(entry.Uses);

        return after != before ? after : null;
    }

    public int UsesOf(string skillId) => _known.TryGetValue(skillId, out var e) ? e.Uses : 0;

    public MasteryRank RankOf(string skillId) =>
        _known.TryGetValue(skillId, out var e) ? RankFor(e.Uses) : MasteryRank.Normal;

    /// <summary>Uses still needed for the next rank. Zero once Perfect.</summary>
    public int UsesToNextRank(string skillId)
    {
        if (!_known.TryGetValue(skillId, out var entry)) return MasterUses;

        return RankFor(entry.Uses) switch
        {
            MasteryRank.Normal => MasterUses - entry.Uses,
            MasteryRank.Master => GrandMasterUses - entry.Uses,
            MasteryRank.GrandMaster => PerfectUses - entry.Uses,
            _ => 0,
        };
    }

    private static MasteryRank RankFor(int uses) => uses switch
    {
        >= PerfectUses => MasteryRank.Perfect,
        >= GrandMasterUses => MasteryRank.GrandMaster,
        >= MasterUses => MasteryRank.Master,
        _ => MasteryRank.Normal,
    };

    /// <summary>Restores saved state (doc 06 §9).</summary>
    public void Load(IReadOnlyDictionary<string, int> uses)
    {
        _known.Clear();

        foreach (var (id, count) in uses)
        {
            _known[id] = new Entry { Uses = Math.Max(0, count) };
        }
    }

    public Dictionary<string, int> Save()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (id, entry) in _known)
        {
            result[id] = entry.Uses;
        }

        return result;
    }
}

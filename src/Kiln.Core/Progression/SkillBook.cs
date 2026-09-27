namespace Kiln.Core.Progression;

/// <summary>Mastery ranks, kept from the original (doc 01 §2.2).</summary>
public enum MasteryRank
{
    Normal,
    Master,
    GrandMaster,
    Perfect,
}

/// <summary>
/// What the player knows, and how well (PRG-03/04/05, settled in REF-03).
/// </summary>
/// <remarks>
/// Two currencies, in this order. <b>Skill points</b>, one per level, are spent on a skill up
/// to <see cref="MaxPoints"/>: the first point learns it, each one after makes it stronger,
/// and the seventh promotes it to <see cref="MasteryRank.Master"/>. <b>Use</b> takes it the
/// rest of the way — Grand Master and then Perfect are earned by casting a skill that is
/// already mastered.
/// <para>
/// The split is what makes both halves mean something. Points alone would let a player buy a
/// finished skill at the level it unlocks; uses alone would make the choice of what to invest
/// in meaningless, since everything would eventually rank up anyway. Points decide <em>what
/// you commit to</em>, and the long tail after Master is paid for by actually playing it.
/// </para>
/// </remarks>
public sealed class SkillBook
{
    /// <summary>Points a single skill can hold. The last one masters it.</summary>
    public const int MaxPoints = 7;

    /// <summary>
    /// Casts of a mastered skill needed for Grand Master, and then for Perfect (counted from
    /// Master). Cut from 200 and 600 on 2026-09-25 at your call: the long tail was too long.
    /// </summary>
    public const int GrandMasterUses = 20;
    public const int PerfectUses = 35;

    /// <summary>
    /// The most skills a character can know at once (2026-09-26, at your call): six, one for
    /// each key on the bar. Which six is the build; a shrine refunds them to choose again.
    /// </summary>
    public const int MaxSkills = 6;

    /// <summary>
    /// Skills every character knows from the start, at one point that costs nothing, and that do
    /// not count toward <see cref="MaxSkills"/>: the Guard, which has a key of its own
    /// (2026-09-27, at your call: it was on Shift from the start but unusable until bought).
    /// </summary>
    private readonly HashSet<string> _free;

    public SkillBook(IEnumerable<string>? free = null)
    {
        _free = new HashSet<string>(free ?? [], StringComparer.Ordinal);
        GrantFree();
    }

    /// <summary>Puts every free skill back at its one point, where it is missing.</summary>
    private void GrantFree()
    {
        foreach (var id in _free)
        {
            if (!_known.ContainsKey(id)) _known[id] = new Entry { Points = 1 };
        }
    }

    private sealed class Entry
    {
        public int Uses { get; set; }

        public int Points { get; set; }
    }

    private readonly Dictionary<string, Entry> _known = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Known => _known.Keys;

    public int Count => _known.Count;

    public bool IsUnlocked(string skillId) => _known.ContainsKey(skillId);

    /// <summary>How many of the <see cref="MaxSkills"/> places are taken.</summary>
    public int Learned => _known.Keys.Count(id => !_free.Contains(id));

    /// <summary>
    /// Whether a point could go into this skill as far as the limit goes: it is known already,
    /// it is free, or there is still a place for it.
    /// </summary>
    public bool CanLearn(string skillId) =>
        _known.ContainsKey(skillId) || _free.Contains(skillId) || Learned < MaxSkills;

    /// <summary>Points invested in a skill. Zero for one that has not been learned.</summary>
    public int PointsIn(string skillId) => _known.TryGetValue(skillId, out var e) ? e.Points : 0;

    public bool IsFullyInvested(string skillId) => PointsIn(skillId) >= MaxPoints;

    /// <summary>
    /// Spends a point on a skill, learning it if this is the first. False when it is already
    /// full, or when it would be a seventh skill — the caller must not take the point off the
    /// player in either case.
    /// </summary>
    public bool Invest(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return false;

        if (!_known.TryGetValue(skillId, out var entry))
        {
            if (!CanLearn(skillId)) return false;

            _known[skillId] = new Entry { Points = 1 };
            return true;
        }

        if (entry.Points >= MaxPoints) return false;

        entry.Points++;
        return true;
    }

    /// <summary>
    /// Learns a skill outright, at one point. For content that grants a skill rather than
    /// selling it — a quest reward, a starting kit.
    /// </summary>
    public bool Unlock(string skillId) => !_known.ContainsKey(skillId) && Invest(skillId);

    public bool Forget(string skillId) => _known.Remove(skillId);

    public void Clear()
    {
        _known.Clear();
        GrantFree();
    }

    /// <summary>
    /// Returns every point spent, for a respec (FR-2.6). The skills are forgotten with them.
    /// </summary>
    public int Refund()
    {
        // The free point on a free skill was never the player's to spend, so it stays.
        var points = _known.Sum(p => p.Value.Points - (_free.Contains(p.Key) ? 1 : 0));

        _known.Clear();
        GrantFree();

        return points;
    }

    /// <summary>
    /// Records a cast toward mastery.
    /// <para>
    /// Only a mastered skill counts. Before that the rank is bought with points, so counting
    /// early casts would mean a skill the player ground out at one point jumped straight past
    /// Master the moment the seventh point landed.
    /// </para>
    /// <para>
    /// Mastery comes from use rather than from rare books with a random success chance. The
    /// original's book gating is a grind and a monetisation lever, not a design — but the long
    /// tail of a skill slowly becoming stronger is worth keeping (doc 02 §5).
    /// </para>
    /// </summary>
    /// <returns>The new rank when this use caused a promotion, otherwise null.</returns>
    public MasteryRank? RecordUse(string skillId)
    {
        if (!_known.TryGetValue(skillId, out var entry) || entry.Points < MaxPoints) return null;

        var before = RankFor(entry);
        entry.Uses++;
        var after = RankFor(entry);

        return after != before ? after : null;
    }

    public int UsesOf(string skillId) => _known.TryGetValue(skillId, out var e) ? e.Uses : 0;

    public MasteryRank RankOf(string skillId) =>
        _known.TryGetValue(skillId, out var e) ? RankFor(e) : MasteryRank.Normal;

    /// <summary>
    /// What is still owed for the next rank: points while the skill is being bought up, then
    /// casts. Zero once Perfect.
    /// </summary>
    public int ToNextRank(string skillId)
    {
        if (!_known.TryGetValue(skillId, out var entry)) return 1;

        if (entry.Points < MaxPoints) return MaxPoints - entry.Points;

        return RankFor(entry) switch
        {
            MasteryRank.Master => GrandMasterUses - entry.Uses,
            MasteryRank.GrandMaster => PerfectUses - entry.Uses,
            _ => 0,
        };
    }

    /// <summary>True while the next rank is bought with points rather than earned by casting.</summary>
    public bool NextRankCostsPoints(string skillId) => PointsIn(skillId) < MaxPoints;

    private static MasteryRank RankFor(Entry entry)
    {
        if (entry.Points < MaxPoints) return MasteryRank.Normal;

        return entry.Uses switch
        {
            >= PerfectUses => MasteryRank.Perfect,
            >= GrandMasterUses => MasteryRank.GrandMaster,
            _ => MasteryRank.Master,
        };
    }

    /// <summary>
    /// Restores saved state (doc 06 §9).
    /// </summary>
    /// <remarks>
    /// A save written before points existed carries uses only. Those skills come back at one
    /// point — learned, at the bottom of the ladder — rather than unusable, which is what a
    /// missing entry would mean.
    /// </remarks>
    public void Load(IReadOnlyDictionary<string, int> uses, IReadOnlyDictionary<string, int>? points = null)
    {
        _known.Clear();

        foreach (var (id, count) in uses)
        {
            _known[id] = new Entry
            {
                Uses = Math.Max(0, count),
                Points = Math.Clamp(points?.GetValueOrDefault(id, 1) ?? 1, 1, MaxPoints),
            };
        }

        // A save from before the Guard came free has none; it gets its point like anyone.
        GrantFree();
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

    public Dictionary<string, int> SavePoints()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (id, entry) in _known)
        {
            result[id] = entry.Points;
        }

        return result;
    }
}

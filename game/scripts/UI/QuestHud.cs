using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Quests;
using Kiln.Game.Items;
using Kiln.Game.Quests;
using Kiln.Game.World;

namespace Kiln.Game.UI;

/// <summary>
/// The current objective, top right (FR-8.3). There is no journal; this is the whole of it.
/// </summary>
/// <remarks>
/// It says three things: which quest, how far along, and where. The last one matters most for
/// a hunt — "twelve pale gnawers" is useless to a player who does not know which map has them,
/// so when the creature lives somewhere else the panel names the place.
/// </remarks>
public partial class QuestHud : CanvasLayer
{
    private static readonly Color Title = new(0.96f, 0.86f, 0.58f);
    private static readonly Color Body = new(0.88f, 0.86f, 0.8f);
    private static readonly Color Dim = new(0.66f, 0.64f, 0.6f);
    private static readonly Color Done = new(0.5f, 0.8f, 0.52f);

    private PanelContainer _panel = null!;
    private Label _name = null!;
    private VBoxContainer _goals = null!;

    public override void _Ready()
    {
        Layer = 16;

        _panel = new PanelContainer
        {
            Name = "Panel",
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -300,
            OffsetRight = -16,
            OffsetTop = 16,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);

        var heading = Line(L10n.T("QUEST"), 11, Dim);
        _name = Line("", 16, Title);
        _goals = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };

        box.AddChild(heading);
        box.AddChild(_name);
        box.AddChild(_goals);

        _panel.AddChild(box);
        AddChild(_panel);

        QuestTracker.Changed += Refresh;

        // Deferred: the zone has not entered itself yet, and "where" is read relative to it.
        CallDeferred(nameof(Refresh));
    }

    /// <summary>
    /// Lets go of the static event. The HUD is rebuilt with every scene and the event is not,
    /// so a subscription left behind is a freed panel the next kill tries to update — the
    /// same leak that once broke loot pickup.
    /// </summary>
    public override void _ExitTree() => QuestTracker.Changed -= Refresh;

    private static Label Line(string text, int size, Color colour)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };

        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);

        return label;
    }

    private void Refresh()
    {
        if (!GameContent.IsLoaded || !IsInsideTree()) return;

        var chain = PlayerProfile.Quests;

        if (chain.Active is not { } quest)
        {
            _panel.Visible = false;
            return;
        }

        _panel.Visible = true;
        _name.Text = QuestTracker.Name(quest);

        foreach (var child in _goals.GetChildren()) child.QueueFree();

        for (var i = 0; i < quest.Goals.Count; i++)
        {
            var goal = quest.Goals[i];
            var have = i < chain.Progress.Count ? chain.Progress[i] : 0;
            var done = have >= goal.Count;

            _goals.AddChild(Line(Describe(goal, have), 14, done ? Done : Body));

            if (!done && Where(goal) is { Length: > 0 } where)
            {
                _goals.AddChild(Line(where, 12, Dim));
            }
        }
    }

    private static string Describe(QuestGoal goal, int have) => goal.Type switch
    {
        ObjectiveType.Kill => $"{GameItems.NameOfEnemy(goal.Target)}   {have} / {goal.Count}",
        ObjectiveType.ClearTower => L10n.F("Clear all {0} floors of {1}", FloorsOf(goal.Target), ZoneName(goal.Target)),
        ObjectiveType.Reach => L10n.F("Travel to {0}", ZoneName(goal.Target)),
        ObjectiveType.Shard => L10n.F("Break {0}", GameItems.Localise($"$shard.{goal.Target}.name")),
        _ => goal.Target,
    };

    /// <summary>
    /// Where the goal can be done, when that is not here.
    /// </summary>
    /// <remarks>
    /// For a hunt it is every map that has a camp of that creature. Said only when the player
    /// is not already standing in one: "in the Ridge" while on the Ridge is noise.
    /// </remarks>
    private static string Where(QuestGoal goal)
    {
        var here = GameWorld.CurrentZoneId;
        var zones = QuestPlaces.ZonesFor(goal);

        if (zones.Count == 0) return "";

        if (zones.Contains(here)) return L10n.T("here  ·  marked on the map (M)");

        return L10n.F("in {0}  ·  the way is on the map (M)", string.Join(L10n.T(" or "), zones.Select(ZoneName)));
    }

    private static string ZoneName(string zoneId) =>
        GameWorld.Graph[zoneId] is { } zone ? GameItems.Localise(zone.Name) : zoneId;

    private static int FloorsOf(string zoneId) => GameWorld.Graph[zoneId]?.Floors.Count ?? 0;
}

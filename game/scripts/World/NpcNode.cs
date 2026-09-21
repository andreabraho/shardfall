using Godot;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.World;

/// <summary>
/// A villager standing in the world (QST-06): the elder, the smith, the merchant.
/// </summary>
/// <remarks>
/// Placed by the scene, described by <c>data/npcs</c>, the same split as a shrine or a camp:
/// where someone stands is level design, what they say and sell is content.
/// <para>
/// Talking is the interact key at close range, like every other thing in the world that does
/// something when asked. The plate says so while the player is close enough, because a
/// villager who can be talked to looks exactly like one who cannot.
/// </para>
/// </remarks>
public partial class NpcNode : Area3D
{
    private Combat.NamePlate? _plate;
    private NpcDef? _def;
    private bool _playerInside;

    [Export] public string NpcId { get; set; } = "";

    [Export] public float Radius { get; set; } = 2.6f;

    public NpcDef? Def => _def;

    public override void _Ready()
    {
        AddToGroup("npcs");

        CollisionLayer = 0;
        CollisionMask = Foundation.Layers.Player;
        Monitoring = true;

        AddChild(new CollisionShape3D
        {
            Name = "Range",
            Shape = new SphereShape3D { Radius = Radius },
            Position = new Vector3(0, 1.0f, 0),
        });

        if (!GameContent.IsLoaded || !GameContent.Database.Npcs.TryGetValue(NpcId, out _def))
        {
            GD.PushWarning($"[npc] '{NpcId}' is not in data/npcs; standing in as nobody.");
            return;
        }

        AddChild(new Visual.VisualRoot
        {
            Name = "VisualRoot",
            VisualId = _def.Visual,
            TintOverride = _def.Tint ?? "",
            Position = new Vector3(0, 0.9f, 0),
        });

        _plate = new Combat.NamePlate
        {
            Name = "NamePlate",
            Offset = new Vector3(0, 2.3f, 0),
            Rank = Combat.NameRank.Elite,
            Tint = new Color("e6d7a8"),
        };

        AddChild(_plate);
        Relabel();

        BodyEntered += body =>
        {
            if (!body.IsInGroup("player")) return;

            _playerInside = true;
            Relabel();
        };

        BodyExited += body =>
        {
            if (!body.IsInGroup("player")) return;

            _playerInside = false;
            Relabel();
        };
    }

    public string DisplayName => _def is null ? NpcId : GameItems.Localise(_def.Name);

    private void Relabel()
    {
        if (_def is null) return;

        var title = string.IsNullOrEmpty(_def.Title) ? "" : $"  ·  {_def.Title}";

        _plate?.SetText(_playerInside ? $"{DisplayName}{title}   [F]" : $"{DisplayName}{title}");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside || _def is null || !@event.IsActionPressed(GameActions.Interact)) return;
        if (UI.UiState.ModalOpen) return;

        Talk();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Whatever this villager does when spoken to.</summary>
    public void Talk()
    {
        if (_def is null) return;

        var session = GetTree().CurrentScene?.GetNodeOrNull("Session");

        switch (_def.Role)
        {
            case NpcRole.Merchant when session?.GetNodeOrNull<UI.MerchantPanel>("MerchantPanel") is { } shop:
                shop.Open(_def, Vendors.For(_def), NextLine());
                break;

            case NpcRole.Smith when session?.GetNodeOrNull<UI.WorkbenchPanel>("WorkbenchPanel") is { } bench:
                bench.Open();
                UI.WorldNotice.Show(GetTree(), $"{DisplayName}: {NextLine()}");
                break;

            default:
                session?.GetNodeOrNull<UI.DialoguePanel>("DialoguePanel")?.Say(DisplayName, _def.Title, Conversation());
                break;
        }
    }

    /// <summary>
    /// What is said this time: the line for the quest in hand, if there is one, then the next
    /// of the usual lines.
    /// </summary>
    /// <remarks>
    /// One ordinary line per conversation, in turn, rather than all of them every time. A
    /// villager who recites four lines on every visit is a villager the player stops visiting.
    /// </remarks>
    private string[] Conversation()
    {
        if (_def is null) return [];

        var lines = new System.Collections.Generic.List<string>();

        if (PlayerProfile.Quests.Active is { } quest && _def.QuestLines.TryGetValue(quest.Id, out var about))
        {
            lines.Add(about);
        }

        if (NextLine() is { Length: > 0 } line) lines.Add(line);

        return [.. lines];
    }

    private static readonly System.Collections.Generic.Dictionary<string, int> Said = new(System.StringComparer.Ordinal);

    private string NextLine()
    {
        if (_def is null || _def.Lines.Length == 0) return "";

        var next = Said.GetValueOrDefault(_def.Id);

        Said[_def.Id] = next + 1;

        return _def.Lines[next % _def.Lines.Length];
    }
}

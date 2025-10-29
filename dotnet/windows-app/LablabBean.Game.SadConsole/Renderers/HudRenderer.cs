using Arch.Core;
using Arch.Core.Extensions;
using LablabBean.Game.Core.Components;
using LablabBean.Game.Core.Systems;
using SadConsole;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace LablabBean.Game.SadConsole.Renderers;

/// <summary>
/// Renders the HUD using SadConsole
/// </summary>
public class HudRenderer
{
    private readonly ControlsConsole _console;
    private readonly Label _healthLabel;
    private readonly Label _effectsLabel;
    private readonly Label _statsLabel;
    private readonly ListBox _messageList;
    private readonly List<string> _messages;
    private CombatSystem? _combatSystem;

    public ControlsConsole Console => _console;

    public HudRenderer(int width, int height)
    {
        _console = new ControlsConsole(width, height);
        _messages = new List<string>();

        // Health label
        _healthLabel = new Label(width - 2)
        {
            Position = new Point(1, 1),
            DisplayText = "Health: --/--"
        };

        // Effects label (between health and stats)
        _effectsLabel = new Label(width - 2)
        {
            Position = new Point(1, 3),
            DisplayText = ""
        };

        // Stats label
        _statsLabel = new Label(width - 2)
        {
            Position = new Point(1, 7),
            DisplayText = "Stats:\n  ATK: --\n  DEF: --\n  SPD: --"
        };

        // Message list
        _messageList = new ListBox(width - 2, height - 12)
        {
            Position = new Point(1, 13)
        };

        _console.Controls.Add(_healthLabel);
        _console.Controls.Add(_effectsLabel);
        _console.Controls.Add(_statsLabel);
        _console.Controls.Add(_messageList);

        // Draw border
        _console.Surface.DrawBox(new Rectangle(0, 0, width, height),
            ShapeParameters.CreateStyledBox(ICellSurface.ConnectedLineThin,
            new ColoredGlyph(Color.White, Color.Black)));
    }

    /// <summary>
    /// Sets the combat system for calculating modified stats
    /// </summary>
    public void SetCombatSystem(CombatSystem combatSystem)
    {
        _combatSystem = combatSystem;
    }

    /// <summary>
    /// Updates the HUD with current player information
    /// </summary>
    public void Update(World world)
    {
        var query = new QueryDescription().WithAll<Player, Health, Combat, Actor>();

        world.Query(in query, (Entity entity, ref Player player, ref Health health, ref Combat combat, ref Actor actor) =>
        {
            UpdatePlayerStats(entity, player.Name, health, combat, actor);
            UpdateStatusEffects(entity, world);
        });
    }

    /// <summary>
    /// Updates player stats display
    /// </summary>
    private void UpdatePlayerStats(Entity entity, string playerName, Health health, Combat combat, Actor actor)
    {
        _healthLabel.DisplayText = $"Health: {health.Current}/{health.Maximum}\n" +
                                   $"HP%: {health.Percentage:P0}";

        // Show base stats and modified stats if combat system is available
        if (_combatSystem != null && entity.Has<StatusEffects>())
        {
            var statusEffects = entity.Get<StatusEffects>();

            // Only show modified values if there are stat-modifying effects
            bool hasStatModifiers = statusEffects.ActiveEffects.Any(e =>
                e.Type == EffectType.Strength || e.Type == EffectType.Weakness ||
                e.Type == EffectType.IronSkin || e.Type == EffectType.Fragile ||
                e.Type == EffectType.Haste || e.Type == EffectType.Slow);

            if (hasStatModifiers)
            {
                int modifiedAttack = _combatSystem.GetModifiedAttack(entity, combat.Attack);
                int modifiedDefense = _combatSystem.GetModifiedDefense(entity, combat.Defense);
                int modifiedSpeed = _combatSystem.GetModifiedSpeed(entity, actor.Speed);

                _statsLabel.DisplayText = $"Stats:\n" +
                                          $"  ATK: {combat.Attack}{GetStatDiff(combat.Attack, modifiedAttack)}\n" +
                                          $"  DEF: {combat.Defense}{GetStatDiff(combat.Defense, modifiedDefense)}\n" +
                                          $"  SPD: {actor.Speed}{GetStatDiff(actor.Speed, modifiedSpeed)}\n" +
                                          $"  NRG: {actor.Energy}";
                return;
            }
        }

        // Default display without modifiers
        _statsLabel.DisplayText = $"Stats:\n" +
                                  $"  ATK: {combat.Attack}\n" +
                                  $"  DEF: {combat.Defense}\n" +
                                  $"  SPD: {actor.Speed}\n" +
                                  $"  NRG: {actor.Energy}";
    }

    /// <summary>
    /// Formats the difference between base and modified stat
    /// </summary>
    private string GetStatDiff(int baseStat, int modifiedStat)
    {
        int diff = modifiedStat - baseStat;
        if (diff == 0) return "";
        if (diff > 0) return $" (+{diff})";
        return $" ({diff})";
    }

    /// <summary>
    /// Updates status effects display
    /// </summary>
    private void UpdateStatusEffects(Entity entity, World world)
    {
        if (!entity.Has<StatusEffects>())
        {
            _effectsLabel.DisplayText = "";
            return;
        }

        var statusEffects = entity.Get<StatusEffects>();

        if (statusEffects.ActiveEffects.Count == 0)
        {
            _effectsLabel.DisplayText = "";
            return;
        }

        var effectLines = new List<string> { "Effects:" };

        foreach (var effect in statusEffects.ActiveEffects)
        {
            var iconAndName = GetEffectIcon(effect.Type) + " " + effect.DisplayName;
            var turnsLeft = $"({effect.Duration})";
            effectLines.Add($"  {iconAndName} {turnsLeft}");
        }

        _effectsLabel.DisplayText = string.Join("\n", effectLines);
    }

    /// <summary>
    /// Gets a visual icon for each effect type
    /// </summary>
    private string GetEffectIcon(EffectType effectType)
    {
        return effectType switch
        {
            EffectType.Poison => "?",
            EffectType.Regeneration => "?",
            EffectType.Haste => "?",
            EffectType.Strength => "??",
            EffectType.IronSkin => "??",
            EffectType.Bleed => "??",
            EffectType.Burning => "??",
            EffectType.Blessed => "?",
            EffectType.Weakness => "?",
            EffectType.Slow => "??",
            EffectType.Fragile => "??",
            _ => "�E"
        };
    }

    /// <summary>
    /// Adds a message to the message log
    /// </summary>
    public void AddMessage(string message)
    {
        _messages.Add(message);

        // Keep only last 100 messages
        if (_messages.Count > 100)
        {
            _messages.RemoveAt(0);
        }

        // Update list box
        _messageList.Items.Clear();
        foreach (var msg in _messages)
        {
            _messageList.Items.Add(msg);
        }

        // Scroll to bottom
        if (_messageList.Items.Count > 0)
        {
            _messageList.SelectedIndex = _messageList.Items.Count - 1;
        }
    }

    /// <summary>
    /// Clears all messages
    /// </summary>
    public void ClearMessages()
    {
        _messages.Clear();
        _messageList.Items.Clear();
    }
}

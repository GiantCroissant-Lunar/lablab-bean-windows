using Arch.Core;
using Arch.Core.Extensions;
using LablabBean.Game.Core.Components;
using LablabBean.Plugins.Contracts;
using SadConsole;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace LablabBean.Game.SadConsole.UI;

/// <summary>
/// Overlay UI for casting spells
/// </summary>
public class SpellCastingOverlay
{
    private readonly ControlsConsole _console;
    private readonly ListBox _spellList;
    private readonly Label _titleLabel;
    private readonly Label _instructionsLabel;
    private readonly IRegistry? _registry;
    private Entity _playerEntity;
    private bool _isVisible;

    public ControlsConsole Console => _console;
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            _console.IsVisible = value;
        }
    }

    public SpellCastingOverlay(int width, int height, IRegistry? registry)
    {
        _registry = registry;
        int overlayWidth = 50;
        int overlayHeight = 25;
        int posX = (width - overlayWidth) / 2;
        int posY = (height - overlayHeight) / 2;

        _console = new ControlsConsole(overlayWidth, overlayHeight)
        {
            Position = new Point(posX, posY),
            IsVisible = false
        };

        // Title
        _titleLabel = new Label(overlayWidth - 4)
        {
            Position = new Point(2, 1),
            DisplayText = "Cast Spell"
        };

        // Instructions
        _instructionsLabel = new Label(overlayWidth - 4)
        {
            Position = new Point(2, 3),
            DisplayText = "Select a spell to cast (ESC to cancel)"
        };

        // Spell list
        _spellList = new ListBox(overlayWidth - 4, overlayHeight - 10)
        {
            Position = new Point(2, 5)
        };

        _console.Controls.Add(_titleLabel);
        _console.Controls.Add(_instructionsLabel);
        _console.Controls.Add(_spellList);

        // Draw border
        _console.Surface.DrawBox(new Rectangle(0, 0, overlayWidth, overlayHeight),
            ShapeParameters.CreateStyledBox(ICellSurface.ConnectedLineThin,
            new ColoredGlyph(Color.Cyan, Color.Black)));
    }

    /// <summary>
    /// Shows the spell casting UI for the given player
    /// </summary>
    public void Show(Entity playerEntity)
    {
        _playerEntity = playerEntity;
        RefreshSpellList();
        IsVisible = true;
    }

    /// <summary>
    /// Hides the spell casting UI
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
    }

    /// <summary>
    /// Refreshes the spell list with available spells
    /// </summary>
    private void RefreshSpellList()
    {
        _spellList.Items.Clear();

        try
        {
            if (_registry == null)
            {
                _spellList.Items.Add("No spells available (plugin not loaded)");
                return;
            }

            // Use reflection to get SpellService
            var spellServiceType = Type.GetType("LablabBean.Plugins.Spell.Services.SpellService, LablabBean.Plugins.Spell");
            if (spellServiceType == null)
            {
                _spellList.Items.Add("No spells available (plugin not loaded)");
                return;
            }

            var getMethod = typeof(IRegistry).GetMethod("Get")?.MakeGenericMethod(spellServiceType);
            var spellService = getMethod?.Invoke(_registry, new object[] { SelectionMode.HighestPriority });

            if (spellService == null)
            {
                _spellList.Items.Add("No spells available");
                return;
            }

            // Get known spells
            var getKnownSpellsMethod = spellServiceType.GetMethod("GetKnownSpells");
            var knownSpells = getKnownSpellsMethod?.Invoke(spellService, new object[] { _playerEntity }) as System.Collections.IEnumerable;

            if (knownSpells == null)
            {
                _spellList.Items.Add("No spells learned yet");
                return;
            }

            // Get Mana component
            var manaType = Type.GetType("LablabBean.Plugins.Spell.Components.Mana, LablabBean.Plugins.Spell");
            if (manaType != null)
            {
                var hasMethod = typeof(Arch.Core.Extensions.EntityExtensions).GetMethod("Has", new[] { typeof(Entity) })?.MakeGenericMethod(manaType);
                var hasMana = (bool)(hasMethod?.Invoke(null, new object[] { _playerEntity }) ?? false);

                int currentMana = 0;
                if (hasMana)
                {
                    var getComponentMethod = typeof(Arch.Core.Extensions.EntityExtensions).GetMethod("Get", new[] { typeof(Entity) })?.MakeGenericMethod(manaType);
                    var mana = getComponentMethod?.Invoke(null, new object[] { _playerEntity });
                    currentMana = (int)(manaType.GetProperty("Current")?.GetValue(mana) ?? 0);
                }

                // Add spells to list
                foreach (var spellId in knownSpells)
                {
                    if (spellId == null) continue;

                    // Get spell data
                    var getSpellMethod = spellServiceType.GetMethod("GetSpell");
                    var spell = getSpellMethod?.Invoke(spellService, new object[] { spellId.ToString()! });

                    if (spell == null) continue;

                    var spellType = spell.GetType();
                    var name = spellType.GetProperty("Name")?.GetValue(spell)?.ToString() ?? "Unknown";
                    var manaCost = (int)(spellType.GetProperty("ManaCost")?.GetValue(spell) ?? 0);
                    var cooldown = (int)(spellType.GetProperty("Cooldown")?.GetValue(spell) ?? 0);

                    // Check if spell can be cast
                    var canCastMethod = spellServiceType.GetMethod("CanCastSpell");
                    var canCast = (bool)(canCastMethod?.Invoke(spellService, new object[] { _playerEntity, spellId.ToString()! }) ?? false);

                    var status = canCast ? "" : " [NOT READY]";
                    var manaStatus = currentMana >= manaCost ? "" : " [LOW MANA]";

                    _spellList.Items.Add($"{name} (Cost: {manaCost}, CD: {cooldown}t){status}{manaStatus}");
                }
            }

            if (_spellList.Items.Count == 0)
            {
                _spellList.Items.Add("No spells learned yet");
            }
        }
        catch (Exception)
        {
            _spellList.Items.Add("Error loading spells");
        }
    }

    /// <summary>
    /// Gets the selected spell ID (if any)
    /// </summary>
    public string? GetSelectedSpell()
    {
        if (_spellList.SelectedItem == null || _spellList.Items.Count == 0)
            return null;

        try
        {
            if (_registry == null) return null;

            var spellServiceType = Type.GetType("LablabBean.Plugins.Spell.Services.SpellService, LablabBean.Plugins.Spell");
            if (spellServiceType == null) return null;

            var getMethod = typeof(IRegistry).GetMethod("Get")?.MakeGenericMethod(spellServiceType);
            var spellService = getMethod?.Invoke(_registry, new object[] { SelectionMode.HighestPriority });
            if (spellService == null) return null;

            // Get known spells
            var getKnownSpellsMethod = spellServiceType.GetMethod("GetKnownSpells");
            var knownSpells = getKnownSpellsMethod?.Invoke(spellService, new object[] { _playerEntity }) as System.Collections.IList;

            if (knownSpells == null || _spellList.SelectedIndex < 0 || _spellList.SelectedIndex >= knownSpells.Count)
                return null;

            return knownSpells[_spellList.SelectedIndex]?.ToString();
        }
        catch
        {
            return null;
        }
    }
}

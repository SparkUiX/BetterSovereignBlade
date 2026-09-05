using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace BetterSovereignBlade.Scripts.Patch;

[ModInitializer(nameof(Initialize))]
public static class PaletteSettingsMod
{
	private const string HarmonyId = "example.sts2.settings-palette-demo";

	private const string ConfigDirectoryPath = "user://mods/settings_palette_demo";

	private const string ConfigPath = ConfigDirectoryPath + "/palette_settings.cfg";

	private const string PresetFallbackPath = ConfigDirectoryPath + "/preset_value.txt";

	private const string ConfigSection = "scene_palette";

	private const float ScrollPaddingTop = 20f;

	private const float ScrollPaddingBottom = 30f;

	private const string DividerName = "ModScenePaletteDivider";

	private const string PresetRowName = "ModScenePalettePresetRow";

	private const string PresetDropdownName = "ModScenePalettePresetDropdown";

	private const string DynamicRgbRowName = "ModScenePaletteDynamicRgbRow";

	private const string DynamicRgbToggleName = "ModScenePaletteDynamicRgbToggle";

	private const string DynamicRgbSpeedRowName = "ModScenePaletteDynamicRgbSpeedRow";

	private const string DynamicRgbSpeedDropdownName = "ModScenePaletteDynamicRgbSpeedDropdown";

	private const string DragRotateRowName = "ModScenePaletteDragRotateRow";

	private const string DragRotateToggleName = "ModScenePaletteDragRotateToggle";

	private const string ColorRowPrefix = "ModScenePaletteColorRow";

	private const string ColorButtonPrefix = "ModScenePaletteColorButton";

	private const string ColorRgbTogglePrefix = "ModScenePaletteColorRgbToggle";

	private const float DefaultDynamicRgbSpeedCyclesPerSecond = 0.14f;

	private const float DynamicRgbSaturationBoost = 0.08f;

	private const float DynamicRgbValueBoost = 0.04f;

	private const string PresetCustomText = "自定义";

	private const string PresetDefaultText = "默认（不更改）";

	private const string PresetOneText = "预设颜色1";

	private const string PresetTwoText = "预设颜色2";

	private const string PresetPinkText = "粉色主题";

	private const string PresetAuroraText = "极光主题";

	private const string PresetBlackRedBlueText = "黑红蓝焰主题";

	private const int PresetCustomId = 0;

	private const int PresetOneId = 1;

	private const int PresetTwoId = 2;

	private const int PresetDefaultId = 3;

	private const int PresetPinkId = 4;

	private const int PresetAuroraId = 5;

	private const int PresetBlackRedBlueId = 8;

	// Editable default eight-color interface. You can change these values directly.
	private static readonly Color DefaultSwordBodyColor1 = new Color(1.0f, 1.0f, 1.0f, 0.471f);

	private static readonly Color DefaultSwordBodyColor2 = new Color(0.141f, 0.337f, 1.0f, 0.251f);

	private static readonly Color DefaultBladeColor1 = new Color(0.325f, 0.596f, 0.902f);

	private static readonly Color DefaultBladeColor2 = new Color(0.137f, 0.525f, 0.506f);

	private static readonly Color DefaultFlameColor1 = new Color(0.047f, 0.188f, 0.337f);

	private static readonly Color DefaultFlameColor2 = new Color(0.09f, 0.271f, 0.459f);

	// Attack slash effect 1 default from NBigSlashVfx creature tint ("50b598").
	private static readonly Color DefaultAuraEffect1Color = new Color(0.313726f, 0.709804f, 0.596078f, 1f);

	// Attack slash effect 2 default from NBigSlashImpactVfx tint (#80dbff).
	private static readonly Color DefaultAuraEffect2Color = new Color(0.501961f, 0.858824f, 1f, 1f);

	private static readonly Dictionary<PaletteColorSlot, ColorPickerButton> _colorButtons = new Dictionary<PaletteColorSlot, ColorPickerButton>();

	private static readonly Dictionary<PaletteColorSlot, NFastModeTickbox> _colorRgbToggles = new Dictionary<PaletteColorSlot, NFastModeTickbox>();

	private static readonly IReadOnlyList<(int Id, string Text, float Speed)> _dynamicSpeedOptions = new List<(int Id, string Text, float Speed)>
	{
		(50, "很慢", 0.05f),
		(100, "慢", 0.10f),
		(140, "默认", 0.14f),
		(200, "快", 0.20f),
		(300, "很快", 0.30f)
	};

	// Add or modify fixed presets here. Custom preset always uses the 8 custom colors.
	private static readonly IReadOnlyList<PresetDefinition> _presetDefinitions = new List<PresetDefinition>
	{
		PresetDefinition.Fixed(PresetDefaultId, PresetDefaultText, PaletteColorSet.CreateDefaultPreset()),
		PresetDefinition.Custom(PresetCustomId, PresetCustomText),
		PresetDefinition.Fixed(PresetOneId, PresetOneText, PaletteColorSet.From(
			new Color(0.81f, 0.9f, 1f, 0.52f),
			new Color(0.18f, 0.45f, 1f, 0.36f),
			new Color(0.95f, 0.68f, 0.16f, 1f),
			new Color(1f, 0.87f, 0.48f, 1f),
			new Color(1f, 0.48f, 0.08f, 1f),
			new Color(1f, 0.78f, 0.28f, 1f),
			new Color(1f, 0.75f, 0.24f, 1f),
			new Color(1f, 0.52f, 0.18f, 1f))),
		PresetDefinition.Fixed(PresetTwoId, PresetTwoText, PaletteColorSet.From(
			new Color(0.71f, 1f, 0.86f, 0.55f),
			new Color(0.08f, 0.74f, 0.56f, 0.4f),
			new Color(0.09f, 0.89f, 0.62f, 1f),
			new Color(0.64f, 1f, 0.89f, 1f),
			new Color(0.08f, 0.28f, 0.18f, 1f),
			new Color(0.13f, 0.52f, 0.36f, 1f),
			new Color(0.11f, 0.82f, 0.63f, 1f),
			new Color(0.63f, 1f, 0.87f, 1f))),
		PresetDefinition.Fixed(PresetPinkId, PresetPinkText, PaletteColorSet.From(
			new Color(1f, 0.82f, 0.92f, 0.58f),
			new Color(0.94f, 0.52f, 0.78f, 0.42f),
			new Color(0.95f, 0.34f, 0.67f, 1f),
			new Color(1f, 0.62f, 0.84f, 1f),
			new Color(0.65f, 0.12f, 0.42f, 1f),
			new Color(0.88f, 0.34f, 0.62f, 1f),
			new Color(0.96f, 0.38f, 0.72f, 1f),
			new Color(1f, 0.72f, 0.9f, 1f))),
		PresetDefinition.Fixed(PresetAuroraId, PresetAuroraText, PaletteColorSet.From(
			new Color(0.86f, 0.82f, 1f, 0.58f),
			new Color(0.56f, 0.42f, 0.98f, 0.42f),
			new Color(0.64f, 0.3f, 1f, 1f),
			new Color(0.9f, 0.68f, 1f, 1f),
			new Color(0.21f, 0.07f, 0.43f, 1f),
			new Color(0.4f, 0.18f, 0.67f, 1f),
			new Color(0.63f, 0.48f, 1f, 1f),
			new Color(0.84f, 0.72f, 1f, 1f))),
		PresetDefinition.Fixed(PresetBlackRedBlueId, PresetBlackRedBlueText, PaletteColorSet.From(
			new Color(0.02f, 0.02f, 0.02f, 0.65f),
			new Color(0.9f, 0.12f, 0.14f, 0.5f),
			new Color(0.15f, 0.55f, 1f, 1f),
			new Color(0.42f, 0.78f, 1f, 1f),
			new Color(1f, 0.36f, 0.08f, 1f),
			new Color(1f, 0.72f, 0.12f, 1f),
			new Color(0.95f, 0.09f, 0.14f, 1f),
			new Color(0.42f, 0.8f, 1f, 1f)))
	};

	private static readonly IReadOnlyDictionary<int, PresetDefinition> _presetDefinitionsById =
		_presetDefinitions.ToDictionary(entry => entry.Id, entry => entry);

	private static PaletteSettings _settings = PaletteSettings.CreateDefaults();

	private static ulong? _activeSlashPlayerNetId;

	private static double _activeSlashExpirySeconds;

	private static OptionButton? _presetDropdown;

	private static NFastModeTickbox? _dynamicRgbToggle;

	private static OptionButton? _dynamicRgbSpeedDropdown;

	private static NFastModeTickbox? _dragRotateToggle;

	private static bool _isRefreshingUi;

	private static bool _isInitialized;

	public static void Initialize()
	{
		if (_isInitialized)
		{
			return;
		}

		_isInitialized = true;
		LoadSettings();
		new Harmony(HarmonyId).PatchAll();
	}

	[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready))]
	private static class NSettingsScreenReadyPatch
	{
		private static void Postfix(NSettingsScreen __instance)
		{
			InjectPaletteSettings(__instance);
		}
	}

	[HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx._Ready))]
	private static class NSovereignBladeVfxReadyPatch
	{
		private static void Postfix(NSovereignBladeVfx __instance)
		{
			ApplyPaletteToVfx(__instance);
		}
	}

	[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._Ready))]
	private static class NCombatRoomReadyPatch
	{
		private static void Postfix()
		{
			ApplyPaletteToExistingVfx();
		}
	}
	[HarmonyPatch(typeof(NScrollbar), nameof(NScrollbar._Ready))]
	private static class NScrollbarReadyPatch
	{
		private static void Postfix(NScrollbar __instance)
		{
			EnsureScrollbarRange(__instance);
		}
	}

	[HarmonyPatch(typeof(NSettingsTabManager), "SwitchTabTo")]
	private static class NSettingsTabManagerSwitchTabPatch
	{
		private static void Postfix(NSettingsTabManager __instance)
		{
			NSettingsPanel? generalPanel = __instance.GetNodeOrNull<NSettingsPanel>("%GeneralSettings");
			if (generalPanel == null || !generalPanel.Visible || generalPanel.Content.GetNodeOrNull(PresetRowName) == null)
			{
				return;
			}

			NScrollableContainer? scrollContainer = __instance.GetNodeOrNull<NScrollableContainer>("%ScrollContainer");
			_ = RefreshSettingsLayoutAsync(generalPanel, scrollContainer, scrollToTop: false);
		}
	}
	[HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx.Forge))]
	private static class NSovereignBladeVfxForgePatch
	{
		private static void Postfix(NSovereignBladeVfx __instance)
		{
			ApplyPaletteToVfx(__instance);
		}
	}

	[HarmonyPatch(typeof(NSovereignBladeVfx), "EndSlash")]
	private static class NSovereignBladeVfxEndSlashPatch
	{
		private static void Postfix(NSovereignBladeVfx __instance)
		{
			ApplyPaletteToVfx(__instance);
		}
	}

	[HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx._Process))]
	private static class NSovereignBladeVfxProcessPatch
	{
		private static void Postfix(NSovereignBladeVfx __instance)
		{
			if (GetSettingsForVfx(__instance).DynamicRgbEnabled)
			{
				ApplyPaletteToVfx(__instance);
			}
		}
	}

	[HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx.Attack))]
	private static class NSovereignBladeVfxAttackPatch
	{
		private static void Prefix(NSovereignBladeVfx __instance)
		{
			ulong? netId = GetPlayerNetIdFromVfx(__instance);
			if (netId == null)
			{
				return;
			}

			_activeSlashPlayerNetId = netId;
			_activeSlashExpirySeconds = Time.GetTicksMsec() / 1000.0 + 1.0;
		}
	}

	[HarmonyPatch(typeof(NFastModeTickbox), "OnTick")]
	private static class NFastModeTickboxOnTickPatch
	{
		private static bool Prefix(NFastModeTickbox __instance)
		{
			return !TryHandleModTickboxState(__instance, isTicked: true);
		}
	}

	[HarmonyPatch(typeof(NFastModeTickbox), "OnUntick")]
	private static class NFastModeTickboxOnUntickPatch
	{
		private static bool Prefix(NFastModeTickbox __instance)
		{
			return !TryHandleModTickboxState(__instance, isTicked: false);
		}
	}

	[HarmonyPatch(typeof(NBigSlashVfx), nameof(NBigSlashVfx.Create), new[] { typeof(Vector2), typeof(bool), typeof(Color) })]
	private static class NBigSlashVfxCreatePatch
	{
		private static void Prefix(ref Color tint)
		{
			if (!IsSovereignBladeAttackContext())
			{
				return;
			}

			tint = GetRuntimeColorsForSlashContext().AuraEffect1Color;
		}
	}

	[HarmonyPatch(typeof(NBigSlashImpactVfx), nameof(NBigSlashImpactVfx.Create), new[] { typeof(Vector2), typeof(float), typeof(Color) })]
	private static class NBigSlashImpactVfxCreatePatch
	{
		private static void Prefix(ref Color tint)
		{
			if (!IsSovereignBladeAttackContext())
			{
				return;
			}

			tint = GetRuntimeColorsForSlashContext().AuraEffect2Color;
		}
	}

	private static void InjectPaletteSettings(NSettingsScreen screen)
	{
		NSettingsPanel generalPanel = screen.GetNode<NSettingsPanel>("%GeneralSettings");
		NScrollableContainer? scrollContainer = screen.GetNodeOrNull<NScrollableContainer>("%ScrollContainer");
		VBoxContainer content = generalPanel.Content;
		if (content.GetNodeOrNull(PresetRowName) != null)
		{
			TryBindExistingControls(content);
			RefreshUiFromSettings();
			_ = RefreshSettingsLayoutAsync(generalPanel, scrollContainer, scrollToTop: false);
			return;
		}

		List<Control> focusableControls = new List<Control>();
		FindFocusableControls(content, focusableControls);
		Control? previousControl = focusableControls.LastOrDefault();

		_colorButtons.Clear();
		_colorRgbToggles.Clear();
		_presetDropdown = null;
		_dynamicRgbToggle = null;
		_dynamicRgbSpeedDropdown = null;
		_dragRotateToggle = null;

		ColorRect divider = CreateDivider(content);
		content.AddChild(divider);

		List<Control> newControls = new List<Control>();

		MarginContainer presetRow = CreatePresetRow(content);
		_presetDropdown = presetRow.FindChild(PresetDropdownName, recursive: true, owned: false) as OptionButton;
		content.AddChild(presetRow);
		if (_presetDropdown != null)
		{
			newControls.Add(_presetDropdown);
		}

		MarginContainer dynamicRgbRow = CreateDynamicRgbRow(content);
		_dynamicRgbToggle = dynamicRgbRow.FindChild(DynamicRgbToggleName, recursive: true, owned: false) as NFastModeTickbox;
		content.AddChild(dynamicRgbRow);
		if (_dynamicRgbToggle != null)
		{
			newControls.Add(_dynamicRgbToggle);
		}

		MarginContainer dynamicRgbSpeedRow = CreateDynamicRgbSpeedRow(content);
		_dynamicRgbSpeedDropdown = dynamicRgbSpeedRow.FindChild(DynamicRgbSpeedDropdownName, recursive: true, owned: false) as OptionButton;
		content.AddChild(dynamicRgbSpeedRow);
		if (_dynamicRgbSpeedDropdown != null)
		{
			newControls.Add(_dynamicRgbSpeedDropdown);
		}

		MarginContainer dragRotateRow = CreateDragRotateRow(content);
		_dragRotateToggle = dragRotateRow.FindChild(DragRotateToggleName, recursive: true, owned: false) as NFastModeTickbox;
		content.AddChild(dragRotateRow);
		if (_dragRotateToggle != null)
		{
			newControls.Add(_dragRotateToggle);
		}

		foreach (PaletteColorSlot slot in Enum.GetValues(typeof(PaletteColorSlot)).Cast<PaletteColorSlot>())
		{
			MarginContainer colorRow = CreateColorRow(content, slot);
			content.AddChild(colorRow);

			if (colorRow.FindChild(GetColorButtonName(slot), recursive: true, owned: false) is ColorPickerButton button)
			{
				_colorButtons[slot] = button;
				newControls.Add(button);
			}

			if (colorRow.FindChild(GetColorRgbToggleName(slot), recursive: true, owned: false) is NFastModeTickbox rgbToggle)
			{
				_colorRgbToggles[slot] = rgbToggle;
				newControls.Add(rgbToggle);
			}
		}

		WireFocus(previousControl, newControls);
		RefreshUiFromSettings();
		RefreshPanelSize(generalPanel);
		_ = RefreshSettingsLayoutAsync(generalPanel, scrollContainer, scrollToTop: false);
	}

	private static ColorRect CreateDivider(VBoxContainer content)
	{
		ColorRect template = content.GetNode<ColorRect>("ScreenshakeDivider");
		ColorRect divider = (ColorRect)template.Duplicate();
		divider.Name = DividerName;
		divider.Visible = true;
		return divider;
	}

	private static MarginContainer CreatePresetRow(VBoxContainer content)
	{
		OptionButton dropdown = CreatePresetDropdown();
		return CreateSettingsRow(content, PresetRowName, "整体预设设置", dropdown);
	}

	private static MarginContainer CreateDynamicRgbRow(VBoxContainer content)
	{
		NFastModeTickbox toggle = CreateStyledTickbox(content, DynamicRgbToggleName);
		return CreateSettingsRow(content, DynamicRgbRowName, "动态RGB变色", toggle);
	}

	private static MarginContainer CreateDynamicRgbSpeedRow(VBoxContainer content)
	{
		OptionButton dropdown = CreateDynamicRgbSpeedDropdown();
		return CreateSettingsRow(content, DynamicRgbSpeedRowName, "动态RGB速度", dropdown);
	}

	private static MarginContainer CreateDragRotateRow(VBoxContainer content)
	{
		NFastModeTickbox toggle = CreateStyledTickbox(content, DragRotateToggleName);
		return CreateSettingsRow(content, DragRotateRowName, "拖动时旋转剑身并显示拖尾", toggle);
	}

	private static MarginContainer CreateColorRow(VBoxContainer content, PaletteColorSlot slot)
	{
		NFastModeTickbox rgbToggle = CreateStyledTickbox(content, GetColorRgbToggleName(slot));
		ColorPickerButton button = CreateColorButton(slot);

		HBoxContainer group = new HBoxContainer();
		group.AddThemeConstantOverride("separation", 10);
		group.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
		group.AddChild(rgbToggle);
		group.AddChild(button);

		return CreateSettingsRow(content, GetColorRowName(slot), GetSlotLabel(slot), group);
	}

	private static MarginContainer CreateSettingsRow(VBoxContainer content, string rowName, string labelText, Control inputControl)
	{
		MarginContainer row = new MarginContainer
		{
			Name = rowName,
			CustomMinimumSize = new Vector2(0f, 64f)
		};
		row.AddThemeConstantOverride("margin_left", 12);
		row.AddThemeConstantOverride("margin_right", 12);

		HBoxContainer hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 16);
		row.AddChild(hbox);

		MegaRichTextLabel templateLabel = content.GetNode<MegaRichTextLabel>("FastMode/Label");
		MegaRichTextLabel label = (MegaRichTextLabel)templateLabel.Duplicate();
		label.SetTextAutoSize(labelText);
		label.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		hbox.AddChild(label);

		hbox.AddChild(inputControl);
		return row;
	}

	private static OptionButton CreatePresetDropdown()
	{
		OptionButton dropdown = new OptionButton
		{
			Name = PresetDropdownName,
			CustomMinimumSize = new Vector2(324f, 48f),
			FocusMode = Control.FocusModeEnum.All,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
		};

		foreach (PresetDefinition preset in _presetDefinitions)
		{
			dropdown.AddItem(preset.Text, preset.Id);
		}

		dropdown.Connect("item_selected", Callable.From<long>(index =>
		{
			int selectedIndex = (int)index;
			if (selectedIndex < 0 || selectedIndex >= dropdown.ItemCount)
			{
				return;
			}

			OnPresetChanged(dropdown.GetItemId(selectedIndex));
		}));
		return dropdown;
	}

	private static OptionButton CreateDynamicRgbSpeedDropdown()
	{
		OptionButton dropdown = new OptionButton
		{
			Name = DynamicRgbSpeedDropdownName,
			CustomMinimumSize = new Vector2(324f, 48f),
			FocusMode = Control.FocusModeEnum.All,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
		};

		foreach ((int id, string text, float speed) in _dynamicSpeedOptions)
		{
			dropdown.AddItem($"{text} ({speed:0.00}/s)", id);
		}

		dropdown.Connect("item_selected", Callable.From<long>(index =>
		{
			int selectedIndex = (int)index;
			if (selectedIndex < 0 || selectedIndex >= dropdown.ItemCount)
			{
				return;
			}

			OnDynamicRgbSpeedChanged(dropdown.GetItemId(selectedIndex));
		}));

		return dropdown;
	}

	private static ColorPickerButton CreateColorButton(PaletteColorSlot slot)
	{
		ColorPickerButton button = new ColorPickerButton
		{
			Name = GetColorButtonName(slot),
			CustomMinimumSize = new Vector2(324f, 48f),
			FocusMode = Control.FocusModeEnum.All,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
		};
		button.Color = _settings.CustomColors.Get(slot);
		button.Text = FormatColor(button.Color);
		button.Set("edit_alpha", true);
		button.Set("edit_intensity", false);
		button.Connect("picker_created", Callable.From(() => ConfigurePicker(button.GetPicker())));
		button.Connect("color_changed", Callable.From<Color>(color => OnCustomColorChanged(slot, color)));
		ConfigurePicker(button.GetPicker());
		return button;
	}

	private static NFastModeTickbox CreateStyledTickbox(VBoxContainer content, string tickboxName)
	{
		NFastModeTickbox template = content.GetNode<NFastModeTickbox>("FastMode/FastModeTickbox");
		NFastModeTickbox toggle = (NFastModeTickbox)template.Duplicate();
		toggle.Name = tickboxName;
		toggle.FocusMode = Control.FocusModeEnum.All;
		toggle.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
		return toggle;
	}

	private static void ConfigurePicker(ColorPicker picker)
	{
		picker.Set("picker_shape", 1);
		picker.Set("color_mode", 1);
		picker.Set("deferred_mode", true);
		picker.Set("edit_alpha", true);
		picker.Set("edit_intensity", false);
		picker.Set("color_modes_visible", true);
		picker.Set("sliders_visible", true);
		picker.Set("hex_visible", true);
		picker.Set("sampler_visible", false);
		picker.Set("presets_visible", false);
		picker.Set("can_add_swatches", false);
	}

	private static void WireFocus(Control? previousControl, IReadOnlyList<Control> newControls)
	{
		List<Control> controls = newControls.Where(static control => control != null && GodotObject.IsInstanceValid(control)).ToList();
		if (controls.Count == 0)
		{
			return;
		}

		if (previousControl != null)
		{
			previousControl.FocusNeighborBottom = controls[0].GetPath();
		}

		for (int i = 0; i < controls.Count; i++)
		{
			Control current = controls[i];
			Control top = i == 0 ? previousControl ?? current : controls[i - 1];
			Control bottom = i == controls.Count - 1 ? current : controls[i + 1];
			current.FocusNeighborTop = top.GetPath();
			current.FocusNeighborBottom = bottom.GetPath();
			current.FocusNeighborLeft = current.GetPath();
			current.FocusNeighborRight = current.GetPath();
		}
	}

	private static void FindFocusableControls(Control parent, List<Control> result)
	{
		foreach (Control child in parent.GetChildren().OfType<Control>())
		{
			if (child.FocusMode == Control.FocusModeEnum.All && child.Visible)
			{
				result.Add(child);
			}

			FindFocusableControls(child, result);
		}
	}

	private static void TryBindExistingControls(VBoxContainer content)
	{
		_presetDropdown = content.FindChild(PresetDropdownName, recursive: true, owned: false) as OptionButton;
		_dynamicRgbToggle = content.FindChild(DynamicRgbToggleName, recursive: true, owned: false) as NFastModeTickbox;
		_dynamicRgbSpeedDropdown = content.FindChild(DynamicRgbSpeedDropdownName, recursive: true, owned: false) as OptionButton;
		_dragRotateToggle = content.FindChild(DragRotateToggleName, recursive: true, owned: false) as NFastModeTickbox;

		_colorButtons.Clear();
		_colorRgbToggles.Clear();
		foreach (PaletteColorSlot slot in Enum.GetValues(typeof(PaletteColorSlot)).Cast<PaletteColorSlot>())
		{
			if (content.FindChild(GetColorButtonName(slot), recursive: true, owned: false) is ColorPickerButton button)
			{
				_colorButtons[slot] = button;
			}

			if (content.FindChild(GetColorRgbToggleName(slot), recursive: true, owned: false) is NFastModeTickbox rgbToggle)
			{
				_colorRgbToggles[slot] = rgbToggle;
			}
		}
	}

	private static void LoadSettings()
	{
		PaletteSettings defaults = PaletteSettings.CreateDefaults();
		ConfigFile config = new ConfigFile();
		Error error = config.Load(ConfigPath);
		if (error != Error.Ok)
		{
			defaults.PresetId = ParsePresetId(LoadPresetFallback(defaults.PresetId));
			_settings = defaults;
			if (error != Error.FileNotFound)
			{
				PrintWarning($"Failed to load palette settings from {ConfigPath}: {error}");
			}
			return;
		}

		defaults.PresetId = ParsePresetId(ReadPresetValue(config, LoadPresetFallback(defaults.PresetId)));
		defaults.DynamicRgbEnabled = ReadBool(config, "dynamic_rgb_enabled", defaults.DynamicRgbEnabled);
		defaults.DynamicRgbSpeed = ReadFloat(config, "dynamic_rgb_speed", defaults.DynamicRgbSpeed);
		defaults.RotateTowardsMouseWhileDragging = ReadBool(config, "drag_rotate_enabled", defaults.RotateTowardsMouseWhileDragging);
		defaults.RgbMask.SwordBody1 = ReadBool(config, "rgb_sword_body_1", defaults.RgbMask.SwordBody1);
		defaults.RgbMask.SwordBody2 = ReadBool(config, "rgb_sword_body_2", defaults.RgbMask.SwordBody2);
		defaults.RgbMask.Blade1 = ReadBool(config, "rgb_blade_1", defaults.RgbMask.Blade1);
		defaults.RgbMask.Blade2 = ReadBool(config, "rgb_blade_2", defaults.RgbMask.Blade2);
		defaults.RgbMask.Flame1 = ReadBool(config, "rgb_flame_1", defaults.RgbMask.Flame1);
		defaults.RgbMask.Flame2 = ReadBool(config, "rgb_flame_2", defaults.RgbMask.Flame2);
		defaults.RgbMask.AuraEffect1 = ReadBool(config, "rgb_aura_1", defaults.RgbMask.AuraEffect1);
		defaults.RgbMask.AuraEffect2 = ReadBool(config, "rgb_aura_2", defaults.RgbMask.AuraEffect2);
		defaults.CustomColors.SwordBodyColor1 = ReadColor(config, "sword_body_1", ReadColor(config, "sword_body", defaults.CustomColors.SwordBodyColor1));
		defaults.CustomColors.SwordBodyColor2 = ReadColor(config, "sword_body_2", ReadColor(config, "blade_2", defaults.CustomColors.SwordBodyColor2));
		defaults.CustomColors.BladeColor1 = ReadColor(config, "blade_1", defaults.CustomColors.BladeColor1);
		defaults.CustomColors.BladeColor2 = ReadColor(config, "blade_2", defaults.CustomColors.BladeColor2);
		defaults.CustomColors.FlameColor1 = ReadColor(config, "flame_1", defaults.CustomColors.FlameColor1);
		defaults.CustomColors.FlameColor2 = ReadColor(config, "flame_2", defaults.CustomColors.FlameColor2);
		defaults.CustomColors.AuraEffect1Color = ReadColor(config, "aura_1", ReadColor(config, "aura", defaults.CustomColors.SwordBodyColor2));
		defaults.CustomColors.AuraEffect2Color = ReadColor(config, "aura_2", ReadColor(config, "aura", defaults.CustomColors.AuraEffect1Color));
		_settings = defaults;
	}

	private static void SaveSettings()
	{
		try
		{
			DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(ConfigDirectoryPath));
			ConfigFile config = new ConfigFile();
			config.SetValue(ConfigSection, "preset", _settings.PresetId);
			config.SetValue(ConfigSection, "dynamic_rgb_enabled", _settings.DynamicRgbEnabled);
			config.SetValue(ConfigSection, "dynamic_rgb_speed", _settings.DynamicRgbSpeed);
			config.SetValue(ConfigSection, "drag_rotate_enabled", _settings.RotateTowardsMouseWhileDragging);
			config.SetValue(ConfigSection, "rgb_sword_body_1", _settings.RgbMask.SwordBody1);
			config.SetValue(ConfigSection, "rgb_sword_body_2", _settings.RgbMask.SwordBody2);
			config.SetValue(ConfigSection, "rgb_blade_1", _settings.RgbMask.Blade1);
			config.SetValue(ConfigSection, "rgb_blade_2", _settings.RgbMask.Blade2);
			config.SetValue(ConfigSection, "rgb_flame_1", _settings.RgbMask.Flame1);
			config.SetValue(ConfigSection, "rgb_flame_2", _settings.RgbMask.Flame2);
			config.SetValue(ConfigSection, "rgb_aura_1", _settings.RgbMask.AuraEffect1);
			config.SetValue(ConfigSection, "rgb_aura_2", _settings.RgbMask.AuraEffect2);
			config.SetValue(ConfigSection, "sword_body", _settings.CustomColors.SwordBodyColor1);
			config.SetValue(ConfigSection, "sword_body_1", _settings.CustomColors.SwordBodyColor1);
			config.SetValue(ConfigSection, "sword_body_2", _settings.CustomColors.SwordBodyColor2);
			config.SetValue(ConfigSection, "blade_1", _settings.CustomColors.BladeColor1);
			config.SetValue(ConfigSection, "blade_2", _settings.CustomColors.BladeColor2);
			config.SetValue(ConfigSection, "flame_1", _settings.CustomColors.FlameColor1);
			config.SetValue(ConfigSection, "flame_2", _settings.CustomColors.FlameColor2);
			config.SetValue(ConfigSection, "aura", _settings.CustomColors.AuraEffect1Color);
			config.SetValue(ConfigSection, "aura_1", _settings.CustomColors.AuraEffect1Color);
			config.SetValue(ConfigSection, "aura_2", _settings.CustomColors.AuraEffect2Color);
			Error error = config.Save(ConfigPath);
			if (error != Error.Ok)
			{
				PrintWarning($"Failed to save palette settings to {ConfigPath}: {error}");
			}

			SavePresetFallback(_settings.PresetId);
		}
		catch (Exception exception)
		{
			PrintWarning($"Failed to save palette settings: {exception.Message}");
		}
	}

	private static int LoadPresetFallback(int fallback)
	{
		try
		{
			if (!Godot.FileAccess.FileExists(PresetFallbackPath))
			{
				return fallback;
			}

			using Godot.FileAccess file = Godot.FileAccess.Open(PresetFallbackPath, Godot.FileAccess.ModeFlags.Read);
			if (file == null)
			{
				return fallback;
			}

			string text = file.GetAsText().Trim();
			if (int.TryParse(text, out int value))
			{
				return value;
			}
		}
		catch
		{
		}

		return fallback;
	}

	private static void SavePresetFallback(int value)
	{
		try
		{
			using Godot.FileAccess file = Godot.FileAccess.Open(PresetFallbackPath, Godot.FileAccess.ModeFlags.Write);
			if (file == null)
			{
				return;
			}

			file.StoreString(value.ToString());
		}
		catch
		{
		}
	}

	private static void RefreshUiFromSettings()
	{
		if (_presetDropdown == null)
		{
			return;
		}

		_isRefreshingUi = true;
		try
		{
			int selectedIndex = FindDropdownItemIndexById(_presetDropdown, _settings.PresetId);
			if (selectedIndex < 0)
			{
				selectedIndex = FindDropdownItemIndexById(_presetDropdown, PresetDefaultId);
			}
			if (selectedIndex < 0)
			{
				selectedIndex = 0;
			}

			_presetDropdown.Select(selectedIndex);
			if (_dynamicRgbToggle != null)
			{
				_dynamicRgbToggle.IsTicked = _settings.DynamicRgbEnabled;
			}

			if (_dynamicRgbSpeedDropdown != null)
			{
				int speedIndex = FindSpeedDropdownItemIndexBySpeed(_dynamicRgbSpeedDropdown, _settings.DynamicRgbSpeed);
				if (speedIndex < 0)
				{
					speedIndex = 0;
				}

				_dynamicRgbSpeedDropdown.Select(speedIndex);
			}

			if (_dragRotateToggle != null)
			{
				_dragRotateToggle.IsTicked = _settings.RotateTowardsMouseWhileDragging;
			}
			bool useCustomColors = IsCustomPreset(_settings.PresetId);
			PaletteColorSet activeColors = _settings.GetActiveColors();
			foreach ((PaletteColorSlot slot, ColorPickerButton button) in _colorButtons)
			{
				Color color = activeColors.Get(slot);
				button.Color = color;
				button.Text = FormatColor(color);
				button.Disabled = !useCustomColors;
				button.Modulate = useCustomColors ? Colors.White : new Color(1f, 1f, 1f, 0.6f);
			}

			foreach ((PaletteColorSlot slot, NFastModeTickbox toggle) in _colorRgbToggles)
			{
				toggle.IsTicked = _settings.RgbMask.Get(slot);
			}
		}
		finally
		{
			_isRefreshingUi = false;
		}
	}

	private static int FindDropdownItemIndexById(OptionButton dropdown, int id)
	{
		for (int i = 0; i < dropdown.ItemCount; i++)
		{
			if (dropdown.GetItemId(i) == id)
			{
				return i;
			}
		}

		return -1;
	}

	private static void OnPresetChanged(int selectedItemId)
	{
		if (_isRefreshingUi)
		{
			return;
		}

		_settings.PresetId = ParsePresetId(selectedItemId);
		SaveSettings();
		RefreshUiFromSettings();
		ApplyPaletteToExistingVfx();
	}

	private static void OnCustomColorChanged(PaletteColorSlot slot, Color color)
	{
		if (_isRefreshingUi)
		{
			return;
		}

		_settings.CustomColors.Set(slot, color);
		SaveSettings();
		RefreshUiFromSettings();
		ApplyPaletteToExistingVfx();
	}

	private static void OnDynamicRgbToggled(bool enabled)
	{
		if (_isRefreshingUi)
		{
			return;
		}

		_settings.DynamicRgbEnabled = enabled;
		SaveSettings();
		RefreshUiFromSettings();
		ApplyPaletteToExistingVfx();
	}

	private static void OnDynamicRgbSpeedChanged(int speedId)
	{
		if (_isRefreshingUi)
		{
			return;
		}

		if (!TryGetSpeedFromId(speedId, out float speed))
		{
			return;
		}

		_settings.DynamicRgbSpeed = speed;
		SaveSettings();
		RefreshUiFromSettings();
		ApplyPaletteToExistingVfx();
	}

	private static void OnDragRotateToggled(bool enabled)
	{
		if (_isRefreshingUi)
		{
			return;
		}

		_settings.RotateTowardsMouseWhileDragging = enabled;
		SaveSettings();
		RefreshUiFromSettings();
	}

	private static void OnSlotRgbToggled(PaletteColorSlot slot, bool enabled)
	{
		if (_isRefreshingUi)
		{
			return;
		}

		_settings.RgbMask.Set(slot, enabled);
		SaveSettings();
		RefreshUiFromSettings();
		ApplyPaletteToExistingVfx();
	}

	private static void ApplyPaletteToExistingVfx()
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}

		foreach (Node node in EnumerateNodes(tree.Root))
		{
			if (node is NSovereignBladeVfx vfx)
			{
				ApplyPaletteToVfx(vfx);
			}
		}
	}

	private static IEnumerable<Node> EnumerateNodes(Node root)
	{
		yield return root;
		foreach (Node child in root.GetChildren())
		{
			foreach (Node descendant in EnumerateNodes(child))
			{
				yield return descendant;
			}
		}
	}

	private static void ApplyPaletteToVfx(NSovereignBladeVfx vfx)
	{
		PaletteSettings settings = GetSettingsForVfx(vfx);
		PaletteColorSet colors = GetRuntimeColors(settings);
		ApplyBladeAndFlamePalette(vfx, colors);
	}

	private static PaletteColorSet GetRuntimeColorsForSlashContext()
	{
		PaletteSettings settings = ResolveSettingsForSlashContext();
		return GetRuntimeColors(settings);
	}

	internal static SovereignBladeTrailPalette GetRuntimeTrailPalette()
	{
		PaletteColorSet colors = GetRuntimeColors(_settings);
		Color primary = colors.BladeColor1;
		Color secondary = colors.BladeColor2;
		Color spark = colors.AuraEffect2Color.Lerp(colors.FlameColor2, 0.35f);

		return new SovereignBladeTrailPalette(primary, secondary, spark);
	}

	internal static bool IsDragRotateEnabled()
	{
		return _settings.RotateTowardsMouseWhileDragging;
	}

	private static PaletteColorSet GetRuntimeColors(PaletteSettings settings)
	{
		PaletteColorSet baseColors = settings.GetActiveColors();
		if (!settings.DynamicRgbEnabled)
		{
			return baseColors;
		}

		float timeSeconds = Time.GetTicksMsec() / 1000f;
		return CreateDynamicColors(baseColors, timeSeconds, settings);
	}

	private static PaletteColorSet CreateDynamicColors(PaletteColorSet source, float timeSeconds, PaletteSettings settings)
	{
		float phase = (timeSeconds * settings.DynamicRgbSpeed) % 1f;
		return PaletteColorSet.From(
			AnimateColorIfEnabled(PaletteColorSlot.SwordBody1, source.SwordBodyColor1, phase + 0.00f, settings),
			AnimateColorIfEnabled(PaletteColorSlot.SwordBody2, source.SwordBodyColor2, phase + 0.08f, settings),
			AnimateColorIfEnabled(PaletteColorSlot.Blade1, source.BladeColor1, phase + 0.16f, settings),
			AnimateColorIfEnabled(PaletteColorSlot.Blade2, source.BladeColor2, phase + 0.24f, settings),
			AnimateColorIfEnabled(PaletteColorSlot.Flame1, source.FlameColor1, phase + 0.32f, settings),
			AnimateColorIfEnabled(PaletteColorSlot.Flame2, source.FlameColor2, phase + 0.40f, settings),
			AnimateColorIfEnabled(PaletteColorSlot.AuraEffect1, source.AuraEffect1Color, phase + 0.48f, settings),
			AnimateColorIfEnabled(PaletteColorSlot.AuraEffect2, source.AuraEffect2Color, phase + 0.56f, settings));
	}

	private static Color AnimateColorIfEnabled(PaletteColorSlot slot, Color baseColor, float phase, PaletteSettings settings)
	{
		return settings.RgbMask.Get(slot) ? AnimateColor(baseColor, phase) : baseColor;
	}

	private static PaletteSettings GetSettingsForVfx(NSovereignBladeVfx vfx)
	{
		return _settings;
	}

	private static PaletteSettings ResolveSettingsForSlashContext()
	{
		return _settings;
	}

	private static ulong? GetPlayerNetIdFromVfx(NSovereignBladeVfx? vfx)
	{
		return vfx?.Card?.Owner?.NetId;
	}


	private static Color AnimateColor(Color baseColor, float phase)
	{
		float hue = Mathf.PosMod(phase, 1f);
		Color rgbWave = Color.FromHsv(hue, 1f, 1f, 1f);
		Color mixed = baseColor.Lerp(rgbWave, 0.45f);

		// Keep alpha from the source color while adding a soft saturation/value lift.
		float boostedS = Mathf.Clamp(mixed.S + DynamicRgbSaturationBoost, 0f, 1f);
		float boostedV = Mathf.Clamp(mixed.V + DynamicRgbValueBoost, 0f, 1f);
		return Color.FromHsv(mixed.H, boostedS, boostedV, baseColor.A);
	}

	private static void ApplyBladeAndFlamePalette(NSovereignBladeVfx vfx, PaletteColorSet colors)
	{
		// Sword body colors are mapped directly to the two blade sprite modulates.
		SetTint(GetCanvasItem(vfx, "SpineSword/SwordBone/ScaleContainer/Blade"), colors.SwordBodyColor1);
		SetTint(GetCanvasItem(vfx, "SpineSword/SwordBone/ScaleContainer/Blade2"), colors.SwordBodyColor2);

		// Blade edge colors are mapped to the outline material shader parameters.
		SetShaderColor(vfx, "SpineSword/SwordBone/ScaleContainer/BladeOutline2", "OuterColor", colors.BladeColor1);
		SetShaderColor(vfx, "SpineSword/SwordBone/ScaleContainer/BladeOutline2", "InnerColor", colors.BladeColor2);

		// Flame colors are mapped to the stepped fire shader parameters.
		SetShaderColor(vfx, "SpineSword/SwordBone/ScaleContainer/SteppedFireMix", "OuterColor", colors.FlameColor1);
		SetShaderColor(vfx, "SpineSword/SwordBone/ScaleContainer/SteppedFireMix", "InnerColor", colors.FlameColor2);
	}

	private static void SetTrailColors(Line2D? trail, Color defaultColor, Color modulate)
	{
		if (trail == null)
		{
			return;
		}

		trail.DefaultColor = defaultColor;
		trail.Modulate = modulate;
	}

	private static CanvasItem? GetCanvasItem(Node root, string path)
	{
		return root.GetNodeOrNull<Node>(path) as CanvasItem;
	}

	private static void SetTint(CanvasItem? item, Color color)
	{
		if (item == null)
		{
			return;
		}

		item.Modulate = color;
	}

	private static void SetShaderColor(Node root, string path, string shaderParameterName, Color color)
	{
		CanvasItem? item = GetCanvasItem(root, path);
		if (item == null)
		{
			return;
		}

		ShaderMaterial? shaderMaterial = GetUniqueShaderMaterial(item);
		if (shaderMaterial == null)
		{
			return;
		}

		shaderMaterial.SetShaderParameter(shaderParameterName, color);
	}

	private static ShaderMaterial? GetUniqueShaderMaterial(CanvasItem item)
	{
		if (item.Material is not ShaderMaterial shaderMaterial)
		{
			return null;
		}

		const string MaterialUniqueMetaKey = "bsb_palette_unique_material";
		if (!item.HasMeta(MaterialUniqueMetaKey))
		{
			ShaderMaterial duplicated = (ShaderMaterial)shaderMaterial.Duplicate();
			duplicated.ResourceLocalToScene = true;
			item.Material = duplicated;
			item.SetMeta(MaterialUniqueMetaKey, true);
			return duplicated;
		}

		return item.Material as ShaderMaterial;
	}

	private static void RefreshPanelSize(NSettingsPanel panel)
	{
		MethodInfo? refreshSize = AccessTools.Method(typeof(NSettingsPanel), "RefreshSize");
		if (refreshSize != null)
		{
			refreshSize.Invoke(panel, null);
			return;
		}

		Control parent = panel.GetParent<Control>();
		Vector2 minimumSize = panel.Content.GetMinimumSize();
		panel.Size = minimumSize.Y + 50f >= parent.Size.Y
			? new Vector2(panel.Content.Size.X, minimumSize.Y + parent.Size.Y * 0.4f)
			: new Vector2(panel.Content.Size.X, minimumSize.Y);
	}

	private static void EnsureScrollbarRange(NScrollbar scrollbar)
	{
		if (scrollbar.MaxValue < 100.0)
		{
			scrollbar.MaxValue = 100.0;
		}
	}

	private static async Task RefreshSettingsLayoutAsync(NSettingsPanel panel, NScrollableContainer? scrollContainer, bool scrollToTop)
	{
		panel.Content.QueueSort();

		await panel.ToSignal(panel.GetTree(), SceneTree.SignalName.ProcessFrame);
		RefreshPanelSize(panel);

		panel.Content.QueueSort();
		await panel.ToSignal(panel.GetTree(), SceneTree.SignalName.ProcessFrame);
		RefreshPanelSize(panel);

		if (scrollContainer != null)
		{
			EnsureScrollbarRange(scrollContainer.Scrollbar);
			scrollContainer.SetContent(panel, ScrollPaddingTop, ScrollPaddingBottom);
			if (scrollToTop)
			{
				scrollContainer.InstantlyScrollToTop();
			}
		}
	}

	private static string GetColorRowName(PaletteColorSlot slot)
	{
		return ColorRowPrefix + slot;
	}

	private static string GetColorButtonName(PaletteColorSlot slot)
	{
		return ColorButtonPrefix + slot;
	}

	private static string GetColorRgbToggleName(PaletteColorSlot slot)
	{
		return ColorRgbTogglePrefix + slot;
	}

	private static bool TryHandleModTickboxState(NFastModeTickbox tickbox, bool isTicked)
	{
		string tickboxName = tickbox.Name.ToString();
		if (!tickboxName.StartsWith("ModScenePalette", StringComparison.Ordinal))
		{
			return false;
		}

		if (tickboxName == DynamicRgbToggleName)
		{
			OnDynamicRgbToggled(isTicked);
			return true;
		}

		if (tickboxName == DragRotateToggleName)
		{
			OnDragRotateToggled(isTicked);
			return true;
		}

		if (tickboxName.StartsWith(ColorRgbTogglePrefix, StringComparison.Ordinal))
		{
			string slotText = tickboxName.Substring(ColorRgbTogglePrefix.Length);
			if (Enum.TryParse(slotText, out PaletteColorSlot slot))
			{
				OnSlotRgbToggled(slot, isTicked);
				return true;
			}
		}

		return false;
	}

	private static bool TryGetSpeedFromId(int speedId, out float speed)
	{
		foreach ((int id, _, float itemSpeed) in _dynamicSpeedOptions)
		{
			if (id == speedId)
			{
				speed = itemSpeed;
				return true;
			}
		}

		speed = DefaultDynamicRgbSpeedCyclesPerSecond;
		return false;
	}

	private static int FindSpeedDropdownItemIndexBySpeed(OptionButton dropdown, float speed)
	{
		speed = ClampDynamicRgbSpeed(speed);
		int bestIndex = -1;
		float bestDistance = float.MaxValue;

		for (int i = 0; i < dropdown.ItemCount; i++)
		{
			if (!TryGetSpeedFromId(dropdown.GetItemId(i), out float itemSpeed))
			{
				continue;
			}

			float distance = Math.Abs(itemSpeed - speed);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestIndex = i;
			}
		}

		return bestIndex;
	}

	private static float ClampDynamicRgbSpeed(float speed)
	{
		return Mathf.Clamp(speed, 0.01f, 1.00f);
	}

	private static string GetSlotLabel(PaletteColorSlot slot)
	{
		return slot switch
		{
			PaletteColorSlot.SwordBody1 => "剑身颜色1",
			PaletteColorSlot.SwordBody2 => "剑身颜色2",
			PaletteColorSlot.Blade1 => "剑刃颜色1",
			PaletteColorSlot.Blade2 => "剑刃颜色2",
			PaletteColorSlot.Flame1 => "火焰颜色1",
			PaletteColorSlot.Flame2 => "火焰颜色2",
			PaletteColorSlot.AuraEffect1 => "剑气特效1",
			PaletteColorSlot.AuraEffect2 => "剑气特效2",
			_ => slot.ToString()
		};
	}

	private static string FormatColor(Color color)
	{
		return "#" + color.ToHtml(true).ToUpperInvariant();
	}

	private static bool TryGetPresetDefinition(int presetId, out PresetDefinition preset)
	{
		if (_presetDefinitionsById.TryGetValue(presetId, out PresetDefinition? definition))
		{
			preset = definition;
			return true;
		}

		preset = _presetDefinitionsById[PresetDefaultId];
		return false;
	}

	private static int ParsePresetId(int value)
	{
		return TryGetPresetDefinition(value, out PresetDefinition preset)
			? preset.Id
			: PresetDefaultId;
	}

	private static bool IsCustomPreset(int presetId)
	{
		return TryGetPresetDefinition(presetId, out PresetDefinition preset) && preset.Kind == PresetKind.Custom;
	}

	private static bool IsSovereignBladeAttackContext()
	{
		const string SovereignBladeTypePrefix = "MegaCrit.Sts2.Core.Models.Cards.SovereignBlade";
		StackFrame[]? frames = new StackTrace(skipFrames: 1, fNeedFileInfo: false).GetFrames();
		if (frames == null)
		{
			return false;
		}

		foreach (StackFrame frame in frames)
		{
			string? fullName = frame.GetMethod()?.DeclaringType?.FullName;
			if (fullName != null && fullName.StartsWith(SovereignBladeTypePrefix, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static PaletteColorSet ResolveActiveColors(int presetId, PaletteColorSet customColors)
	{
		if (!TryGetPresetDefinition(presetId, out PresetDefinition preset))
		{
			return PaletteColorSet.CreateDefaultPreset();
		}

		if (preset.Kind == PresetKind.Custom)
		{
			return customColors.Clone();
		}

		return preset.Colors?.Clone() ?? PaletteColorSet.CreateDefaultPreset();
	}

	private static int ReadPresetValue(ConfigFile config, int fallback)
	{
		Variant value;
		try
		{
			value = config.GetValue(ConfigSection, "preset", fallback);
		}
		catch
		{
			return fallback;
		}

		string text = value.ToString().Trim();
		if (int.TryParse(text, out int parsedInt))
		{
			return parsedInt;
		}

		if (double.TryParse(text, out double parsedDouble))
		{
			return (int)parsedDouble;
		}

		return text.ToLowerInvariant() switch
		{
			"default" => PresetDefaultId,
			"custom" => PresetCustomId,
			"presetone" => PresetOneId,
			"presettwo" => PresetTwoId,
			"preset1" => PresetOneId,
			"preset2" => PresetTwoId,
			"presetpink" => PresetPinkId,
			"preset3" => PresetPinkId,
			"pink" => PresetPinkId,
			"presetaurora" => PresetAuroraId,
			"preset6" => PresetAuroraId,
			"aurora" => PresetAuroraId,
			"presetblackredblue" => PresetBlackRedBlueId,
			"preset8" => PresetBlackRedBlueId,
			"blackredblue" => PresetBlackRedBlueId,
			_ => fallback
		};
	}

	private static Color ReadColor(ConfigFile config, string key, Color fallback)
	{
		try
		{
			return (Color)config.GetValue(ConfigSection, key, fallback);
		}
		catch
		{
			return fallback;
		}
	}

	private static bool ReadBool(ConfigFile config, string key, bool fallback)
	{
		try
		{
			Variant value = config.GetValue(ConfigSection, key, fallback);
			return value.VariantType switch
			{
				Variant.Type.Bool => (bool)value,
				Variant.Type.Int => (int)value != 0,
				Variant.Type.Float => Math.Abs((double)(float)value) > 0.001,
				_ => bool.TryParse(value.ToString(), out bool parsed) ? parsed : fallback
			};
		}
		catch
		{
			return fallback;
		}
	}

	private static float ReadFloat(ConfigFile config, string key, float fallback)
	{
		try
		{
			Variant value = config.GetValue(ConfigSection, key, fallback);
			if (value.VariantType == Variant.Type.Float)
			{
				return ClampDynamicRgbSpeed((float)value);
			}

			if (value.VariantType == Variant.Type.Int)
			{
				return ClampDynamicRgbSpeed((int)value);
			}

			if (float.TryParse(value.ToString(), out float parsed))
			{
				return ClampDynamicRgbSpeed(parsed);
			}
		}
		catch
		{
		}

		return ClampDynamicRgbSpeed(fallback);
	}

	private static void PrintWarning(string message)
	{
		if (!TryPrintToModConsole(message))
		{
			GD.PushWarning(message);
		}
	}

	private static bool TryPrintToModConsole(string message)
	{
		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			foreach (Type type in GetLoadableTypes(assembly))
			{
				if (!string.Equals(type.Name, "ModConsole", StringComparison.Ordinal))
				{
					continue;
				}

				MethodInfo? printMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
					.FirstOrDefault(method => method.Name == "Print" && method.GetParameters().Length == 1);
				if (printMethod == null)
				{
					continue;
				}

				ParameterInfo parameter = printMethod.GetParameters()[0];
				if (parameter.ParameterType != typeof(string) && parameter.ParameterType != typeof(object))
				{
					continue;
				}

				printMethod.Invoke(null, new object[] { message });
				return true;
			}
		}

		return false;
	}

	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException exception)
		{
			return exception.Types.Where(type => type != null)!;
		}
	}

	private enum PresetKind
	{
		Custom,
		Palette
	}

	private sealed class PresetDefinition
	{
		private PresetDefinition(int id, string text, PresetKind kind, PaletteColorSet? colors)
		{
			Id = id;
			Text = text;
			Kind = kind;
			Colors = colors;
		}

		public static PresetDefinition Custom(int id, string text)
		{
			return new PresetDefinition(id, text, PresetKind.Custom, null);
		}

		public static PresetDefinition Fixed(int id, string text, PaletteColorSet colors)
		{
			return new PresetDefinition(id, text, PresetKind.Palette, colors);
		}

		public int Id { get; }

		public string Text { get; }

		public PresetKind Kind { get; }

		public PaletteColorSet? Colors { get; }
	}

	private enum PaletteColorSlot
	{
		SwordBody1,
		SwordBody2,
		Blade1,
		Blade2,
		Flame1,
		Flame2,
		AuraEffect1,
		AuraEffect2
	}

	private sealed class PaletteSettings
	{
		public int PresetId { get; set; }

		public bool DynamicRgbEnabled { get; set; }

		public float DynamicRgbSpeed { get; set; }

		public bool RotateTowardsMouseWhileDragging { get; set; }

		public PaletteColorSet CustomColors { get; } = PaletteColorSet.CreateDefaultCustom();

		public PaletteRgbMask RgbMask { get; } = PaletteRgbMask.CreateAllEnabled();

		public static PaletteSettings CreateDefaults()
		{
			return new PaletteSettings
			{
				PresetId = PresetDefaultId,
				DynamicRgbEnabled = false,
				DynamicRgbSpeed = DefaultDynamicRgbSpeedCyclesPerSecond,
				RotateTowardsMouseWhileDragging = true
			};
		}

		public PaletteColorSet GetActiveColors()
		{
			return ResolveActiveColors(PresetId, CustomColors);
		}

		public PaletteSettings Clone()
		{
			PaletteSettings cloned = CreateDefaults();
			cloned.PresetId = PresetId;
			cloned.DynamicRgbEnabled = DynamicRgbEnabled;
			cloned.DynamicRgbSpeed = DynamicRgbSpeed;
			cloned.RotateTowardsMouseWhileDragging = RotateTowardsMouseWhileDragging;
			foreach (PaletteColorSlot slot in Enum.GetValues(typeof(PaletteColorSlot)).Cast<PaletteColorSlot>())
			{
				cloned.CustomColors.Set(slot, CustomColors.Get(slot));
				cloned.RgbMask.Set(slot, RgbMask.Get(slot));
			}

			return cloned;
		}
	}

	private sealed class PaletteRgbMask
	{
		public bool SwordBody1 { get; set; }

		public bool SwordBody2 { get; set; }

		public bool Blade1 { get; set; }

		public bool Blade2 { get; set; }

		public bool Flame1 { get; set; }

		public bool Flame2 { get; set; }

		public bool AuraEffect1 { get; set; }

		public bool AuraEffect2 { get; set; }

		public bool Get(PaletteColorSlot slot)
		{
			return slot switch
			{
				PaletteColorSlot.SwordBody1 => SwordBody1,
				PaletteColorSlot.SwordBody2 => SwordBody2,
				PaletteColorSlot.Blade1 => Blade1,
				PaletteColorSlot.Blade2 => Blade2,
				PaletteColorSlot.Flame1 => Flame1,
				PaletteColorSlot.Flame2 => Flame2,
				PaletteColorSlot.AuraEffect1 => AuraEffect1,
				PaletteColorSlot.AuraEffect2 => AuraEffect2,
				_ => true
			};
		}

		public void Set(PaletteColorSlot slot, bool enabled)
		{
			switch (slot)
			{
				case PaletteColorSlot.SwordBody1:
					SwordBody1 = enabled;
					break;
				case PaletteColorSlot.SwordBody2:
					SwordBody2 = enabled;
					break;
				case PaletteColorSlot.Blade1:
					Blade1 = enabled;
					break;
				case PaletteColorSlot.Blade2:
					Blade2 = enabled;
					break;
				case PaletteColorSlot.Flame1:
					Flame1 = enabled;
					break;
				case PaletteColorSlot.Flame2:
					Flame2 = enabled;
					break;
				case PaletteColorSlot.AuraEffect1:
					AuraEffect1 = enabled;
					break;
				case PaletteColorSlot.AuraEffect2:
					AuraEffect2 = enabled;
					break;
			}
		}

		public static PaletteRgbMask CreateAllEnabled()
		{
			return new PaletteRgbMask
			{
				SwordBody1 = true,
				SwordBody2 = true,
				Blade1 = true,
				Blade2 = true,
				Flame1 = true,
				Flame2 = true,
				AuraEffect1 = true,
				AuraEffect2 = true
			};
		}
	}

	private sealed class PaletteColorSet
	{
		public static PaletteColorSet From(
			Color swordBodyColor1,
			Color swordBodyColor2,
			Color bladeColor1,
			Color bladeColor2,
			Color flameColor1,
			Color flameColor2,
			Color auraEffect1Color,
			Color auraEffect2Color)
		{
			return new PaletteColorSet
			{
				SwordBodyColor1 = swordBodyColor1,
				SwordBodyColor2 = swordBodyColor2,
				BladeColor1 = bladeColor1,
				BladeColor2 = bladeColor2,
				FlameColor1 = flameColor1,
				FlameColor2 = flameColor2,
				AuraEffect1Color = auraEffect1Color,
				AuraEffect2Color = auraEffect2Color
			};
		}

		public Color SwordBodyColor1 { get; set; }

		public Color SwordBodyColor2 { get; set; }

		public Color BladeColor1 { get; set; }

		public Color BladeColor2 { get; set; }

		public Color FlameColor1 { get; set; }

		public Color FlameColor2 { get; set; }

		public Color AuraEffect1Color { get; set; }

		public Color AuraEffect2Color { get; set; }

		public Color Get(PaletteColorSlot slot)
		{
			return slot switch
			{
				PaletteColorSlot.SwordBody1 => SwordBodyColor1,
				PaletteColorSlot.SwordBody2 => SwordBodyColor2,
				PaletteColorSlot.Blade1 => BladeColor1,
				PaletteColorSlot.Blade2 => BladeColor2,
				PaletteColorSlot.Flame1 => FlameColor1,
				PaletteColorSlot.Flame2 => FlameColor2,
				PaletteColorSlot.AuraEffect1 => AuraEffect1Color,
				PaletteColorSlot.AuraEffect2 => AuraEffect2Color,
				_ => Colors.White
			};
		}

		public void Set(PaletteColorSlot slot, Color color)
		{
			switch (slot)
			{
				case PaletteColorSlot.SwordBody1:
					SwordBodyColor1 = color;
					break;
				case PaletteColorSlot.SwordBody2:
					SwordBodyColor2 = color;
					break;
				case PaletteColorSlot.Blade1:
					BladeColor1 = color;
					break;
				case PaletteColorSlot.Blade2:
					BladeColor2 = color;
					break;
				case PaletteColorSlot.Flame1:
					FlameColor1 = color;
					break;
				case PaletteColorSlot.Flame2:
					FlameColor2 = color;
					break;
				case PaletteColorSlot.AuraEffect1:
					AuraEffect1Color = color;
					break;
				case PaletteColorSlot.AuraEffect2:
					AuraEffect2Color = color;
					break;
			}
		}

		public PaletteColorSet Clone()
		{
			return new PaletteColorSet
			{
				SwordBodyColor1 = SwordBodyColor1,
				SwordBodyColor2 = SwordBodyColor2,
				BladeColor1 = BladeColor1,
				BladeColor2 = BladeColor2,
				FlameColor1 = FlameColor1,
				FlameColor2 = FlameColor2,
				AuraEffect1Color = AuraEffect1Color,
				AuraEffect2Color = AuraEffect2Color
			};
		}

		public static PaletteColorSet CreateDefaultCustom()
		{
			return CreateDefaultPreset();
		}

		public static PaletteColorSet CreateDefaultPreset()
		{
			return From(
				DefaultSwordBodyColor1,
				DefaultSwordBodyColor2,
				DefaultBladeColor1,
				DefaultBladeColor2,
				DefaultFlameColor1,
				DefaultFlameColor2,
				DefaultAuraEffect1Color,
				DefaultAuraEffect2Color);
		}
	}

}
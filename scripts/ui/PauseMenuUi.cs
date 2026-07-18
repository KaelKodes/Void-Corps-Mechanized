using System;
using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// In-match pause overlay. ProcessMode Always so it works while the tree is paused.
/// </summary>
public partial class PauseMenuUi : Control
{
	private enum Page
	{
		Root,
		Options,
		Keybinds,
		ConfirmMain,
		ConfirmQuit
	}

	private Page _page = Page.Root;
	private VBoxContainer? _content;
	private Label? _overlapLabel;
	private string? _listeningAction;
	private bool _rebindArmed;
	private readonly Dictionary<string, Button> _bindButtons = new();
	private bool _open;

	public bool IsOpen => _open;

	[Signal] public delegate void ClosedEventHandler();

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Visible = false;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public void Open()
	{
		_open = true;
		_page = Page.Root;
		_listeningAction = null;
		_rebindArmed = false;
		Visible = true;
		MouseFilter = MouseFilterEnum.Stop;
		Rebuild();
		MoveToFront();
	}

	public void Close()
	{
		_open = false;
		_listeningAction = null;
		_rebindArmed = false;
		Visible = false;
		MouseFilter = MouseFilterEnum.Ignore;
		EmitSignal(SignalName.Closed);
	}

	public override void _Process(double delta)
	{
		if (!_open || _listeningAction == null || _rebindArmed)
			return;

		// Wait until the click that started rebind is fully released so LMB
		// doesn't instantly bind to the row you just pressed.
		if (!Input.IsMouseButtonPressed(MouseButton.Left)
		    && !Input.IsMouseButtonPressed(MouseButton.Right)
		    && !Input.IsMouseButtonPressed(MouseButton.Middle)
		    && !Input.IsMouseButtonPressed(MouseButton.Xbutton1)
		    && !Input.IsMouseButtonPressed(MouseButton.Xbutton2))
		{
			_rebindArmed = true;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!_open || _listeningAction == null)
			return;

		if (!_rebindArmed)
		{
			// Swallow the activating click so it can't fall through to other UI.
			if (@event is InputEventMouseButton)
				GetViewport()?.SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false } key)
		{
			if (key.PhysicalKeycode == Key.Escape)
			{
				_listeningAction = null;
				_rebindArmed = false;
				Rebuild();
				GetViewport()?.SetInputAsHandled();
				return;
			}

			InputBindings.Rebind(_listeningAction, CloneForBind(key));
			FinishRebind();
			GetViewport()?.SetInputAsHandled();
			return;
		}

		if (@event is InputEventMouseButton { Pressed: true } mouse)
		{
			// Wheel events are noisy as binds; still allow them if the user wants.
			InputBindings.Rebind(_listeningAction, CloneForBind(mouse));
			FinishRebind();
			GetViewport()?.SetInputAsHandled();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_open || _listeningAction != null)
			return;

		if (@event.IsActionPressed("pause"))
		{
			CloseAndResume();
			GetViewport()?.SetInputAsHandled();
		}
	}

	private static InputEvent CloneForBind(InputEventKey key)
	{
		return new InputEventKey
		{
			PhysicalKeycode = key.PhysicalKeycode,
			Keycode = key.Keycode
		};
	}

	private static InputEvent CloneForBind(InputEventMouseButton mouse)
	{
		return new InputEventMouseButton
		{
			ButtonIndex = mouse.ButtonIndex
		};
	}

	private void FinishRebind()
	{
		_listeningAction = null;
		_rebindArmed = false;
		GetNodeOrNull<GameSession>("/root/GameSession")?.SaveProfile();
		Rebuild();
	}

	private void CloseAndResume()
	{
		Close();
		if (GetTree() != null)
			GetTree().Paused = false;
	}

	private void Rebuild()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_bindButtons.Clear();
		_content = null;
		_overlapLabel = null;

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.04f, 0.82f),
			MouseFilter = MouseFilterEnum.Stop
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = _page == Page.Options ? new Vector2(520, 520) : new Vector2(520, 420)
		};
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.09f, 0.11f, 0.98f),
			BorderColor = new Color(0.62f, 0.5f, 0.28f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 18,
			ContentMarginTop = 16,
			ContentMarginRight = 18,
			ContentMarginBottom = 16,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8
		});
		center.AddChild(panel);

		_content = new VBoxContainer();
		_content.AddThemeConstantOverride("separation", 10);
		panel.AddChild(_content);

		switch (_page)
		{
			case Page.Options:
				BuildOptions();
				break;
			case Page.Keybinds:
				BuildKeybinds();
				break;
			case Page.ConfirmMain:
				BuildConfirm("Return to Main Menu?", () =>
				{
					GetTree().Paused = false;
					GetNodeOrNull<GameSession>("/root/GameSession")?.SaveProfile();
					GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
				});
				break;
			case Page.ConfirmQuit:
				BuildConfirm("Quit Mechanize?", () => GetTree().Quit());
				break;
			default:
				BuildRoot();
				break;
		}
	}

	private void BuildRoot()
	{
		var title = new Label
		{
			Text = "PAUSED",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.85f, 0.7f, 0.38f)
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		_content!.AddChild(title);

		_content.AddChild(MakeButton("RESUME", () =>
		{
			SfxService.Click();
			CloseAndResume();
		}));
		_content.AddChild(MakeButton("OPTIONS", () =>
		{
			SfxService.Click();
			_page = Page.Options;
			Rebuild();
		}));
		_content.AddChild(MakeButton("KEYBINDS", () =>
		{
			SfxService.Click();
			_page = Page.Keybinds;
			Rebuild();
		}));
		_content.AddChild(MakeButton("MAIN MENU", () =>
		{
			SfxService.Click();
			_page = Page.ConfirmMain;
			Rebuild();
		}));
		_content.AddChild(MakeButton("QUIT", () =>
		{
			SfxService.Click();
			_page = Page.ConfirmQuit;
			Rebuild();
		}));
	}

	private void BuildOptions()
	{
		AddHeader("OPTIONS");

		_content!.AddChild(MakeSectionLabel("Mech HUD"));
		_content.AddChild(MakeSliderRow(
			"Scale",
			GameSettings.HudScale,
			0.5f,
			1.5f,
			GameSettings.SetHudScale,
			() => $"{Mathf.RoundToInt(GameSettings.HudScale * 100f)}%"));
		_content.AddChild(MakeSliderRow(
			"Horizontal",
			GameSettings.HudOffsetX,
			0f,
			1f,
			GameSettings.SetHudOffsetX,
			() => GameSettings.HudOffsetX < 0.05f
				? "Left"
				: GameSettings.HudOffsetX > 0.95f
					? "Right"
					: Mathf.Abs(GameSettings.HudOffsetX - 0.5f) < 0.03f
						? "Center"
						: $"{Mathf.RoundToInt(GameSettings.HudOffsetX * 100f)}%"));
		_content.AddChild(MakeSliderRow(
			"Vertical",
			GameSettings.HudOffsetY,
			0f,
			1f,
			GameSettings.SetHudOffsetY,
			() => GameSettings.HudOffsetY < 0.05f
				? "Bottom"
				: $"{Mathf.RoundToInt(GameSettings.HudOffsetY * 100f)}% lift"));

		_content.AddChild(MakeButton(
			GameSettings.MetersBesideMech
				? "PWR / HEAT: Beside MAP"
				: "PWR / HEAT: Corner HUD",
			() =>
			{
				SfxService.Click();
				GameSettings.SetMetersBesideMech(!GameSettings.MetersBesideMech);
				Rebuild();
			}));

		_content.AddChild(MakeButton("Reset HUD layout", () =>
		{
			SfxService.Click();
			GameSettings.ResetHudLayout();
			Rebuild();
		}));

		var stub = new Label
		{
			Text = "Audio and graphics options coming soon.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.55f, 0.6f, 0.65f)
		};
		stub.AddThemeFontSizeOverride("font_size", 13);
		_content.AddChild(stub);

		_content.AddChild(MakeButton("Back", () =>
		{
			_page = Page.Root;
			Rebuild();
		}));
	}

	private static Label MakeSectionLabel(string text)
	{
		var label = new Label
		{
			Text = text,
			Modulate = new Color(0.78f, 0.66f, 0.38f)
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		return label;
	}

	private static Control MakeSliderRow(string title, float value, float min, float max, Action<float> onChanged, Func<string> format)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 4);

		var header = new HBoxContainer();
		var name = new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var valueLabel = new Label
		{
			Text = format(),
			HorizontalAlignment = HorizontalAlignment.Right,
			Modulate = new Color(0.75f, 0.8f, 0.85f)
		};
		header.AddChild(name);
		header.AddChild(valueLabel);
		box.AddChild(header);

		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = 0.01,
			Value = value,
			CustomMinimumSize = new Vector2(0, 22),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		slider.ValueChanged += v =>
		{
			onChanged((float)v);
			valueLabel.Text = format();
		};
		box.AddChild(slider);
		return box;
	}

	private void BuildKeybinds()
	{
		AddHeader("KEYBINDS");

		_overlapLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.95f, 0.55f, 0.35f)
		};
		_overlapLabel.AddThemeFontSizeOverride("font_size", 13);
		_content!.AddChild(_overlapLabel);

		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 260),
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_content.AddChild(scroll);

		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(list);

		string? lastGroup = null;
		foreach (var info in InputBindings.All)
		{
			if (info.Group != lastGroup)
			{
				lastGroup = info.Group;
				var group = new Label
				{
					Text = info.Group,
					Modulate = new Color(0.78f, 0.66f, 0.38f)
				};
				group.AddThemeFontSizeOverride("font_size", 14);
				list.AddChild(group);
			}

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			var name = new Label
			{
				Text = info.Label,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			row.AddChild(name);

			var bindBtn = new Button
			{
				Text = InputBindings.FormatAction(info.Action),
				CustomMinimumSize = new Vector2(160, 32)
			};
			var action = info.Action;
			bindBtn.Pressed += () =>
			{
				_listeningAction = action;
				_rebindArmed = false;
				bindBtn.Text = "Press key / mouse...";
				SfxService.Click();
			};
			_bindButtons[action] = bindBtn;
			row.AddChild(bindBtn);

			var reset = new Button { Text = "↺", CustomMinimumSize = new Vector2(36, 32) };
			reset.Pressed += () =>
			{
				InputBindings.ResetAction(action);
				GetNodeOrNull<GameSession>("/root/GameSession")?.SaveProfile();
				Rebuild();
			};
			row.AddChild(reset);
			list.AddChild(row);
		}

		RefreshOverlapWarning();

		var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		footer.AddThemeConstantOverride("separation", 10);
		_content.AddChild(footer);
		footer.AddChild(MakeButton("Reset All", () =>
		{
			InputBindings.ResetAll();
			GetNodeOrNull<GameSession>("/root/GameSession")?.SaveProfile();
			Rebuild();
		}, 140));
		footer.AddChild(MakeButton("Back", () =>
		{
			_page = Page.Root;
			Rebuild();
		}, 140));
	}

	private void RefreshOverlapWarning()
	{
		if (_overlapLabel == null)
			return;
		var summary = InputBindings.OverlapSummary();
		_overlapLabel.Text = summary ?? "No overlapping keybinds.";
		_overlapLabel.Modulate = summary == null
			? new Color(0.5f, 0.7f, 0.55f)
			: new Color(0.95f, 0.55f, 0.35f);

		var overlaps = new HashSet<string>(InputBindings.FindOverlaps());
		foreach (var (action, button) in _bindButtons)
		{
			button.Modulate = overlaps.Contains(action)
				? new Color(1f, 0.7f, 0.45f)
				: Colors.White;
		}
	}

	private void BuildConfirm(string message, System.Action confirm)
	{
		AddHeader("CONFIRM");
		var body = new Label
		{
			Text = message,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_content!.AddChild(body);
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 12);
		_content.AddChild(row);
		row.AddChild(MakeButton("Cancel", () =>
		{
			_page = Page.Root;
			Rebuild();
		}, 140));
		row.AddChild(MakeButton("Confirm", () =>
		{
			SfxService.Confirm();
			confirm();
		}, 140));
	}

	private void AddHeader(string text)
	{
		var title = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.85f, 0.7f, 0.38f)
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		_content!.AddChild(title);
	}

	private static Button MakeButton(string text, System.Action onPress, float width = 280)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(width, 40),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		button.AddThemeFontSizeOverride("font_size", 16);
		button.Pressed += onPress;
		return button;
	}
}

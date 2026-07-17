using Godot;

namespace Mechanize;

/// <summary>Shared Void Corps industrial UI chrome for garage, shop, and overlays.</summary>
public static class MechUiTheme
{
	public static readonly Color Dim = new(0.02f, 0.03f, 0.045f, 0.94f);
	public static readonly Color PanelBg = new(0.055f, 0.07f, 0.09f, 0.97f);
	public static readonly Color PanelBgDeep = new(0.04f, 0.05f, 0.065f, 0.98f);
	public static readonly Color Border = new(0.62f, 0.5f, 0.28f);
	public static readonly Color BorderDim = new(0.38f, 0.32f, 0.2f);
	public static readonly Color Accent = new(0.85f, 0.7f, 0.38f);
	public static readonly Color AccentHot = new(0.95f, 0.82f, 0.42f);
	public static readonly Color Cyan = new(0.45f, 0.78f, 0.92f);
	public static readonly Color Muted = new(0.58f, 0.64f, 0.7f);
	public static readonly Color Danger = new(0.9f, 0.38f, 0.32f);
	public static readonly Color Success = new(0.5f, 0.82f, 0.42f);
	public static readonly Color ChipBg = new(0.07f, 0.09f, 0.11f, 0.96f);
	public static readonly Color Text = new(0.88f, 0.9f, 0.92f);

	public static StyleBoxFlat MakePanelStyle(float margin = 14f, bool deep = false, Color? border = null)
	{
		var b = border ?? Border;
		return new StyleBoxFlat
		{
			BgColor = deep ? PanelBgDeep : PanelBg,
			BorderColor = b,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = margin,
			ContentMarginTop = margin,
			ContentMarginRight = margin,
			ContentMarginBottom = margin,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		};
	}

	public static PanelContainer MakePanel(string name, float minWidth = 0f, bool deep = false)
	{
		var panel = new PanelContainer { Name = name };
		if (minWidth > 0f)
			panel.CustomMinimumSize = new Vector2(minWidth, 0f);
		panel.AddThemeStyleboxOverride("panel", MakePanelStyle(deep: deep));
		return panel;
	}

	public static Control MakeDimOverlay()
	{
		var root = new Control
		{
			Name = "DimOverlay",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		var dim = new ColorRect
		{
			Color = Dim,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(dim);

		var topRule = new ColorRect
		{
			Color = Accent,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		topRule.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		topRule.OffsetBottom = 3;
		root.AddChild(topRule);

		var bottomRule = new ColorRect
		{
			Color = Accent,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		bottomRule.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		bottomRule.OffsetTop = -3;
		root.AddChild(bottomRule);

		return root;
	}

	public static Label MakeSectionLabel(string text)
	{
		var label = new Label
		{
			Text = text.StartsWith("//") ? text : $"// {text}",
			Modulate = Accent
		};
		label.AddThemeFontSizeOverride("font_size", 13);
		return label;
	}

	public static VBoxContainer MakeHeaderStrip(string title, string subtitle)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 4);

		var brand = new Label
		{
			Text = "VOID CORPS  ·  MECHANIZE",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = Accent.Darkened(0.15f)
		};
		brand.AddThemeFontSizeOverride("font_size", 12);
		box.AddChild(brand);

		var titleLabel = new Label
		{
			Name = "Title",
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = Text
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 28);
		box.AddChild(titleLabel);

		var sub = new Label
		{
			Name = "Subtitle",
			Text = subtitle,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = Muted,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		sub.AddThemeFontSizeOverride("font_size", 13);
		box.AddChild(sub);

		return box;
	}

	public static void StylePrimaryButton(Button button)
	{
		var normal = MakeButtonStyle(Accent.Darkened(0.55f), Accent, 2);
		var hover = MakeButtonStyle(Accent.Darkened(0.4f), AccentHot, 2);
		var pressed = MakeButtonStyle(Accent.Darkened(0.65f), Accent, 2);
		var disabled = MakeButtonStyle(new Color(0.12f, 0.13f, 0.15f), BorderDim, 1);
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
		button.AddThemeStyleboxOverride("disabled", disabled);
		button.AddThemeColorOverride("font_color", AccentHot);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", Accent);
		button.AddThemeColorOverride("font_disabled_color", Muted);
	}

	public static void StyleGhostButton(Button button)
	{
		var normal = MakeButtonStyle(new Color(0.08f, 0.1f, 0.12f, 0.9f), BorderDim, 1);
		var hover = MakeButtonStyle(new Color(0.12f, 0.14f, 0.17f, 0.95f), Border, 2);
		var pressed = MakeButtonStyle(new Color(0.06f, 0.08f, 0.1f, 0.95f), Border, 2);
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
		button.AddThemeColorOverride("font_color", Muted);
		button.AddThemeColorOverride("font_hover_color", Text);
		button.AddThemeColorOverride("font_pressed_color", Accent);
	}

	public static StyleBoxFlat MakeChipStyle(bool selected, Color? tint = null)
	{
		var border = selected ? AccentHot : tint ?? new Color(0.35f, 0.4f, 0.45f);
		return new StyleBoxFlat
		{
			BgColor = ChipBg,
			BorderColor = border,
			BorderWidthLeft = selected ? 3 : 2,
			BorderWidthTop = selected ? 3 : 2,
			BorderWidthRight = selected ? 3 : 2,
			BorderWidthBottom = selected ? 3 : 2,
			ContentMarginLeft = 6,
			ContentMarginTop = 6,
			ContentMarginRight = 6,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		};
	}

	private static StyleBoxFlat MakeButtonStyle(Color bg, Color border, int borderWidth) => new()
	{
		BgColor = bg,
		BorderColor = border,
		BorderWidthLeft = borderWidth,
		BorderWidthTop = borderWidth,
		BorderWidthRight = borderWidth,
		BorderWidthBottom = borderWidth,
		ContentMarginLeft = 14,
		ContentMarginTop = 8,
		ContentMarginRight = 14,
		ContentMarginBottom = 8,
		CornerRadiusTopLeft = 2,
		CornerRadiusTopRight = 2,
		CornerRadiusBottomRight = 2,
		CornerRadiusBottomLeft = 2
	};
}

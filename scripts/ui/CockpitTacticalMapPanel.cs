using Godot;

namespace Mechanize;

/// <summary>
/// WingL cockpit panel: claim / mission header above a reserved tactical-map body.
/// </summary>
public partial class CockpitTacticalMapPanel : Control
{
	private Label? _claimLine;
	private Label? _contractLine;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(280, 220);
		Build();
	}

	private void Build()
	{
		var panel = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.055f, 0.07f, 0.92f),
			BorderColor = new Color(0.35f, 0.55f, 0.42f, 0.75f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 6,
			ContentMarginRight = 8,
			ContentMarginBottom = 8
		});
		AddChild(panel);

		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 4);
		panel.AddChild(col);

		_claimLine = new Label
		{
			Text = "—",
			Modulate = MechUiTheme.Accent,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_claimLine.AddThemeFontSizeOverride("font_size", 9);
		col.AddChild(_claimLine);

		_contractLine = new Label
		{
			Text = "",
			Modulate = new Color(0.75f, 0.82f, 0.88f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_contractLine.AddThemeFontSizeOverride("font_size", 8);
		col.AddChild(_contractLine);

		var body = new Label
		{
			Text = "// TACTICAL MAP\nReserved for sector navigation.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Modulate = new Color(0.55f, 0.72f, 0.62f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		body.AddThemeFontSizeOverride("font_size", 10);
		col.AddChild(body);
	}

	public void SetHeader(string claimLine, string contractLine)
	{
		if (_claimLine != null)
			_claimLine.Text = string.IsNullOrEmpty(claimLine) ? "—" : claimLine;
		if (_contractLine != null)
			_contractLine.Text = contractLine ?? "";
	}
}

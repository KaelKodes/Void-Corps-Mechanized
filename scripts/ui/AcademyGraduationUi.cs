using Godot;

namespace Mechanize;

/// <summary>
/// Graduation + short chat log acknowledging the active mode's job convention.
/// </summary>
public partial class AcademyGraduationUi : Control
{
	private Label? _body;
	private Button? _continue;
	private int _stage;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MusicService.Cue(MusicCue.Menu);
		Build();
		ShowStage(0);
	}

	private void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();

		var bg = new ColorRect
		{
			Color = new Color(0.04f, 0.06f, 0.08f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		var root = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.OffsetLeft = 160;
		root.OffsetRight = -160;
		root.AddThemeConstantOverride("separation", 16);
		AddChild(root);

		var title = new Label
		{
			Text = "GRADUATION",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.85f, 0.7f, 0.38f)
		};
		title.AddThemeFontSizeOverride("font_size", 36);
		root.AddChild(title);

		_body = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.75f, 0.82f, 0.88f)
		};
		_body.AddThemeFontSizeOverride("font_size", 17);
		root.AddChild(_body);

		_continue = new Button
		{
			Text = "Continue",
			CustomMinimumSize = new Vector2(260, 46),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		_continue.AddThemeFontSizeOverride("font_size", 18);
		_continue.Pressed += OnContinue;
		root.AddChild(_continue);
	}

	private void ShowStage(int stage)
	{
		_stage = stage;
		if (_body == null || _continue == null)
			return;

		switch (stage)
		{
			case 0:
				_body.Text =
					"MAP Cadet Program — complete.\n\n" +
					"Your wings are certified. The training chassis is recalled to the academy pool.\n" +
					"You leave the range without a personal MAP.";
				_continue.Text = "Acknowledge";
				break;
			case 1:
				var solar = GetNodeOrNull<GameSession>("/root/GameSession")?.Campaign?.SolarOnboarding == true;
				_body.Text =
					"COMM LOG\n" +
					"────────────────────────────────────\n" +
					"YOU  ·  Loaner's gone. No kit, no contract.\n" +
					"YOU  ·  Convention circuit is the only door open.\n" +
					(solar
						? "YOU  ·  Walk in, compare frontier companies, earn a charter and chassis.\n"
						: "YOU  ·  Walk in, pick a manufacturer trial, earn a chassis.\n") +
					"────────────────────────────────────\n\n" +
					(solar ? "Next: Frontier Job Convention." : "Next: Big Four Convention.");
				_continue.Text = solar ? "Enter Job Convention" : "Open sector map";
				break;
			default:
				Finish();
				break;
		}
	}

	private void OnContinue()
	{
		SfxService.Confirm();
		if (_stage < 1)
			ShowStage(_stage + 1);
		else
			Finish();
	}

	private void Finish()
	{
		var session = GetNodeOrNull<GameSession>("/root/GameSession");
		session?.CompleteAcademyGraduation();
		GetTree().ChangeSceneToFile(session?.Campaign?.SolarOnboarding == true
			? "res://scenes/convention_hall.tscn"
			: "res://scenes/campaign_map.tscn");
	}
}

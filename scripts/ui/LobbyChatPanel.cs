using System.Text;
using Godot;

namespace Mechanize;

/// <summary>Shared lobby chat panel — history + input wired to NetSession.</summary>
public partial class LobbyChatPanel : VBoxContainer
{
	private RichTextLabel? _log;
	private LineEdit? _input;
	private NetSession? _net;

	public static LobbyChatPanel Create(NetSession? net, float minHeight = 140f)
	{
		var panel = new LobbyChatPanel();
		panel.Build(net, minHeight);
		return panel;
	}

	public void Bind(NetSession? net)
	{
		Unbind();
		_net = net;
		if (_net != null)
			_net.ChatReceived += OnChat;
		RefreshLog();
	}

	public void Unbind()
	{
		if (_net != null)
			_net.ChatReceived -= OnChat;
		_net = null;
	}

	public override void _ExitTree() => Unbind();

	private void Build(NetSession? net, float minHeight)
	{
		Name = "LobbyChat";
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;
		AddThemeConstantOverride("separation", 6);

		var title = new Label
		{
			Text = "Chat",
			Modulate = MechUiTheme.Muted
		};
		title.AddThemeFontSizeOverride("font_size", 14);
		AddChild(title);

		var frame = MechUiTheme.MakePanel("ChatFrame", deep: true);
		frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		frame.SizeFlagsVertical = SizeFlags.ExpandFill;
		frame.CustomMinimumSize = new Vector2(0, minHeight);
		AddChild(frame);

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 6);
		frame.AddChild(inner);

		_log = new RichTextLabel
		{
			BbcodeEnabled = false,
			FitContent = false,
			ScrollFollowing = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, minHeight - 48),
			Modulate = MechUiTheme.Text
		};
		_log.AddThemeFontSizeOverride("normal_font_size", 13);
		inner.AddChild(_log);

		_input = new LineEdit
		{
			PlaceholderText = "…",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 32)
		};
		_input.TextSubmitted += OnSubmit;
		inner.AddChild(_input);

		Bind(net);
	}

	private void OnSubmit(string text)
	{
		_net?.SendChat(text);
		if (_input != null)
			_input.Text = "";
	}

	private void OnChat((string Speaker, string Text) _) => RefreshLog();

	private void RefreshLog()
	{
		if (_log == null)
			return;
		var sb = new StringBuilder();
		if (_net != null)
		{
			foreach (var (speaker, text) in _net.ChatLines)
				sb.AppendLine($"{speaker}: {text}");
		}

		_log.Text = sb.ToString().TrimEnd();
	}
}

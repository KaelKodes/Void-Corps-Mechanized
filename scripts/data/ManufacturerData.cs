using Godot;

namespace Mechanize;

[GlobalClass]
public partial class ManufacturerData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public Color AccentColor { get; set; } = Colors.Gray;
	[Export] public string Blurb { get; set; } = "";
	[Export] public string Niche { get; set; } = "";
}

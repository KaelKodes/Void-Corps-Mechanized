using Godot;

namespace Mechanize;

/// <summary>
/// Visual identity for a speaker oscilloscope "portrait".
/// Same speech bank, different scope chrome and waveform behavior.
/// </summary>
public sealed class VoiceOscilloscopeStyle
{
	public required string Id { get; init; }
	public required Color Accent { get; init; }
	public Color Trace { get; init; } = Colors.White;
	public Color Ghost { get; init; } = Colors.Transparent;
	public Color Grid { get; init; } = new(0.2f, 0.25f, 0.3f, 0.45f);
	public Color Panel { get; init; } = new(0.03f, 0.04f, 0.055f, 1f);
	public Color Bezel { get; init; } = new(0.55f, 0.45f, 0.25f, 0.9f);
	public float LineWidth { get; init; } = 2.2f;
	public float GhostWidth { get; init; } = 1.2f;
	public float Amplitude { get; init; } = 1f;
	public float Frequency { get; init; } = 1f;
	public float Decay { get; init; } = 6.5f;
	public float IdleHum { get; init; } = 0.04f;
	public float GlowStrength { get; init; } = 0.35f;
	public float Squareness { get; init; } // 0 = sine, 1 = square-ish
	public float Harmonic { get; init; } // secondary wave mix
	public float GhostLag { get; init; } = 0.12f;
	public bool ShowReticles { get; init; }
	public bool ShowHexFrame { get; init; }
	public bool EmberFloor { get; init; }
	public bool ScanSweep { get; init; }
	public string Caption { get; init; } = "";

	public static VoiceOscilloscopeStyle ForManufacturer(string manufacturerId) => manufacturerId switch
	{
		"brimforge" => Brimforge,
		"ourotech" => OuroTech,
		"trinova" => Trinova,
		"lumina" => Lumina,
		_ => CorpOps
	};

	public static VoiceOscilloscopeStyle ForProfile(string profileId) => profileId switch
	{
		TextVoiceService.BrimforgeProfile => Brimforge,
		TextVoiceService.OuroTechProfile => OuroTech,
		TextVoiceService.TrinovaProfile => Trinova,
		TextVoiceService.LuminaProfile => Lumina,
		TextVoiceService.AcademyProfile => Academy,
		TextVoiceService.CorpOpsProfile => CorpOps,
		TextVoiceService.MicaProfile => Mica,
		_ => CorpOps
	};

	public static readonly VoiceOscilloscopeStyle Brimforge = new()
	{
		Id = "brimforge",
		Accent = new Color(0.72f, 0.38f, 0.18f),
		Trace = new Color(1f, 0.55f, 0.22f),
		Ghost = new Color(0.85f, 0.25f, 0.05f, 0.45f),
		Grid = new Color(0.45f, 0.22f, 0.1f, 0.4f),
		Panel = new Color(0.05f, 0.03f, 0.02f, 1f),
		Bezel = new Color(0.72f, 0.38f, 0.18f, 0.95f),
		LineWidth = 3.4f,
		GhostWidth = 2.2f,
		Amplitude = 1.25f,
		Frequency = 0.72f,
		Decay = 4.8f,
		IdleHum = 0.06f,
		GlowStrength = 0.55f,
		Squareness = 0.55f,
		Harmonic = 0.35f,
		GhostLag = 0.08f,
		EmberFloor = true,
		Caption = "GARRICK HOLT"
	};

	public static readonly VoiceOscilloscopeStyle OuroTech = new()
	{
		Id = "ourotech",
		Accent = new Color(0.25f, 0.55f, 0.78f),
		Trace = new Color(0.45f, 0.9f, 1f),
		Ghost = new Color(0.2f, 0.55f, 0.85f, 0.3f),
		Grid = new Color(0.2f, 0.45f, 0.6f, 0.5f),
		Panel = new Color(0.02f, 0.04f, 0.06f, 1f),
		Bezel = new Color(0.25f, 0.55f, 0.78f, 0.95f),
		LineWidth = 1.6f,
		GhostWidth = 0.9f,
		Amplitude = 0.85f,
		Frequency = 1.45f,
		Decay = 9f,
		IdleHum = 0.02f,
		GlowStrength = 0.22f,
		Squareness = 0.05f,
		Harmonic = 0.08f,
		GhostLag = 0.03f,
		ShowReticles = true,
		Caption = "SELENE VEY"
	};

	public static readonly VoiceOscilloscopeStyle Trinova = new()
	{
		Id = "trinova",
		Accent = new Color(0.35f, 0.72f, 0.42f),
		Trace = new Color(0.55f, 0.95f, 0.6f),
		Ghost = new Color(0.2f, 0.65f, 0.35f, 0.35f),
		Grid = new Color(0.2f, 0.45f, 0.28f, 0.42f),
		Panel = new Color(0.02f, 0.05f, 0.03f, 1f),
		Bezel = new Color(0.35f, 0.72f, 0.42f, 0.95f),
		LineWidth = 2.4f,
		GhostWidth = 1.4f,
		Amplitude = 1f,
		Frequency = 1f,
		Decay = 6.2f,
		IdleHum = 0.035f,
		GlowStrength = 0.3f,
		Squareness = 0.18f,
		Harmonic = 0.22f,
		GhostLag = 0.05f,
		Caption = "MARA KEEL"
	};

	public static readonly VoiceOscilloscopeStyle Lumina = new()
	{
		Id = "lumina",
		Accent = new Color(0.78f, 0.62f, 0.95f),
		Trace = new Color(0.92f, 0.78f, 1f),
		Ghost = new Color(0.55f, 0.3f, 0.95f, 0.55f),
		Grid = new Color(0.4f, 0.28f, 0.55f, 0.35f),
		Panel = new Color(0.04f, 0.02f, 0.06f, 1f),
		Bezel = new Color(0.78f, 0.62f, 0.95f, 0.95f),
		LineWidth = 2f,
		GhostWidth = 2.8f,
		Amplitude = 1.1f,
		Frequency = 0.88f,
		Decay = 3.8f,
		IdleHum = 0.05f,
		GlowStrength = 0.7f,
		Squareness = 0.12f,
		Harmonic = 0.55f,
		GhostLag = 0.12f,
		ShowHexFrame = true,
		ScanSweep = true,
		Caption = "ILYRA SENN"
	};

	public static readonly VoiceOscilloscopeStyle Academy = new()
	{
		Id = "academy",
		Accent = MechUiTheme.Accent,
		Trace = MechUiTheme.AccentHot,
		Ghost = MechUiTheme.Accent with { A = 0.35f },
		Grid = MechUiTheme.BorderDim with { A = 0.45f },
		Bezel = MechUiTheme.Border,
		LineWidth = 2.2f,
		Amplitude = 0.95f,
		Frequency = 1.1f,
		Caption = "INSTR. CALDER"
	};

	public static readonly VoiceOscilloscopeStyle CorpOps = new()
	{
		Id = "corp_ops",
		Accent = MechUiTheme.Cyan,
		Trace = new Color(0.65f, 0.9f, 1f),
		Ghost = MechUiTheme.Cyan with { A = 0.3f },
		Bezel = MechUiTheme.Cyan,
		LineWidth = 2f,
		Amplitude = 1f,
		Frequency = 1.15f,
		Caption = "JAX ROOK"
	};

	public static readonly VoiceOscilloscopeStyle Mica = new()
	{
		Id = "mica",
		Accent = new Color(0.55f, 0.75f, 0.85f),
		Trace = new Color(0.75f, 0.95f, 1f),
		Ghost = new Color(0.4f, 0.7f, 0.85f, 0.25f),
		Bezel = new Color(0.45f, 0.65f, 0.75f),
		LineWidth = 1.4f,
		Amplitude = 0.7f,
		Frequency = 1.8f,
		Decay = 11f,
		IdleHum = 0.015f,
		GlowStrength = 0.18f,
		Squareness = 0.4f,
		ShowReticles = true,
		Caption = "MICA"
	};
}

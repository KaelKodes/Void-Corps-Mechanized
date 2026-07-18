using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mechanize;

/// <summary>
/// Tunable presentation voice built from Kael's short letter/digraph recordings.
/// Dialogue text remains authoritative; speech may be cancelled or disabled without
/// affecting UI and game flow.
/// </summary>
public partial class TextVoiceService : Node
{
	private const string BankDirectory = "res://audio/text_to_voice";
	private const string ManifestPath = BankDirectory + "/bank.json";
	// A single articulation channel deliberately cuts each recording short.
	// Letting whole vowel samples overlap makes the bank harsh and unintelligible.
	private const int PlayerCount = 1;

	public const string BrimforgeProfile = "brimforge_garrick";
	public const string OuroTechProfile = "ourotech_selene";
	public const string TrinovaProfile = "trinova_mara";
	public const string LuminaProfile = "lumina_ilyra";
	public const string AcademyProfile = "academy_calder";
	public const string CorpOpsProfile = "corp_ops_rook";
	public const string MicaProfile = "mica";

	private readonly Dictionary<string, AudioStream> _clips = new();
	private readonly Dictionary<string, VoiceProfile> _profiles = new();
	private readonly List<AudioStreamPlayer> _players = new();
	private int _playerIndex;
	private ulong _sequence;
	private float _visualizationEnergy;

	private enum SpeechBeat
	{
		Token,
		Space,
		Comma,
		Sentence,
		Silent
	}

	private readonly record struct SpeechUnit(SpeechBeat Beat, string Token, int SourceAdvance);

	public static TextVoiceService? Instance { get; private set; }
	public static IReadOnlyDictionary<string, VoiceProfile> Profiles =>
		Instance?._profiles ?? EmptyProfiles;

	/// <summary>0..~1.3 speech energy for oscilloscope portraits.</summary>
	public static float VisualizationEnergy => Instance?._visualizationEnergy ?? 0f;
	public static bool IsSpeaking { get; private set; }
	/// <summary>Fired for each articulated token: (isVowel, pitchScale).</summary>
	public static event System.Action<bool, float>? TokenSpoken;

	private static readonly IReadOnlyDictionary<string, VoiceProfile> EmptyProfiles =
		new Dictionary<string, VoiceProfile>();

	public override void _Ready()
	{
		Instance = this;
		RegisterProfiles();
		LoadBank();

		for (var i = 0; i < PlayerCount; i++)
		{
			var player = new AudioStreamPlayer
			{
				Name = $"TextVoice_{i}",
				Bus = "Sfx"
			};
			AddChild(player);
			_players.Add(player);
		}
	}

	public override void _Process(double delta)
	{
		if (_visualizationEnergy > 0f)
			_visualizationEnergy = Mathf.MoveToward(_visualizationEnergy, 0f, (float)delta * 14f);
	}

	public override void _ExitTree()
	{
		Stop();
		if (Instance == this)
			Instance = null;
	}

	/// <summary>Start a line, cancelling any text voice currently playing.</summary>
	public static void Speak(
		string text,
		string profileId,
		System.Action<int>? onReveal = null,
		System.Action? onComplete = null)
	{
		Instance?.StartLine(text, profileId, onReveal, onComplete);
	}

	/// <summary>Immediately cancel the active line and silence overlapping tokens.</summary>
	public static void Stop()
	{
		Instance?.StopInternal();
	}

	public static string ProfileForManufacturer(string manufacturerId) => manufacturerId switch
	{
		"brimforge" => BrimforgeProfile,
		"ourotech" => OuroTechProfile,
		"trinova" => TrinovaProfile,
		"lumina" => LuminaProfile,
		_ => CorpOpsProfile
	};

	private void StartLine(
		string text,
		string profileId,
		System.Action<int>? onReveal,
		System.Action? onComplete)
	{
		StopInternal();
		if (_clips.Count == 0 || !_profiles.TryGetValue(profileId, out var profile))
			return;

		var spokenText = StripSpeakerPrefix(text);
		if (string.IsNullOrWhiteSpace(spokenText))
			return;

		var sequence = _sequence;
		IsSpeaking = true;
		onReveal?.Invoke(0);
		_ = SpeakAsync(spokenText, profile, sequence, onReveal, onComplete);
	}

	private void StopInternal()
	{
		_sequence++;
		IsSpeaking = false;
		_visualizationEnergy = 0f;
		foreach (var player in _players)
			player.Stop();
	}

	private async Task SpeakAsync(
		string text,
		VoiceProfile profile,
		ulong sequence,
		System.Action<int>? onReveal,
		System.Action? onComplete)
	{
		var units = BuildSpeechUnits(text);
		var letterCount = CountLetters(text);
		var spokenLetters = 0;
		var revealed = 0;
		var rng = new RandomNumberGenerator
		{
			Seed = StableSeed(profile.Id + "|" + text.ToLowerInvariant())
		};
		var wordPitch = rng.RandfRange(-profile.PitchJitter, profile.PitchJitter);

		foreach (var unit in units)
		{
			if (sequence != _sequence)
				return;

			// Reveal the source characters at the exact beat that produces their sound.
			revealed = Mathf.Min(text.Length, revealed + unit.SourceAdvance);
			onReveal?.Invoke(revealed);

			switch (unit.Beat)
			{
				case SpeechBeat.Token when _clips.TryGetValue(unit.Token, out var clip):
				{
					var progress = letterCount <= 1 ? 0.5f : spokenLetters / (float)(letterCount - 1);
					var vowel = unit.Token is "a" or "e" or "i" or "o" or "u" or "oo";
					var player = PlayToken(clip, profile, progress, spokenLetters, wordPitch, vowel, rng);
					spokenLetters += Mathf.Max(1, unit.SourceAdvance);

					var ratio = vowel ? profile.VowelGateRatio : profile.ConsonantGateRatio;
					var gate = Mathf.Max(0.025f, profile.LetterSeconds * ratio);
					await WaitAsync(gate, sequence);
					if (sequence == _sequence && player.Stream == clip)
						player.Stop();
					await WaitAsync(Mathf.Max(0f, profile.LetterSeconds - gate), sequence);
					break;
				}
				case SpeechBeat.Space:
					await WaitAsync(profile.SpaceSeconds, sequence);
					wordPitch = rng.RandfRange(-profile.PitchJitter, profile.PitchJitter);
					break;
				case SpeechBeat.Comma:
					await WaitAsync(profile.CommaSeconds, sequence);
					break;
				case SpeechBeat.Sentence:
					await WaitAsync(profile.SentenceSeconds, sequence);
					break;
				case SpeechBeat.Silent:
					await WaitAsync(profile.LetterSeconds * 0.35f, sequence);
					break;
			}
		}

		if (sequence == _sequence && revealed < text.Length)
			onReveal?.Invoke(text.Length);

		if (sequence == _sequence)
		{
			IsSpeaking = false;
			onComplete?.Invoke();
		}
	}

	private AudioStreamPlayer PlayToken(
		AudioStream clip,
		VoiceProfile profile,
		float lineProgress,
		int tokenIndex,
		float wordPitch,
		bool vowel,
		RandomNumberGenerator rng)
	{
		var player = _players[_playerIndex++ % _players.Count];
		var contour = profile.PitchContour * (lineProgress * 2f - 1f);
		var alternate = tokenIndex % 2 == 0 ? profile.PitchAlternation : -profile.PitchAlternation;
		var microJitter = rng.RandfRange(-profile.TokenPitchJitter, profile.TokenPitchJitter);
		var vowelPitch = vowel ? profile.VowelPitchOffset : 0f;
		var volumeJitter = rng.RandfRange(-profile.VolumeJitterDb, profile.VolumeJitterDb);
		var vowelVolume = vowel ? profile.VowelVolumeOffsetDb : 0f;

		var pitchScale = Mathf.Clamp(
			profile.BasePitch + contour + alternate + wordPitch + microJitter + vowelPitch,
			0.5f,
			2f);

		player.Stop();
		player.Stream = clip;
		player.PitchScale = pitchScale;
		player.VolumeDb = profile.VolumeDb + volumeJitter + vowelVolume;
		player.Play();

		_visualizationEnergy = Mathf.Max(_visualizationEnergy, vowel ? 1.15f : 0.75f);
		TokenSpoken?.Invoke(vowel, pitchScale);
		return player;
	}

	private async Task WaitAsync(float seconds, ulong sequence)
	{
		if (seconds <= 0f || sequence != _sequence || !IsInsideTree())
			return;

		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}

	private void LoadBank()
	{
		_clips.Clear();
		if (!Godot.FileAccess.FileExists(ManifestPath))
		{
			GD.PushWarning($"TextVoiceService: missing manifest {ManifestPath}");
			return;
		}

		using var file = Godot.FileAccess.Open(ManifestPath, Godot.FileAccess.ModeFlags.Read);
		var parsed = file == null ? default : Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			GD.PushWarning($"TextVoiceService: invalid manifest {ManifestPath}");
			return;
		}

		var root = parsed.AsGodotDictionary();
		if (!root.ContainsKey("tokens") || root["tokens"].VariantType != Variant.Type.Dictionary)
		{
			GD.PushWarning("TextVoiceService: manifest has no token map.");
			return;
		}

		foreach (var (key, value) in root["tokens"].AsGodotDictionary())
		{
			var token = key.AsString().ToLowerInvariant();
			var path = $"{BankDirectory}/{value.AsString()}";
			var stream = ResourceLoader.Load<AudioStream>(path);
			if (stream == null)
			{
				GD.PushWarning($"TextVoiceService: could not load token '{token}' from {path}");
				continue;
			}

			_clips[token] = stream;
		}

		GD.Print($"TextVoiceService: {_clips.Count} speech tokens, {_profiles.Count} voice profiles.");
	}

	private void RegisterProfiles()
	{
		_profiles.Clear();

		// Garrick Holt — heavy, deliberate foundry-floor cadence.
		Add(new VoiceProfile(BrimforgeProfile, 0.78f, 0.025f, 0.078f, 0.032f, 0.13f, 0.24f, -4.5f)
		{
			PitchContour = -0.035f,
			VolumeJitterDb = 0.35f
		});

		// Selene Vey — fast, clipped, carefully controlled.
		Add(new VoiceProfile(OuroTechProfile, 1.27f, 0.018f, 0.046f, 0.018f, 0.075f, 0.14f, -7f)
		{
			PitchContour = 0.025f,
			PitchAlternation = 0.012f,
			VolumeJitterDb = 0.12f
		});

		// Mara Keel — steady, warm logistics rhythm.
		Add(new VoiceProfile(TrinovaProfile, 1.0f, 0.035f, 0.061f, 0.03f, 0.105f, 0.19f, -5.5f)
		{
			PitchContour = 0.01f,
			VolumeJitterDb = 0.25f
		});

		// Ilyra Senn — airy, slow, slightly uncanny without becoming a caricature.
		Add(new VoiceProfile(LuminaProfile, 1.18f, 0.075f, 0.086f, 0.052f, 0.16f, 0.29f, -9f)
		{
			PitchContour = -0.055f,
			PitchAlternation = 0.018f,
			VolumeJitterDb = 0.45f
		});

		// Instructor Calder — firm tutorial command voice.
		Add(new VoiceProfile(AcademyProfile, 0.9f, 0.018f, 0.055f, 0.023f, 0.09f, 0.17f, -5f)
		{
			PitchContour = -0.015f,
			VolumeJitterDb = 0.15f
		});

		// Jax Rook — conversational corp operations chatter.
		Add(new VoiceProfile(CorpOpsProfile, 1.07f, 0.055f, 0.052f, 0.025f, 0.095f, 0.17f, -5.5f)
		{
			PitchContour = 0.035f,
			VolumeJitterDb = 0.4f
		});

		// MICA — rapid, stable onboard interface voice.
		Add(new VoiceProfile(MicaProfile, 1.38f, 0.006f, 0.04f, 0.014f, 0.055f, 0.11f, -8f)
		{
			PitchAlternation = 0.028f,
			VolumeJitterDb = 0.05f
		});
	}

	private void Add(VoiceProfile profile) => _profiles[profile.Id] = profile;

	/// <summary>
	/// Build spoken beats while retaining a source-character count for synchronized
	/// text reveal. Pronunciation substitutions may consume or emit multiple sounds,
	/// but every original character is still revealed exactly once.
	/// </summary>
	private static List<SpeechUnit> BuildSpeechUnits(string text)
	{
		var units = new List<SpeechUnit>(text.Length);
		var lower = text.ToLowerInvariant();

		for (var i = 0; i < lower.Length; i++)
		{
			var ch = lower[i];
			if (char.IsWhiteSpace(ch))
			{
				units.Add(new SpeechUnit(SpeechBeat.Space, "", 1));
				continue;
			}

			if (ch is '.' or '?' or '!')
			{
				var count = 1;
				while (i + count < lower.Length && lower[i + count] is '.' or '?' or '!')
					count++;
				units.Add(new SpeechUnit(SpeechBeat.Sentence, "", count));
				i += count - 1;
				continue;
			}

			if (ch is ',' or ':' or ';' or '—' or '-')
			{
				units.Add(new SpeechUnit(SpeechBeat.Comma, "", 1));
				continue;
			}

			if (ch is not (>= 'a' and <= 'z'))
			{
				units.Add(new SpeechUnit(SpeechBeat.Silent, "", 1));
				continue;
			}

			var remaining = lower.AsSpan(i);
			if (remaining.StartsWith("tion"))
			{
				units.Add(new SpeechUnit(SpeechBeat.Token, "sh", 2));
				units.Add(new SpeechUnit(SpeechBeat.Token, "u", 1));
				units.Add(new SpeechUnit(SpeechBeat.Token, "n", 1));
				i += 3;
				continue;
			}
			if (remaining.StartsWith("ph"))
			{
				units.Add(new SpeechUnit(SpeechBeat.Token, "f", 2));
				i++;
				continue;
			}
			if (remaining.StartsWith("qu"))
			{
				units.Add(new SpeechUnit(SpeechBeat.Token, "k", 1));
				units.Add(new SpeechUnit(SpeechBeat.Token, "w", 1));
				i++;
				continue;
			}
			if (remaining.StartsWith("ck"))
			{
				units.Add(new SpeechUnit(SpeechBeat.Token, "k", 2));
				i++;
				continue;
			}
			if (remaining.StartsWith("th") || remaining.StartsWith("sh")
			                               || remaining.StartsWith("ch") || remaining.StartsWith("oo"))
			{
				units.Add(new SpeechUnit(SpeechBeat.Token, lower.Substring(i, 2), 2));
				i++;
				continue;
			}

			if (ch == 'x')
			{
				units.Add(new SpeechUnit(SpeechBeat.Token, "k", 1));
				units.Add(new SpeechUnit(SpeechBeat.Token, "s", 0));
				continue;
			}
			if (ch == 'c')
			{
				var soft = i + 1 < lower.Length && lower[i + 1] is 'e' or 'i' or 'y';
				units.Add(new SpeechUnit(SpeechBeat.Token, soft ? "s" : "k", 1));
				continue;
			}
			if (IsSilentFinalE(lower, i))
			{
				units.Add(new SpeechUnit(SpeechBeat.Silent, "", 1));
				continue;
			}

			units.Add(new SpeechUnit(SpeechBeat.Token, ch.ToString(), 1));
		}

		return units;
	}

	private static bool IsSilentFinalE(string text, int index)
	{
		if (text[index] != 'e' || index == 0
		    || (index + 1 < text.Length && text[index + 1] is >= 'a' and <= 'z')
		    || text[index - 1] is 'a' or 'e' or 'i' or 'o' or 'u')
			return false;

		var start = index;
		while (start > 0 && text[start - 1] is >= 'a' and <= 'z')
			start--;
		return index - start + 1 > 3;
	}

	private static string StripSpeakerPrefix(string text)
	{
		var divider = text.IndexOf('·');
		return divider >= 0 && divider + 1 < text.Length
			? text[(divider + 1)..].Trim()
			: text.Trim();
	}

	private static int CountLetters(string text)
	{
		var count = 0;
		foreach (var ch in text)
		{
			if (char.IsLetter(ch))
				count++;
		}
		return count;
	}

	private static ulong StableSeed(string value)
	{
		const ulong offset = 14695981039346656037UL;
		const ulong prime = 1099511628211UL;
		var hash = offset;
		foreach (var ch in value)
		{
			hash ^= ch;
			hash *= prime;
		}
		return hash;
	}
}

/// <summary>All playback variables defining one reusable character voice.</summary>
public sealed class VoiceProfile(
	string id,
	float basePitch,
	float pitchJitter,
	float letterSeconds,
	float spaceSeconds,
	float commaSeconds,
	float sentenceSeconds,
	float volumeDb)
{
	public string Id { get; } = id;
	public float BasePitch { get; } = basePitch;
	public float PitchJitter { get; } = pitchJitter;
	public float LetterSeconds { get; } = letterSeconds;
	public float SpaceSeconds { get; } = spaceSeconds;
	public float CommaSeconds { get; } = commaSeconds;
	public float SentenceSeconds { get; } = sentenceSeconds;
	public float VolumeDb { get; } = volumeDb;
	public float PitchContour { get; init; }
	public float PitchAlternation { get; init; }
	/// <summary>Small per-token movement; broad variation belongs at word level.</summary>
	public float TokenPitchJitter { get; init; } = 0.008f;
	public float VolumeJitterDb { get; init; }
	/// <summary>Short, softer, slightly brighter vowels avoid long harsh sample tails.</summary>
	public float VowelGateRatio { get; init; } = 0.58f;
	public float ConsonantGateRatio { get; init; } = 0.82f;
	public float VowelPitchOffset { get; init; } = 0.045f;
	public float VowelVolumeOffsetDb { get; init; } = -3f;
}

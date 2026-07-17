using System;
using Godot;

namespace Mechanize;

/// <summary>Tiny procedural SFX baker — prototype grade, no external assets.</summary>
public static class SfxSynth
{
	public const int SampleRate = 22050;

	public static AudioStreamWav Bake(string id)
	{
		var samples = id switch
		{
			"weapon_fire" => WeaponFire(),
			"weapon_hit" => WeaponHit(),
			"explosion" => Explosion(),
			"ui_click" => UiClick(),
			"ui_confirm" => UiConfirm(),
			"countdown" => CountdownBeep(),
			"fight" => FightSting(),
			"victory" => VictoryFanfare(),
			"defeat" => DefeatSting(),
			"capture" => CapturePulse(),
			"scrap" => ScrapPickup(),
			"alarm" => AlarmBuzz(),
			"disk" => DiskPickup(),
			_ => UiClick()
		};

		return ToStream(samples);
	}

	private static float[] WeaponFire()
	{
		var n = (int)(SampleRate * 0.14f);
		var s = new float[n];
		var rng = new Random(11);
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var env = Mathf.Exp(-t * 28f);
			var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
			var tone = Mathf.Sin(t * 520f * Mathf.Tau) * 0.35f
			           + Mathf.Sin(t * 180f * Mathf.Tau) * 0.25f;
			s[i] = (noise * 0.55f + tone) * env;
		}

		return s;
	}

	private static float[] WeaponHit()
	{
		var n = (int)(SampleRate * 0.09f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var env = Mathf.Exp(-t * 40f);
			var clang = Mathf.Sin(t * 1400f * Mathf.Tau) * 0.45f
			            + Mathf.Sin(t * 2200f * Mathf.Tau) * 0.25f
			            + Mathf.Sin(t * 900f * Mathf.Tau) * 0.2f;
			s[i] = clang * env;
		}

		return s;
	}

	private static float[] Explosion()
	{
		var n = (int)(SampleRate * 0.55f);
		var s = new float[n];
		var rng = new Random(42);
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var env = Mathf.Exp(-t * 4.5f);
			var rumble = Mathf.Sin(t * 55f * Mathf.Tau) * 0.55f;
			var noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.7f;
			var crack = i < SampleRate * 0.03f ? (float)(rng.NextDouble() * 2.0 - 1.0) : 0f;
			s[i] = (rumble + noise * env + crack * 0.5f) * Mathf.Min(1f, env * 1.4f);
		}

		return SoftClip(s, 0.9f);
	}

	private static float[] UiClick()
	{
		var n = (int)(SampleRate * 0.05f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var env = Mathf.Exp(-t * 55f);
			s[i] = Mathf.Sin(t * 880f * Mathf.Tau) * env * 0.45f;
		}

		return s;
	}

	private static float[] UiConfirm()
	{
		var n = (int)(SampleRate * 0.18f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var freq = t < 0.08f ? 520f : 780f;
			var env = t < 0.08f ? Mathf.Exp(-t * 20f) : Mathf.Exp(-(t - 0.08f) * 14f);
			s[i] = Mathf.Sin(t * freq * Mathf.Tau) * env * 0.4f;
		}

		return s;
	}

	private static float[] CountdownBeep()
	{
		var n = (int)(SampleRate * 0.12f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var env = t < 0.08f ? 1f : Mathf.Exp(-(t - 0.08f) * 40f);
			s[i] = Mathf.Sin(t * 660f * Mathf.Tau) * env * 0.5f;
		}

		return s;
	}

	private static float[] FightSting()
	{
		var n = (int)(SampleRate * 0.35f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var env = Mathf.Exp(-t * 6f);
			var tone = Mathf.Sin(t * 220f * Mathf.Tau) * 0.35f
			           + Mathf.Sin(t * 330f * Mathf.Tau) * 0.28f
			           + Mathf.Sin(t * 440f * Mathf.Tau) * 0.2f;
			s[i] = tone * env;
		}

		return s;
	}

	private static float[] VictoryFanfare()
	{
		var notes = new[] { 523.25f, 659.25f, 783.99f, 1046.5f };
		var n = (int)(SampleRate * 0.55f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var idx = Mathf.Clamp(Mathf.FloorToInt(t / 0.12f), 0, notes.Length - 1);
			var local = t - idx * 0.12f;
			var env = Mathf.Exp(-local * 8f);
			s[i] = Mathf.Sin(t * notes[idx] * Mathf.Tau) * env * 0.38f;
		}

		return s;
	}

	private static float[] DefeatSting()
	{
		var n = (int)(SampleRate * 0.5f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var freq = Mathf.Lerp(240f, 90f, t / 0.5f);
			var env = Mathf.Exp(-t * 3.5f);
			s[i] = (Mathf.Sin(t * freq * Mathf.Tau) * 0.4f + Mathf.Sin(t * freq * 0.5f * Mathf.Tau) * 0.25f) * env;
		}

		return s;
	}

	private static float[] CapturePulse()
	{
		var n = (int)(SampleRate * 0.1f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var env = Mathf.Exp(-t * 18f);
			s[i] = Mathf.Sin(t * 440f * Mathf.Tau) * env * 0.3f
			       + Mathf.Sin(t * 660f * Mathf.Tau) * env * 0.15f;
		}

		return s;
	}

	private static float[] ScrapPickup()
	{
		var n = (int)(SampleRate * 0.12f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var env = Mathf.Exp(-t * 22f);
			s[i] = Mathf.Sin(t * 1200f * Mathf.Tau) * env * 0.28f
			       + Mathf.Sin(t * 1800f * Mathf.Tau) * env * 0.18f;
		}

		return s;
	}

	private static float[] AlarmBuzz()
	{
		var n = (int)(SampleRate * 0.22f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var mod = Mathf.Sin(t * 8f * Mathf.Tau) * 0.5f + 0.5f;
			var env = Mathf.Exp(-t * 5f);
			s[i] = Mathf.Sin(t * 480f * Mathf.Tau) * mod * env * 0.35f;
		}

		return s;
	}

	private static float[] DiskPickup()
	{
		var n = (int)(SampleRate * 0.2f);
		var s = new float[n];
		for (var i = 0; i < n; i++)
		{
			var t = i / (float)SampleRate;
			var freq = Mathf.Lerp(600f, 1200f, t / 0.2f);
			var env = Mathf.Exp(-t * 10f);
			s[i] = Mathf.Sin(t * freq * Mathf.Tau) * env * 0.35f;
		}

		return s;
	}

	private static float[] SoftClip(float[] s, float limit)
	{
		for (var i = 0; i < s.Length; i++)
			s[i] = Mathf.Clamp(s[i], -limit, limit);
		return s;
	}

	private static AudioStreamWav ToStream(float[] samples)
	{
		var data = new byte[samples.Length * 2];
		for (var i = 0; i < samples.Length; i++)
		{
			var v = Mathf.Clamp(samples[i], -1f, 1f);
			var sample = (short)Mathf.RoundToInt(v * short.MaxValue * 0.85f);
			data[i * 2] = (byte)(sample & 0xff);
			data[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
		}

		return new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = SampleRate,
			Stereo = false,
			Data = data
		};
	}
}

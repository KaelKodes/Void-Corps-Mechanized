using System;
using System.Collections.Generic;
using Godot;

namespace Mechanize;

/// <summary>
/// Shared tileable PBR surfaces (ambientCG CC0) for arena floor / procedural cover.
/// Uses world-space triplanar so box geometry reads as textured without UV unwraps.
/// </summary>
public static class SurfaceLibrary
{
	public enum Kind
	{
		Asphalt,
		Concrete,
		ConcreteRough,
		Steel,
		SteelDark,
		PaintedMetal,
		Rust,
		Ground
	}

	private static readonly Dictionary<Kind, string> Folders = new()
	{
		[Kind.Asphalt] = "res://art/surfaces/asphalt/",
		[Kind.Concrete] = "res://art/surfaces/concrete/",
		[Kind.ConcreteRough] = "res://art/surfaces/concrete_rough/",
		[Kind.Steel] = "res://art/surfaces/steel/",
		[Kind.SteelDark] = "res://art/surfaces/steel_dark/",
		[Kind.PaintedMetal] = "res://art/surfaces/painted_metal/",
		[Kind.Rust] = "res://art/surfaces/rust/",
		[Kind.Ground] = "res://art/surfaces/ground/"
	};

	/// <summary>World meters covered by one texture tile (Godot triplanar uv1_scale).</summary>
	private static readonly Dictionary<Kind, float> UvScale = new()
	{
		[Kind.Asphalt] = 8f,
		[Kind.Concrete] = 3.5f,
		[Kind.ConcreteRough] = 4f,
		[Kind.Steel] = 2.5f,
		[Kind.SteelDark] = 2.8f,
		[Kind.PaintedMetal] = 2.8f,
		[Kind.Rust] = 2.2f,
		[Kind.Ground] = 6f
	};

	private static readonly Dictionary<Kind, StandardMaterial3D> Cache = new();

	/// <summary>
	/// Build (or reuse) a triplanar PBR material. Tint multiplies albedo for claim ambience.
	/// Falls back to flat color if textures are missing.
	/// </summary>
	public static StandardMaterial3D Get(Kind kind, Color? tint = null, float metallicBias = 0f, float roughnessBias = 0f)
	{
		if (!Cache.TryGetValue(kind, out var proto))
		{
			proto = BuildPrototype(kind);
			Cache[kind] = proto;
		}

		var mat = (StandardMaterial3D)proto.Duplicate();
		if (tint.HasValue)
			mat.AlbedoColor = tint.Value;
		if (metallicBias != 0f)
			mat.Metallic = Mathf.Clamp(mat.Metallic + metallicBias, 0f, 1f);
		if (roughnessBias != 0f)
			mat.Roughness = Mathf.Clamp(mat.Roughness + roughnessBias, 0f, 1f);
		return mat;
	}

	/// <summary>Pick a floor surface that matches claim fantasy.</summary>
	public static Kind FloorForClaim(string claimCode)
	{
		if (claimCode.Contains("7-ORBITAL", StringComparison.OrdinalIgnoreCase))
			return Kind.Ground;
		if (claimCode.Contains("BLACK-WHARF", StringComparison.OrdinalIgnoreCase)
		    || claimCode.Contains("SPIRE-NULL", StringComparison.OrdinalIgnoreCase))
			return Kind.Asphalt;
		if (claimCode.Contains("SLAG", StringComparison.OrdinalIgnoreCase)
		    || claimCode.Contains("GRID-ASH", StringComparison.OrdinalIgnoreCase))
			return Kind.ConcreteRough;
		return Kind.Asphalt;
	}

	/// <summary>Wall / pad-edge surface for a claim.</summary>
	public static Kind WallForClaim(string claimCode)
	{
		if (claimCode.Contains("BLACK-WHARF", StringComparison.OrdinalIgnoreCase)
		    || claimCode.Contains("SPIRE-NULL", StringComparison.OrdinalIgnoreCase))
			return Kind.Concrete;
		if (claimCode.Contains("7-ORBITAL", StringComparison.OrdinalIgnoreCase))
			return Kind.ConcreteRough;
		return Kind.Concrete;
	}

	/// <summary>Flat emissive / accent — not textured.</summary>
	public static StandardMaterial3D Flat(
		Color albedo,
		float metallic,
		float roughness,
		Color? emission = null,
		float emissionEnergy = 0f)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = albedo,
			Metallic = metallic,
			Roughness = roughness
		};
		if (emission.HasValue && emissionEnergy > 0f)
		{
			mat.EmissionEnabled = true;
			mat.Emission = emission.Value;
			mat.EmissionEnergyMultiplier = emissionEnergy;
		}
		return mat;
	}

	private static StandardMaterial3D BuildPrototype(Kind kind)
	{
		var folder = Folders[kind];
		var color = FindTex(folder, "Color", "Diffuse", "BaseColor");
		var normal = FindTex(folder, "NormalGL", "Normal", "NormalDX");
		var rough = FindTex(folder, "Roughness");
		var metal = FindTex(folder, "Metalness", "Metallic");

		var metallicDefault = kind switch
		{
			Kind.Steel or Kind.SteelDark or Kind.PaintedMetal or Kind.Rust => 0.7f,
			_ => 0.05f
		};
		var roughnessDefault = kind switch
		{
			Kind.Asphalt or Kind.Concrete or Kind.ConcreteRough or Kind.Ground => 0.9f,
			Kind.SteelDark => 0.5f,
			_ => 0.55f
		};

		var mat = new StandardMaterial3D
		{
			AlbedoColor = Colors.White,
			Metallic = metallicDefault,
			Roughness = roughnessDefault,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
			Uv1Triplanar = true,
			Uv1WorldTriplanar = true,
			Uv1TriplanarSharpness = 4f,
			Uv1Scale = new Vector3(UvScale[kind], UvScale[kind], UvScale[kind])
		};

		if (color != null)
			mat.AlbedoTexture = color;

		if (normal != null)
		{
			mat.NormalEnabled = true;
			mat.NormalTexture = normal;
			mat.NormalScale = 1f;
		}

		if (rough != null)
		{
			mat.RoughnessTexture = rough;
			mat.RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Red;
		}

		if (metal != null)
		{
			mat.MetallicTexture = metal;
			mat.MetallicTextureChannel = BaseMaterial3D.TextureChannel.Red;
		}

		return mat;
	}

	private static Texture2D? FindTex(string folder, params string[] tokens)
	{
		using var dir = DirAccess.Open(folder);
		if (dir == null)
			return null;

		dir.ListDirBegin();
		string? best = null;
		while (true)
		{
			var name = dir.GetNext();
			if (string.IsNullOrEmpty(name))
				break;
			if (dir.CurrentIsDir())
				continue;
			if (!name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
			    && !name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
			    && !name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
				continue;

			foreach (var token in tokens)
			{
				if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
				{
					best = folder + name;
					break;
				}
			}
			if (best != null)
				break;
		}
		dir.ListDirEnd();

		if (best == null || !ResourceLoader.Exists(best))
			return null;
		return GD.Load<Texture2D>(best);
	}
}

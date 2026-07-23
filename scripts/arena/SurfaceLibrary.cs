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
	public static StandardMaterial3D Get(
		Kind kind,
		Color? tint = null,
		float metallicBias = 0f,
		float roughnessBias = 0f,
		float uvScaleMul = 1f)
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
		if (!Mathf.IsEqualApprox(uvScaleMul, 1f))
		{
			var s = mat.Uv1Scale.X * uvScaleMul;
			mat.Uv1Scale = new Vector3(s, s, s);
		}
		return mat;
	}

	/// <summary>
	/// Mech kit / hollow hull plate. Finer tiles than arena cover; softened normals for FP.
	/// Object-space triplanar (not world) so paint stays glued when the mech moves.
	/// </summary>
	public static StandardMaterial3D GetMech(Kind kind, Color tint, float metersPerTile = 1.1f, float normalScale = 0.55f)
	{
		var mat = Get(kind, tint);
		mat.Uv1WorldTriplanar = false;
		mat.Uv1Scale = new Vector3(metersPerTile, metersPerTile, metersPerTile);
		if (mat.NormalEnabled)
			mat.NormalScale = normalScale;
		return mat;
	}

	/// <summary>
	/// Issued MAP plate: steel albedo (clean paint color) + painted-metal normals/roughness
	/// (scratches / dents) without the rust mottling in the paint pack's color map.
	/// </summary>
	public static StandardMaterial3D GetMechPlate(Color tint, float metersPerTile = 1.1f, float normalScale = 0.7f)
	{
		var mat = Get(Kind.PaintedMetal, tint);
		var clean = Get(Kind.Steel, Colors.White);
		if (clean.AlbedoTexture != null)
			mat.AlbedoTexture = clean.AlbedoTexture;
		mat.AlbedoColor = tint;
		mat.Uv1WorldTriplanar = false;
		mat.Uv1Scale = new Vector3(metersPerTile, metersPerTile, metersPerTile);
		if (mat.NormalEnabled)
			mat.NormalScale = normalScale;
		// Keep it issued-kit, not chrome: wear lives in the normal/rough maps.
		mat.Metallic = Mathf.Clamp(mat.Metallic - 0.15f, 0.2f, 0.85f);
		mat.Roughness = Mathf.Clamp(mat.Roughness + 0.08f, 0.35f, 0.95f);
		return mat;
	}

	/// <summary>
	/// Tall rim buildings — stretched tiles + matte concrete so FP doesn't read sandpaper chrome.
	/// </summary>
	public static StandardMaterial3D GetBuildingFacade(Color tint, int seed = 0)
	{
		unchecked
		{
			var h = (uint)(seed * 2654435761u);
			var tintN = (h % 1000u) / 1000f;
			var tinted = tint.Lightened(tintN * 0.06f - 0.03f);
			var mat = Get(Kind.Concrete, tinted);
			// ~18–24 m per tile on a ~28 m tower = a few soft mottles, not glitter.
			var meters = 18f + ((h / 1000u) % 1000u) / 1000f * 6f;
			mat.Uv1Scale = new Vector3(meters, meters, meters);
			mat.NormalEnabled = false;
			mat.NormalTexture = null;
			mat.RoughnessTexture = null;
			mat.MetallicTexture = null;
			mat.Metallic = 0.04f;
			mat.Roughness = 0.9f;
			return mat;
		}
	}

	/// <summary>
	/// Deterministic tint + UV-scale jitter so identical cover kinds don't look stamped.
	/// Seed typically comes from cover index / world position hash.
	/// </summary>
	public static StandardMaterial3D GetVaried(
		Kind kind,
		Color tint,
		int seed,
		float metallicBias = 0f,
		float roughnessBias = 0f,
		float uvScaleMul = 1f)
	{
		unchecked
		{
			var h = (uint)(seed * 2654435761u);
			var tintN = (h % 1000u) / 1000f;
			var uvN = ((h / 1000u) % 1000u) / 1000f;
			var tinted = tint.Lightened(tintN * 0.1f - 0.05f);
			var uvMul = (0.82f + uvN * 0.36f) * uvScaleMul; // ~0.82–1.18 * optional stretch
			return Get(kind, tinted, metallicBias, roughnessBias, uvMul);
		}
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
	public static Kind WallForClaim(string claimCode) => Kind.Concrete;

	/// <summary>
	/// Arena bulkhead material. Same concrete albedo as cover, but strips normal/roughness maps
	/// and uses large tiles — big vertical planes make pack micro-grain look like sandpaper.
	/// </summary>
	public static StandardMaterial3D GetPadWall(Color tint)
	{
		var mat = Get(Kind.Concrete, tint);
		return CalmLargePlane(mat, metersPerTile: 22f);
	}

	/// <summary>
	/// Claim floor — claim albedo pick, but calm maps so FP looking-down isn't sandpaper.
	/// </summary>
	public static StandardMaterial3D GetPadFloor(string claimCode, Color tint)
	{
		var mat = Get(FloorForClaim(claimCode), tint);
		return CalmLargePlane(mat, metersPerTile: 28f);
	}

	private static StandardMaterial3D CalmLargePlane(StandardMaterial3D mat, float metersPerTile)
	{
		mat.NormalEnabled = false;
		mat.NormalTexture = null;
		mat.RoughnessTexture = null;
		mat.MetallicTexture = null;
		mat.Roughness = 0.92f;
		mat.Uv1Scale = new Vector3(metersPerTile, metersPerTile, metersPerTile);
		return mat;
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
			mat.NormalScale = kind is Kind.Asphalt or Kind.Ground or Kind.ConcreteRough ? 0.45f : 0.75f;
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

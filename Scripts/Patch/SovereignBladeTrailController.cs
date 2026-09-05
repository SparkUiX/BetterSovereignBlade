using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace BetterSovereignBlade.Scripts.Patch;

internal readonly record struct SovereignBladeTrailPalette(
	Color Primary,
	Color Secondary,
	Color Spark);

internal sealed partial class SovereignBladeTrailController
{
	private sealed class TrailSample
	{
		public TrailSample(Vector2 position)
		{
			Position = position;
		}

		public Vector2 Position;
		public float Age;
	}

	private readonly List<TrailSample> _samples = new();

	private TrailRoot? _root;
	private Line2D? _glowRibbon;
	private Line2D? _coreRibbon;
	private GpuParticles2D? _moteParticles;
	private GpuParticles2D? _sparkParticles;
	private Vector2 _lastRecordedPosition;
	private bool _hasRecordedPosition;
	private bool _wasDragging;
	private bool _loggedRenderable;

	private static TrailTuning Tuning => TrailTuning.Default;

	/// <summary>
	/// Ensures the visual nodes exist and updates the trail every frame.
	/// </summary>
	public void Update(NSovereignBladeVfx owner, Node2D spine, bool isDragging, Vector2 velocity, float delta)
	{
		if (!GodotObject.IsInstanceValid(owner) || !GodotObject.IsInstanceValid(spine))
		{
			return;
		}

		EnsureNodes(owner);
		Vector2 emissionPosition = spine.ToGlobal(Tuning.EmissionOffset);
		Vector2 localEmissionPosition = owner.ToLocal(emissionPosition);
		LogDragStateChange(owner, spine, isDragging, emissionPosition, localEmissionPosition, velocity);
		AgeSamples(delta);
		UpdateTrailSamples(localEmissionPosition, isDragging);
		RefreshPalette();
		UpdateParticleEmission(localEmissionPosition, velocity, isDragging, spine.GlobalRotation);
		RenderTrail();
	}

	/// <summary>
	/// Stops particle emission immediately and lets the ribbon finish fading naturally.
	/// </summary>
	public void StopEmission()
	{
		if (_wasDragging)
		{
			Log.Info($"[BetterSovereignBlade] Trail stop samples={_samples.Count}");
		}

		if (GodotObject.IsInstanceValid(_moteParticles))
		{
			_moteParticles!.Emitting = false;
		}

		if (GodotObject.IsInstanceValid(_sparkParticles))
		{
			_sparkParticles!.Emitting = false;
		}
	}

	/// <summary>
	/// Releases all created nodes when the sword VFX is destroyed.
	/// </summary>
	public void Dispose()
	{
		_samples.Clear();
		QueueFreeIfValid(_root);
		_root = null;
		_glowRibbon = null;
		_coreRibbon = null;
		_moteParticles = null;
		_sparkParticles = null;
		_hasRecordedPosition = false;
	}

	private void EnsureNodes(NSovereignBladeVfx owner)
	{
		if (GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		_root = new TrailRoot
		{
			Name = "BetterSovereignBladeDragTrail",
			Position = Vector2.Zero,
			ShowBehindParent = true,
			ZIndex = Tuning.RootZIndex,
			ProcessMode = Node.ProcessModeEnum.Always
		};

		_glowRibbon = CreateRibbon(width: Tuning.GlowWidth * Tuning.EffectScale, additive: true, zIndex: Tuning.RibbonGlowZIndex);
		_coreRibbon = CreateRibbon(width: Tuning.CoreWidth * Tuning.EffectScale, additive: false, zIndex: Tuning.RibbonCoreZIndex);
		_moteParticles = CreateParticles("Motes", Tuning.MoteAmount, Tuning.MoteLifetime, Tuning.MoteScale * Tuning.EffectScale, soft: true, zIndex: Tuning.ParticleZIndex);
		_sparkParticles = CreateParticles("Sparks", Tuning.SparkAmount, Tuning.SparkLifetime, Tuning.SparkScale * Tuning.EffectScale, soft: false, zIndex: Tuning.ParticleZIndex);

		_root.AddChild(_glowRibbon);
		_root.AddChild(_coreRibbon);
		_root.AddChild(_moteParticles);
		_root.AddChild(_sparkParticles);
		owner.AddChild(_root);
		owner.MoveChild(_root, 0);

		Log.Info($"[BetterSovereignBlade] Trail nodes created rootBehind={_root.ShowBehindParent} rootZ={_root.ZIndex} glowZ={_glowRibbon.ZIndex} coreZ={_coreRibbon.ZIndex} particleZ={_moteParticles.ZIndex}");
	}

	private void AgeSamples(float delta)
	{
		for (int i = _samples.Count - 1; i >= 0; i--)
		{
			_samples[i].Age += delta;
			if (_samples[i].Age > Tuning.FadeDuration)
			{
				_samples.RemoveAt(i);
			}
		}
	}

	private void UpdateTrailSamples(Vector2 currentPosition, bool isDragging)
	{
		if (!isDragging)
		{
			_hasRecordedPosition = false;
			_loggedRenderable = false;
			return;
		}

		if (!_hasRecordedPosition || currentPosition.DistanceTo(_lastRecordedPosition) >= Tuning.SampleSpacing * Tuning.EffectScale)
		{
			_samples.Insert(0, new TrailSample(currentPosition));
			_lastRecordedPosition = currentPosition;
			_hasRecordedPosition = true;
		}

		while (_samples.Count > Tuning.MaxSamples)
		{
			_samples.RemoveAt(_samples.Count - 1);
		}
	}

	private void RefreshPalette()
	{
		if (!GodotObject.IsInstanceValid(_glowRibbon) || !GodotObject.IsInstanceValid(_coreRibbon))
		{
			return;
		}

		SovereignBladeTrailPalette palette = PaletteSettingsMod.GetRuntimeTrailPalette();
		Color glowStart = palette.Primary.Lerp(Colors.White, 0.45f) with { A = 0.42f };
		Color glowMid = palette.Secondary.Lerp(palette.Primary, 0.35f) with { A = 0.18f };
		Color glowEnd = glowMid with { A = 0.0f };

		Color coreStart = palette.Primary.Lerp(Colors.White, 0.65f) with { A = 0.95f };
		Color coreMid = palette.Secondary.Lerp(palette.Primary, 0.20f) with { A = 0.46f };
		Color coreEnd = coreMid with { A = 0.0f };

		_glowRibbon!.Gradient = CreateGradient(glowStart, glowMid, glowEnd);
		_coreRibbon!.Gradient = CreateGradient(coreStart, coreMid, coreEnd);

		UpdateParticlePalette(_moteParticles, palette.Primary.Lerp(Colors.White, 0.35f) with { A = 0.65f });
		UpdateParticlePalette(_sparkParticles, palette.Spark.Lerp(Colors.White, 0.20f) with { A = 0.9f });
	}

	private void UpdateParticleEmission(Vector2 currentPosition, Vector2 velocity, bool isDragging, float rotation)
	{
		UpdateSingleEmitter(_moteParticles, currentPosition, velocity, isDragging, rotation, Tuning.MoteEmissionRate, 0.35f, 18f);
		UpdateSingleEmitter(_sparkParticles, currentPosition, velocity, isDragging, rotation, Tuning.SparkEmissionRate, 0.6f, 10f);
	}

	private void UpdateSingleEmitter(
		GpuParticles2D? emitter,
		Vector2 currentPosition,
		Vector2 velocity,
		bool isDragging,
		float rotation,
		float baseAmountRatio,
		float speedFactor,
		float spread)
	{
		if (!GodotObject.IsInstanceValid(emitter))
		{
			return;
		}

		emitter!.Position = currentPosition;
		Vector2 rotationForward = Vector2.Right.Rotated(rotation);
		Vector2 safeVelocity = velocity.LengthSquared() > 0.0001f ? velocity : rotationForward;
		Vector2 backwards = -safeVelocity.Normalized();

		if (emitter.ProcessMaterial is ParticleProcessMaterial material)
		{
			material.Direction = new Vector3(backwards.X, backwards.Y, 0f);
			material.Spread = spread;
			material.InitialVelocityMin = Math.Max(12f, velocity.Length() * speedFactor * 0.4f);
			material.InitialVelocityMax = Math.Max(material.InitialVelocityMin + 8f, velocity.Length() * speedFactor);
		}

		emitter.AmountRatio = isDragging ? Mathf.Clamp(baseAmountRatio + velocity.Length() / 900f, 0.15f, 1f) : 0f;
		emitter.Emitting = isDragging;
	}

	private void RenderTrail()
	{
		Vector2[] points = new Vector2[_samples.Count];
		for (int i = 0; i < _samples.Count; i++)
		{
			points[i] = _samples[i].Position;
		}

		bool visible = points.Length >= 2;
		if (visible && !_loggedRenderable)
		{
			_loggedRenderable = true;
			Log.Info($"[BetterSovereignBlade] Trail renderable points={points.Length} head={points[0]} tail={points[^1]}");
		}

		SetRibbonPoints(_glowRibbon, points, visible);
		SetRibbonPoints(_coreRibbon, points, visible);
	}

	private void LogDragStateChange(
		NSovereignBladeVfx owner,
		Node2D spine,
		bool isDragging,
		Vector2 emissionPosition,
		Vector2 localEmissionPosition,
		Vector2 velocity)
	{
		if (isDragging == _wasDragging)
		{
			return;
		}

		_wasDragging = isDragging;

		if (!isDragging)
		{
			return;
		}

		Log.Info(
			$"[BetterSovereignBlade] Trail start ownerPos={owner.GlobalPosition} spinePos={spine.GlobalPosition} spineRot={spine.GlobalRotation:F3} " +
			$"emissionGlobal={emissionPosition} emissionLocal={localEmissionPosition} velocity={velocity}");
	}

	private static void SetRibbonPoints(Line2D? ribbon, Vector2[] points, bool visible)
	{
		if (!GodotObject.IsInstanceValid(ribbon))
		{
			return;
		}

		ribbon!.Visible = visible;
		ribbon.Points = points;
	}

	private static Line2D CreateRibbon(float width, bool additive, int zIndex)
	{
		Line2D ribbon = new()
		{
			Position = Vector2.Zero,
			ShowBehindParent = true,
			Width = width,
			Antialiased = true,
			JointMode = Line2D.LineJointMode.Round,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			WidthCurve = CreateWidthCurve(),
			TextureMode = Line2D.LineTextureMode.None,
			Visible = false,
			ZIndex = zIndex,
			Material = new CanvasItemMaterial
			{
				BlendMode = additive
					? CanvasItemMaterial.BlendModeEnum.Add
					: CanvasItemMaterial.BlendModeEnum.Mix
			}
		};

		return ribbon;
	}

	private static Curve CreateWidthCurve()
	{
		Curve curve = new();
		curve.AddPoint(new Vector2(0f, 0.95f));
		curve.AddPoint(new Vector2(0.25f, 1.0f));
		curve.AddPoint(new Vector2(0.8f, 0.45f));
		curve.AddPoint(new Vector2(1f, 0.05f));
		return curve;
	}

	private static Gradient CreateGradient(Color start, Color mid, Color end)
	{
		Gradient gradient = new();
		gradient.AddPoint(0f, start);
		gradient.AddPoint(0.35f, mid);
		gradient.AddPoint(1f, end);
		return gradient;
	}

	private static GpuParticles2D CreateParticles(string name, int amount, float lifetime, float scale, bool soft, int zIndex)
	{
		ParticleProcessMaterial processMaterial = new()
		{
			Direction = Vector3.Left,
			Spread = soft ? 28f : 16f,
			InitialVelocityMin = soft ? 20f : 35f,
			InitialVelocityMax = soft ? 45f : 75f,
			DampingMin = 8f,
			DampingMax = 16f,
			ScaleMin = scale * 0.7f,
			ScaleMax = scale,
			Gravity = Vector3.Zero,
			Color = Colors.White
		};

		GpuParticles2D particles = new()
		{
			Name = name,
			Position = Vector2.Zero,
			ShowBehindParent = true,
			ZIndex = zIndex,
			Amount = amount,
			AmountRatio = 0f,
			Lifetime = lifetime,
			Emitting = false,
			OneShot = false,
			Explosiveness = 0f,
			LocalCoords = false,
			ProcessMaterial = processMaterial,
			Texture = CreateParticleTexture(soft),
			Material = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add
			}
		};

		return particles;
	}

	private static Texture2D CreateParticleTexture(bool soft)
	{
		int size = soft ? 32 : 18;
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		Vector2 center = new(size / 2f, size / 2f);
		float maxDistance = center.X;

		for (int x = 0; x < size; x++)
		{
			for (int y = 0; y < size; y++)
			{
				float distance = new Vector2(x, y).DistanceTo(center) / maxDistance;
				float alpha = soft
					? Mathf.Clamp(1f - distance, 0f, 1f)
					: Mathf.Clamp(1f - distance * 1.6f, 0f, 1f);

				if (!soft && distance < 0.28f)
				{
					alpha = 1f;
				}

				image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private static void UpdateParticlePalette(GpuParticles2D? emitter, Color color)
	{
		if (!GodotObject.IsInstanceValid(emitter) || emitter!.ProcessMaterial is not ParticleProcessMaterial material)
		{
			return;
		}

		material.Color = color;
	}

	private static void QueueFreeIfValid(Node? node)
	{
		if (GodotObject.IsInstanceValid(node))
		{
			node!.QueueFree();
		}
	}

	private readonly record struct TrailTuning(
		float EffectScale,
		Vector2 EmissionOffset,
		int RootZIndex,
		int RibbonGlowZIndex,
		int RibbonCoreZIndex,
		int ParticleZIndex,
		float SampleSpacing,
		int MaxSamples,
		float FadeDuration,
		float GlowWidth,
		float CoreWidth,
		int MoteAmount,
		int SparkAmount,
		float MoteLifetime,
		float SparkLifetime,
		float MoteScale,
		float SparkScale,
		float MoteEmissionRate,
		float SparkEmissionRate)
	{
		public static TrailTuning Default => new(
			EffectScale: 1.7f,
			EmissionOffset: new Vector2(-150f, 0f),
			RootZIndex: 0,
			RibbonGlowZIndex: 0,
			RibbonCoreZIndex: 0,
			ParticleZIndex: 0,
			SampleSpacing: 10f,
			MaxSamples: 18,
			FadeDuration: 0.26f,
			GlowWidth: 26f,
			CoreWidth: 13f,
			MoteAmount: 20,
			SparkAmount: 14,
			MoteLifetime: 0.42f,
			SparkLifetime: 0.28f,
			MoteScale: 0.72f,
			SparkScale: 0.38f,
			MoteEmissionRate: 0.35f,
			SparkEmissionRate: 0.18f);
	}

	private sealed partial class TrailRoot : Node2D
	{
	}
}
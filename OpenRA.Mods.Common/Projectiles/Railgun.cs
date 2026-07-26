#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Collections.Immutable;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Projectiles
{
	[Desc("Laser effect with helix coiling around.")]
	public class RailgunInfo : IProjectileInfo
	{
		[Desc("Damage all units hit by the beam instead of just the target?")]
		public readonly bool DamageActorsInLine = false;

		[Desc("The maximum/constant/incremental inaccuracy used in conjunction with the InaccuracyType property.")]
		public readonly WDist Inaccuracy = WDist.Zero;

		[Desc("Controls the way inaccuracy is calculated. Possible values are " +
			"'Maximum' - scale from 0 to max with range, " +
			"'PerCellIncrement' - scale from 0 with range, " +
			"'Absolute' - use set value regardless of range.")]
		public readonly InaccuracyType InaccuracyType = InaccuracyType.Maximum;

		[Desc("Can this projectile be blocked when hitting actors with an IBlocksProjectiles trait.")]
		public readonly bool Blockable = false;

		[Desc("Duration of the beam and helix")]
		public readonly int Duration = 15;

		[Desc("Equivalent to sequence ZOffset. Controls Z sorting.")]
		public readonly int ZOffset = 0;

		[Desc("The width of the main trajectory. (\"beam\").")]
		public readonly WDist BeamWidth = new(86);

		[Desc("The shape of the beam.  Accepts values Cylindrical or Flat.")]
		public readonly BeamRenderableShape BeamShape = BeamRenderableShape.Cylindrical;

		[Desc("Beam color in (A),R,G,B.")]
		public readonly Color BeamColor = Color.FromArgb(128, 255, 255, 255);

		[Desc("When true, this will override BeamColor parameter and draw the laser with player color."
			+ " (Still uses BeamColor's alpha information)")]
		public readonly bool BeamPlayerColor = false;

		[Desc("Beam alpha gets + this value per tick during drawing; hence negative value makes it fade over time.")]
		public readonly int BeamAlphaDeltaPerTick = -8;

		[Desc("Thickness of the helix")]
		public readonly WDist HelixThickness = new(32);

		[Desc("The radius of the spiral effect. (WDist)")]
		public readonly WDist HelixRadius = new(64);

		[Desc("Height of one complete helix turn, measured parallel to the axis of the helix (WDist)")]
		public readonly WDist HelixPitch = new(512);

		[Desc("Helix radius gets + this value per tick during drawing")]
		public readonly int HelixRadiusDeltaPerTick = 8;

		[Desc("Helix alpha gets + this value per tick during drawing; hence negative value makes it fade over time.")]
		public readonly int HelixAlphaDeltaPerTick = -8;

		[Desc("Helix spins by this much over time each tick.")]
		public readonly WAngle HelixAngleDeltaPerTick = new(16);

		[Desc("Draw each cycle of helix with this many quantization steps")]
		public readonly int QuantizationCount = 16;

		[Desc("Helix color in (A),R,G,B.")]
		public readonly Color HelixColor = Color.FromArgb(128, 255, 255, 255);

		[Desc("Draw helix in PlayerColor? Overrides RGB part of the HelixColor. (Still uses HelixColor's alpha information)")]
		public readonly bool HelixPlayerColor = false;

		[Desc("Impact animation.")]
		public readonly string HitAnim = null;

		[Desc("Sequence of impact animation to use.")]
		[SequenceReference(nameof(HitAnim), allowNullImage: true)]
		public readonly string HitAnimSequence = "idle";

		[PaletteReference]
		public readonly string HitAnimPalette = "effect";

		[Desc("Maximum screenspace distortion in pixels applied along the beam. 0 disables the refraction effect.")]
		public readonly float Distortion = 0f;

		[Desc("Width of the distortion effect. 0 uses BeamWidth.")]
		public readonly WDist DistortionWidth = WDist.Zero;

		[Desc("Spatial frequency of the distortion wave along the beam.")]
		public readonly float DistortionWaveScale = 28f;

		[Desc("Animation speed of the distortion wave.")]
		public readonly float DistortionWaveSpeed = 0.18f;

		[Desc("Fraction of the distortion width used to fade out the beam edges.")]
		public readonly float DistortionEdgeSoftness = 0.45f;

		[Desc("Break the beam apart into drifting, fading particles once it finishes instead of just cutting off.")]
		public readonly bool TrailParticles = false;

		[Desc("Number of particles the trail dissolves into.")]
		public readonly int TrailParticleCount = 24;

		[Desc("Ticks each trail particle drifts and fades for after the beam ends.")]
		public readonly int TrailParticleLifetime = 20;

		[Desc("Outward drift speed range of trail particles (WDist per tick), low to high.")]
		public readonly ImmutableArray<WDist> TrailParticleSpeed = [new(20), new(80)];

		[Desc("Drawn length of each trail particle.")]
		public readonly WDist TrailParticleLength = new(64);

		[Desc("Drawn width of each trail particle.")]
		public readonly WDist TrailParticleWidth = new(24);

		[Desc("Trail particle color at the start of its life.")]
		public readonly Color TrailParticleStartColor = Color.FromArgb(200, 255, 255, 255);

		[Desc("Trail particle color at the end of its life (usually alpha 0 to fade out).")]
		public readonly Color TrailParticleEndColor = Color.FromArgb(0, 255, 255, 255);

		[Desc("Per-particle bloom-source multiplier (only effective while BloomGlowEffects is active).",
			"Default 1 = same strength as the global bloom; higher values make the particles push harder",
			"into the glow buffer for a more visible glow.")]
		public readonly float TrailParticleBloomIntensity = 3f;

		public IProjectile Create(ProjectileArgs args)
		{
			var bc = BeamPlayerColor ? Color.FromArgb(BeamColor.A, args.SourceActor.OwnerColor()) : BeamColor;
			var hc = HelixPlayerColor ? Color.FromArgb(HelixColor.A, args.SourceActor.OwnerColor()) : HelixColor;
			return new Railgun(args, this, bc, hc);
		}
	}

	public class Railgun : IProjectile, ISync
	{
		struct TrailParticle
		{
			public WPos Pos;
			public WVec Velocity;
		}

		readonly ProjectileArgs args;
		readonly RailgunInfo info;
		readonly Animation hitanim;
		readonly AreaBeamDistortionRenderer distortionRenderer;
		public readonly Color BeamColor;
		public readonly Color HelixColor;

		int ticks;
		bool animationComplete;

		TrailParticle[] trailParticles;
		bool trailSpawned;
		int trailTicks;

		[VerifySync]
		WPos target;

		// Computing these in Railgun instead of RailgunRenderable saves Info.Duration ticks of computation.
		// Fortunately, railguns don't track the target.
		public int CycleCount { get; private set; }
		public WVec SourceToTarget { get; private set; }
		public WVec ForwardStep { get; private set; }
		public WVec LeftVector { get; private set; }
		public WVec UpVector { get; private set; }
		public WAngle AngleStep { get; private set; }

		public Railgun(ProjectileArgs args, RailgunInfo info, Color beamColor, Color helixColor)
		{
			this.args = args;
			this.info = info;
			target = args.PassiveTarget;

			BeamColor = beamColor;
			HelixColor = helixColor;

			if (info.Inaccuracy.Length > 0)
			{
				var maxInaccuracyOffset = Util.GetProjectileInaccuracy(info.Inaccuracy.Length, info.InaccuracyType, args);
				target += WVec.FromPDF(args.SourceActor.World.SharedRandom, 2) * maxInaccuracyOffset / 1024;
			}

			if (!string.IsNullOrEmpty(info.HitAnim))
				hitanim = new Animation(args.SourceActor.World, info.HitAnim);

			if (info.Distortion > 0f)
				distortionRenderer = args.SourceActor.World.WorldActor.TraitOrDefault<AreaBeamDistortionRenderer>();

			CalculateVectors();
		}

		void CalculateVectors()
		{
			// Check for blocking actors
			if (info.Blockable && BlocksProjectiles.AnyBlockingActorsBetween(args.SourceActor.World, args.SourceActor.Owner, target, args.Source,
					info.BeamWidth, out var blockedPos))
				target = blockedPos;

			// Note: WAngle.Sin(x) = 1024 * Math.Sin(2pi/1024 * x)
			AngleStep = new WAngle(1024 / info.QuantizationCount);

			SourceToTarget = target - args.Source;

			// Forward step, pointing from src to target.
			// QuantizationCont * forwardStep == One cycle of beam in src2target direction.
			ForwardStep = info.HelixPitch.Length * SourceToTarget / (info.QuantizationCount * SourceToTarget.Length);

			// An easy vector to find which is perpendicular vector to forwardStep, with 0 Z component
			LeftVector = new WVec(ForwardStep.Y, -ForwardStep.X, 0);
			if (LeftVector.LengthSquared != 0)
				LeftVector = 1024 * LeftVector / LeftVector.Length;

			// Vector that is pointing upwards from the ground
			UpVector = new WVec(
				-ForwardStep.X * ForwardStep.Z,
				-ForwardStep.Z * ForwardStep.Y,
				ForwardStep.X * ForwardStep.X + ForwardStep.Y * ForwardStep.Y);

			if (UpVector.LengthSquared != 0)
				UpVector = 1024 * UpVector / UpVector.Length;

			//// LeftVector and UpVector are unit vectors of size 1024.

			CycleCount = SourceToTarget.Length / info.HelixPitch.Length;
			if (SourceToTarget.Length % info.HelixPitch.Length != 0)
				CycleCount++; // math.ceil, int version.

			// Using ForwardStep * CycleCount, the helix and the main beam gets "out of sync"
			// if drawn from source to target. Instead, the main beam is drawn from source to end point of helix.
			// Trade-off between computation vs Railgun weapon range.
			// Modders must not have too large range for railgun weapons.
			SourceToTarget = info.QuantizationCount * CycleCount * ForwardStep;
		}

		public void Tick(World world)
		{
			if (ticks == 0)
			{
				if (hitanim != null)
					hitanim.PlayThen(info.HitAnimSequence, () => animationComplete = true);
				else
					animationComplete = true;

				if (!info.DamageActorsInLine)
				{
					var warheadArgs = new WarheadArgs(args)
					{
						ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(args.Source, target), args.Facing),
						ImpactPosition = target,
					};

					args.Weapon.Impact(Target.FromPos(target), warheadArgs);
				}
				else
				{
					var actors = world.FindActorsOnLine(args.Source, target, info.BeamWidth);
					foreach (var a in actors)
					{
						var warheadArgs = new WarheadArgs(args)
						{
							ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(args.Source, target), args.Facing),

							// Calculating an impact position is bogus for line damage.
							// FindActorsOnLine guarantees that the beam touches the target's HitShape,
							// so we just assume a center hit to avoid bogus warhead recalculations.
							ImpactPosition = a.CenterPosition,
						};

						args.Weapon.Impact(Target.FromActor(a), warheadArgs);
					}
				}
			}

			hitanim?.Tick();

			if (info.TrailParticles)
			{
				if (ticks == info.Duration && !trailSpawned)
					SpawnTrailParticles(world);

				if (trailSpawned)
					TickTrailParticles();
			}

			var trailFinished = !info.TrailParticles || trailTicks >= info.TrailParticleLifetime;

			if (ticks++ > info.Duration && animationComplete && trailFinished)
				world.AddFrameEndTask(w => w.Remove(this));
		}

		void SpawnTrailParticles(World world)
		{
			trailSpawned = true;
			trailParticles = new TrailParticle[info.TrailParticleCount];

			for (var i = 0; i < trailParticles.Length; i++)
			{
				var t = trailParticles.Length > 1 ? i * 1024 / (trailParticles.Length - 1) : 0;
				var pos = args.Source + SourceToTarget * t / 1024;

				var angle = new WAngle(world.SharedRandom.Next(1024));
				var direction = (LeftVector * angle.Cos() + UpVector * angle.Sin()) / 1024;
				var speed = Util.RandomDistance(world.SharedRandom, info.TrailParticleSpeed);

				trailParticles[i] = new TrailParticle
				{
					Pos = pos,
					Velocity = direction * speed.Length / 1024,
				};
			}
		}

		void TickTrailParticles()
		{
			for (var i = 0; i < trailParticles.Length; i++)
				trailParticles[i].Pos += trailParticles[i].Velocity;

			trailTicks++;
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (wr.World.FogObscures(target) &&
				wr.World.FogObscures(args.Source))
				yield break;

			if (ticks < info.Duration)
			{
				yield return new RailgunHelixRenderable(args.Source, info.ZOffset, this, info, ticks);

				var beamAlpha = (BeamColor.A + info.BeamAlphaDeltaPerTick * ticks).Clamp(0, 255);
				var beamColor = Color.FromArgb(beamAlpha, BeamColor);
				if (distortionRenderer != null)
				{
					var distortionWidth = info.DistortionWidth.Length > 0 ? info.DistortionWidth : info.BeamWidth;
					yield return new AreaBeamDistortionRenderable(distortionRenderer, args.Source, info.ZOffset, SourceToTarget,
						distortionWidth, info.BeamShape, info.BeamWidth, beamColor, 0f, info.Distortion,
						info.DistortionWaveScale, info.DistortionWaveSpeed, info.DistortionEdgeSoftness);
				}
				else
					yield return new BeamRenderable(args.Source, info.ZOffset, SourceToTarget, info.BeamShape, info.BeamWidth, beamColor);
			}

			if (trailSpawned)
			{
				var fade = info.TrailParticleLifetime > 0 ? trailTicks * 1f / info.TrailParticleLifetime : 1f;
				var color = Exts.ColorLerp(System.Math.Clamp(fade, 0f, 1f), info.TrailParticleStartColor, info.TrailParticleEndColor);
				if (color.A > 0)
				{
					foreach (var particle in trailParticles)
					{
						var dir = particle.Velocity.LengthSquared > 0
							? info.TrailParticleLength.Length * particle.Velocity / particle.Velocity.Length
							: WVec.Zero;
						yield return new BeamRenderable(particle.Pos, info.ZOffset, dir, info.BeamShape, info.TrailParticleWidth, color,
							0f, info.TrailParticleBloomIntensity);
					}
				}
			}

			if (hitanim != null)
				foreach (var r in hitanim.Render(target, wr.Palette(info.HitAnimPalette)))
					yield return r;
		}
	}
}

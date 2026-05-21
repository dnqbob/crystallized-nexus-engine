#region Copyright & License Information
/*
 * Crystallized Nexus Mod
 * Shared state feeding the combined shader's per-sprite world tint used by
 * the day/night cycle. A CN world trait writes these each tick;
 * SpriteRenderer reads them when setting per-frame viewport params. Applied
 * to terrain + tinted sprites and SKIPPED for IgnoreWorldTint geometry
 * (exactly like the cloud-shadow path). Identity (1,1,1) = no-op, so stock
 * behaviour is unchanged unless a trait opts in.
 */
#endregion

namespace OpenRA.Graphics
{
	public static class WorldTintState
	{
		// Multiplicative day/night tint. (1,1,1) = disabled / no-op.
		public static float Red = 1f;
		public static float Green = 1f;
		public static float Blue = 1f;
	}
}

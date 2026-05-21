#region Copyright & License Information
/*
 * Crystallized Nexus Mod
 * Shared state feeding the combined shader's world-tint cloud shadow.
 * A CN world trait writes these each tick; SpriteRenderer reads them when
 * setting per-frame viewport params. Alpha == 0 (default) = fully disabled,
 * so stock behaviour is unchanged unless a trait opts in.
 */
#endregion

namespace OpenRA.Graphics
{
	public static class CloudShadowState
	{
		// Maximum shadow darkness (0 = disabled / no-op in shader).
		public static float Alpha;

		// Spatial frequency of the cloud noise (smaller = larger banks).
		public static float Scale = 0.006f;

		// Drift velocity (world px per "time" unit) and the accumulated time.
		public static float WindX;
		public static float WindY;
		public static float Time;

		// Coverage threshold and edge softness for the cloud noise.
		public static float Coverage = 0.5f;
		public static float Edge = 0.18f;
	}
}

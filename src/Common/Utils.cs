using PowerOfMind.Drafts.Common;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts
{
	public static class Utils
	{
		public static readonly IComparer<AssetLocation> AssetLocationComparer = new AssetLocationCompare();

		public static int Compare(AssetLocation x, AssetLocation y)
		{
			int c = x!.Domain.CompareTo(y!.Domain);
			if(c != 0) return c;

			return x!.Path.CompareTo(y!.Path);
		}

		public static (int x, int y) RotateOffset(int x, int y, int xMax, int yMax, int rotation)
		{
			switch(rotation)
			{
				case 1: return (yMax - y, x);
				case 2: return (xMax - x, yMax - y);
				case 3: return (y, xMax - x);
				default: return (x, y);
			}
		}

		public static (int x, int y) RotateOffset(int x, int y, int rotation)
		{
			switch(rotation)
			{
				case 1: return (-y, x);
				case 2: return (-x, -y);
				case 3: return (y, -x);
				default: return (x, y);
			}
		}

		public static (int x, int y) OffsetByDirection(int x, int y, int direction)
		{
			switch(direction)
			{
				case 1: return (x + 1, y);
				case 2: return (x, y + 1);
				case 3: return (x - 1, y);
				default: return (x, y - 1);
			}
		}

		public static EnumLearnResult TryLearnDraft(this DraftsModSystem draftsSystem, IPlayer player, AssetLocation code)
		{
			if(draftsSystem.GetDescriptor(code) != null)
			{
				if(draftsSystem.Api.Side == EnumAppSide.Client)
				{
					var playerDrafts = draftsSystem.GetPlayerDrafts();
					if(playerDrafts != null && !playerDrafts.Contains(code))
					{
						return EnumLearnResult.Learned;
					}
				}
				else
				{
					var playerDrafts = draftsSystem.GetPlayerDrafts(player.PlayerUID);
					if(playerDrafts != null && !playerDrafts.Contains(code))
					{
						using var change = playerDrafts.BeginChange();
						change.Add(code);
						return EnumLearnResult.Learned;
					}
					return EnumLearnResult.None;
				}
			}
			return EnumLearnResult.Failed;
		}

		private class AssetLocationCompare : IComparer<AssetLocation>
		{
			public int Compare(AssetLocation? x, AssetLocation? y)
			{
				int c = x!.Domain.CompareTo(y!.Domain);
				if(c != 0) return c;

				return x!.Path.CompareTo(y!.Path);
			}
		}
	}
}
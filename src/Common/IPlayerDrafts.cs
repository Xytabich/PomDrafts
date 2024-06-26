using Vintagestory.API.Common;

namespace PowerOfMind.Drafts
{
	public interface IPlayerDrafts : IReadOnlyCollection<AssetLocation>, IDisposable
	{
		/// <summary>
		/// Checks whether the player has a given draft
		/// </summary>
		bool Contains(AssetLocation draftCode);
	}
}
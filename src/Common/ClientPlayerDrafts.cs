using System.Collections;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts
{
	internal class ClientPlayerDrafts : IPlayerDrafts
	{
		public int Count => codes.Count;

		private readonly HashSet<AssetLocation> codes;

		public ClientPlayerDrafts(HashSet<AssetLocation> codes)
		{
			this.codes = codes;
		}

		public bool Contains(AssetLocation draftCode)
		{
			return codes.Contains(draftCode);
		}

		public IEnumerator<AssetLocation> GetEnumerator()
		{
			return codes.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return codes.GetEnumerator();
		}

		public void Dispose() { }
	}
}
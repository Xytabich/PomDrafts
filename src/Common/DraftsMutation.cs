using System.Runtime.CompilerServices;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts
{
	/// <summary>
	/// Represents the operation to modify a player's draft list
	/// </summary>
	public readonly ref struct DraftsMutation
	{
		private readonly ChangesContainer container;

		internal DraftsMutation(ServerPlayerDrafts drafts)
		{
			container = new(drafts);
		}

		/// <summary>
		/// Adds a draft to the list
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly void Add(AssetLocation draftCode)
		{
			container?.Add(draftCode);
		}

		/// <summary>
		/// Adds drafts to the list
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly void AddRange(IEnumerable<AssetLocation> draftCodes)
		{
			container?.UnionWith(draftCodes);
		}


		/// <summary>
		/// Removes a draft from the list
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly void Remove(AssetLocation draftCode)
		{
			if(container == null) return;

			if(!container.Remove(draftCode) && !container.clearMark)
			{
				(container.toRemove ??= new()).Add(draftCode);
			}
		}

		/// <summary>
		/// Removes drafts from the list
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly void Remove(IEnumerable<AssetLocation> draftCodes)
		{
			if(container == null) return;

			foreach(var code in draftCodes)
			{
				if(!container.Remove(code) && !container.clearMark)
				{
					(container.toRemove ??= new()).Add(code);
				}
			}
		}

		/// <summary>
		/// Removes all drafts
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly void Clear()
		{
			if(container == null) return;

			container.Clear();
			container.clearMark = true;
			container.toRemove?.Clear();
		}

		/// <summary>
		/// Commits changes and writes them to the database
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly void Commit()
		{
			container?.Commit();
		}

		/// <summary>
		/// Calls <see cref="Commit"/>
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly void Dispose()
		{
			Commit();
		}

		private class ChangesContainer : HashSet<AssetLocation>
		{
			public bool clearMark = false;
			public HashSet<AssetLocation>? toRemove = null;

			public readonly ServerPlayerDrafts drafts;

			public ChangesContainer(ServerPlayerDrafts drafts)
			{
				this.drafts = drafts;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Commit()
			{
				if(!clearMark && toRemove != null && toRemove.Count != 0)
				{
					drafts.Remove(toRemove);
					toRemove.Clear();
				}

				if(Count == 0) return;
				if(clearMark) drafts.Replace(this);
				else drafts.Add(this);
				Clear();
			}
		}
	}
}
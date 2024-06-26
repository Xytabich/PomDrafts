using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Common
{
	/// <summary>
	/// Represents an interface that describes drafts and interactions with them
	/// </summary>
	public interface IDraftsDescriptor
	{
		/// <summary>
		/// Initializes drafts.
		/// Can be called from a side thread.
		/// </summary>
		/// <param name="connectionTypes">Read-only collection of connection types. Used only on the server side, <see langword="null"/> on the client side</param>
		void InitDescriptor(ICollection<AssetLocation>? connectionTypes);

		/// <summary>
		/// Enumerates the codes that this descriptor serves.
		/// Can be called from a side thread.
		/// </summary>
		IEnumerable<AssetLocation> EnumerateDraftCodes();

		/// <summary>
		/// Returns a list of groups for the given draft code.
		/// A tab will be created for each group in the drafting table.
		/// Can be called from a side thread.
		/// </summary>
		string[] GetDraftGroups(AssetLocation draftCode);

		/// <summary>
		/// Returns the draft shape
		/// </summary>
		DraftShape GetDraftShape(AssetLocation draftCode);

		/// <summary>
		/// Returns whether the draft is interactive.
		/// Interactive drawings can be used via the draftsman folder, i.e. methods like <see cref="CollectibleObject.OnHeldInteractStart"/> will be called for them.
		/// The item should not depend on stored attributes, as it will be called with an empty list of attributes.
		/// However, if temporary or persistent attributes are changed during <see cref="CollectibleObject.OnHeldInteractStart"/> and others, they will be saved until the drawing in the folder is changed.
		/// </summary>
		bool IsInteractableDraft(AssetLocation draftCode);

		/// <summary>
		/// Returns itemstack, used to obtain information about an item, as well as used during interactions.
		/// Can be called from a side thread.
		/// </summary>
		ItemStack CreateDummyStack(AssetLocation draftCode);
	}
}
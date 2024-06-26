namespace PowerOfMind.Drafts
{
	public interface IMutablePlayerDrafts : IPlayerDrafts
	{
		/// <summary>
		/// Starts changing the draft list.
		/// Call <see cref="DraftsMutation.Commit"/> or <see cref="DraftsMutation.Dispose"/> to commit changes.
		/// </summary>
		DraftsMutation BeginChange();
	}
}
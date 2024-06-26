using PowerOfMind.Drafts.Network;
using System.Collections;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PowerOfMind.Drafts
{
	internal class ServerPlayerDrafts : IMutablePlayerDrafts
	{
		public int Count => Codes.Count;

		internal IServerPlayer? ServerPlayer = null;
		internal readonly HashSet<AssetLocation> Codes = new();

		private readonly DraftsModSystem draftsSystem;
		private readonly string uid;

		private int allocationCounter = 0;

		public ServerPlayerDrafts(DraftsModSystem draftsSystem, string uid)
		{
			this.draftsSystem = draftsSystem;
			this.uid = uid;
		}

		public void Allocate()
		{
			allocationCounter++;
		}

		public void Dispose()
		{
			allocationCounter--;
			if(allocationCounter == 0)
			{
				draftsSystem.draftsByPlayer.Remove(uid);
			}
		}

		public DraftsMutation BeginChange()
		{
			return new DraftsMutation(this);
		}

		public bool Contains(AssetLocation draftCode)
		{
			return Codes.Contains(draftCode);
		}

		public IEnumerator<AssetLocation> GetEnumerator()
		{
			return Codes.GetEnumerator();
		}

		internal void Add(HashSet<AssetLocation> set)
		{
			var database = draftsSystem.database!;

			if(set.Count == 1)
			{
				foreach(var code in set)
				{
					if(Codes.Add(code))
					{
						database.AddPlayerDraft(uid, code);
					}
				}
			}
			else
			{
				Codes.UnionWith(set);
				database.AddPlayerDrafts(uid, set);
			}

			SendUpdateMsg(set, DraftListAction.Add);
		}

		internal void Replace(HashSet<AssetLocation> set)
		{
			var database = draftsSystem.database!;

			Codes.Clear();
			database.ClearPlayerDrafts(uid);
			if(set.Count == 1)
			{
				foreach(var code in set)
				{
					if(Codes.Add(code))
					{
						database.AddPlayerDraft(uid, code);
					}
				}
			}
			else
			{
				Codes.UnionWith(set);
				database.AddPlayerDrafts(uid, set);
			}

			SendUpdateMsg(set, DraftListAction.Replace);
		}

		internal void Remove(HashSet<AssetLocation> set)
		{
			var database = draftsSystem.database!;

			if(set.Count == 1)
			{
				foreach(var code in set)
				{
					if(Codes.Remove(code))
					{
						database.RemovePlayerDraft(uid, code);
					}
				}
			}
			else
			{
				Codes.ExceptWith(set);
				database.RemovePlayerDrafts(uid, set);
			}

			SendUpdateMsg(set, DraftListAction.Remove);
		}

		private void SendUpdateMsg(HashSet<AssetLocation> set, DraftListAction action)
		{
			if(ServerPlayer != null)
			{
				draftsSystem.SendDraftListUpdate(ServerPlayer, new UpdateDraftListMsg() {
					Action = action,
					Count = set.Count,
					Data = SerializationUtils.SerializeCodes(set)
				});
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Codes.GetEnumerator();
		}
	}
}
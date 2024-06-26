using PowerOfMind.Drafts.Common;
using PowerOfMind.Drafts.GUI;
using PowerOfMind.Drafts.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PowerOfMind.Drafts
{
	public partial class DraftsModSystem
	{
		public delegate void OnClientDraftsUpdateDelegate(IReadOnlySet<AssetLocation> added, IReadOnlySet<AssetLocation> removed);

		/// <summary>
		/// Local player draft list update event
		/// </summary>
		public event OnClientDraftsUpdateDelegate OnClientDraftsUpdate { add => (clientUpdateListeners ??= new()).Add(value); remove => clientUpdateListeners?.Remove(value); }

		private ICoreClientAPI capi = default!;
		private HashSet<AssetLocation> playerDrafts = default!;
		private HashSet<string> draftGroups = default!;
		private ClientPlayerDrafts clientPlayerDrafts = default!;
		private IClientNetworkChannel clientChannel = default!;
		private List<OnClientDraftsUpdateDelegate>? clientUpdateListeners = null;

		public override void StartClientSide(ICoreClientAPI api)
		{
			capi = api;
			base.StartClientSide(api);

			playerDrafts = new();
			draftGroups = new();
			clientPlayerDrafts = new(playerDrafts);

			clientChannel = api.Network.RegisterChannel("pomdrafts")
				.RegisterMessageType<InitDraftsMsg>()
				.RegisterMessageType<UpdateDraftListMsg>()
				.SetMessageHandler<InitDraftsMsg>(OnReceivedInit)
				.SetMessageHandler<UpdateDraftListMsg>(OnReceivedDraftList);

			api.Input.RegisterHotKey(DRAFT_SELECT_HOTKEY, "Select Draft", GlKeys.F, shiftPressed: true);

			api.Event.BlockTexturesLoaded += ClientCollectDrafts;

			capi.Gui.RegisterDialog(new HudDraftDiscovery(capi));
		}

		public IEnumerable<string> GetDraftGroups()
		{
			return draftGroups;
		}

		/// <summary>
		/// Get all drafts for local player.
		/// Should only be called on the client side.
		/// </summary>
		public IPlayerDrafts GetPlayerDrafts()
		{
			return clientPlayerDrafts;
		}

		private void ClientCollectDrafts()
		{
			var errors = new List<string>();
			var draftDescriptors = new Dictionary<AssetLocation, IDraftsDescriptor>();
			var locker = new object();
			Parallel.ForEach(capi.World.Collectibles, () => {
				return (list: new List<(AssetLocation code, IDraftsDescriptor descriptor)>(), groups: new HashSet<string>(), errors: new List<string>());
			}, (item, _, state) => {
				if(item.Code == null) return state;

				foreach(var beh in item.CollectibleBehaviors)
				{
					if(beh is IDraftsDescriptor descriptor)
					{
						try
						{
							descriptor.InitDescriptor(null);
						}
						catch(Exception e)
						{
							state.errors.Add(e.ToString());
						}
						foreach(var code in descriptor.EnumerateDraftCodes())
						{
							state.list.Add((code, descriptor));
							foreach(var group in descriptor.GetDraftGroups(code))
							{
								state.groups.Add(group);
							}
						}
					}
				}
				return state;
			}, state => {
				lock(locker)
				{
					foreach(var info in state.list)
					{
						draftDescriptors.Add(info.code, info.descriptor);
					}
					draftGroups.UnionWith(state.groups);
					errors.AddRange(state.errors);
				}
			});
			if(errors.Count != 0)
			{
				foreach(var log in errors)
				{
					Api.Logger.Error(log);
				}
			}
			errors = null;
			GC.Collect();

			draftDescriptors.TrimExcess();
			assetsSystem.DraftDescriptors = draftDescriptors;
		}

		private void OnReceivedInit(InitDraftsMsg packet)
		{
			playerDrafts.Clear();
			SerializationUtils.DeserializeCodes(packet.DraftsData, packet.DraftCount, playerDrafts);

			var typesTmpData = new List<AssetLocation>();
			SerializationUtils.DeserializeCodes(packet.TypesData, packet.TypeCount * 4, typesTmpData);

			var outTypes = assetsSystem.ConnectionTypes;
			int count = packet.TypeCount;
			for(int i = 0; i < count; i++)
			{
				int offset = i * 4;
				outTypes[typesTmpData[offset]] = new ConnectionType() {
					Code = typesTmpData[offset],
					Textures = new() {
						Input = typesTmpData[offset + 1],
						Output = typesTmpData[offset + 2],
						Connected = typesTmpData[offset + 3],
					}
				};
			}
		}

		private void OnReceivedDraftList(UpdateDraftListMsg packet)
		{
			var list = new List<AssetLocation>();//TODO: cache collections
			var addedSet = new HashSet<AssetLocation>();
			var removedSet = new HashSet<AssetLocation>();

			switch(packet.Action)
			{
				case DraftListAction.Add:
					SerializationUtils.DeserializeCodes(packet.Data, packet.Count, addedSet);
					playerDrafts.UnionWith(addedSet);
					ShowClientDiscoveryMsg(addedSet);
					break;
				case DraftListAction.Remove:
					SerializationUtils.DeserializeCodes(packet.Data, packet.Count, removedSet);
					playerDrafts.ExceptWith(removedSet);
					break;
				case DraftListAction.Replace:
					removedSet.UnionWith(playerDrafts);
					playerDrafts.Clear();
					goto case DraftListAction.Add;
			}

			if(addedSet.Count == 0 && removedSet.Count == 0) return;
			if(clientUpdateListeners != null)
			{
				for(int i = clientUpdateListeners.Count - 1; i >= 0; i--)
				{
					clientUpdateListeners[i].Invoke(addedSet, removedSet);
				}
			}
		}

		private void ShowClientDiscoveryMsg(HashSet<AssetLocation> list)
		{
			foreach(var code in list)
			{
				try
				{
					capi.ShowChatMessage(Lang.Get("pomdrafts:chatdiscovery", GetDescriptor(code)!.CreateDummyStack(code).GetName()));
				}
				catch { }
			}
		}
	}
}
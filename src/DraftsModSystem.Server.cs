using PowerOfMind.Drafts.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PowerOfMind.Drafts
{
	public partial class DraftsModSystem
	{
		internal Dictionary<string, ServerPlayerDrafts> draftsByPlayer = default!;
		internal DraftsDatabase? database = null;

		private ICoreServerAPI sapi = default!;
		private IServerNetworkChannel serverChannel = default!;

		private (byte[]? data, int count) cachedConnTypesData;

		public override void StartServerSide(ICoreServerAPI api)
		{
			sapi = api;
			base.StartServerSide(api);

			draftsByPlayer = new();

			api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, OnGameReady);

			serverChannel = api.Network.RegisterChannel("pomdrafts")
				.RegisterMessageType<InitDraftsMsg>()
				.RegisterMessageType<UpdateDraftListMsg>();
			api.Event.PlayerJoin += OnPlayerJoin;
			api.Event.PlayerLeave += OnPlayerLeave;

			RegisterCommands(api);
		}

		/// <summary>
		/// Get all drafts for a player.
		/// Should only be called on the server side.
		/// After use, Dispose should be called so that the data doesn't dangling in memory.
		/// </summary>
		/// <param name="load">Forces data load if the player is not on the server</param>
		/// <returns>Player drafts or <see langword="null"/> if not found</returns>
		public IMutablePlayerDrafts? GetPlayerDrafts(string playerUID, bool load = false)
		{
			if(sapi == null) throw new Exception("Should only be called on the server side");

			if(string.IsNullOrEmpty(playerUID)) return null;
			if(draftsByPlayer.TryGetValue(playerUID, out var drafts))
			{
				drafts.Allocate();
				return drafts;
			}
			if(load)
			{
				drafts = new ServerPlayerDrafts(this, playerUID);

				GetDatabase().GetPlayerDrafts(playerUID, drafts.Codes);

				draftsByPlayer[playerUID] = drafts;
				drafts.Allocate();
				return drafts;
			}
			return null;
		}

		internal void SendDraftListUpdate(IServerPlayer byPlayer, UpdateDraftListMsg msg)
		{
			serverChannel.SendPacket(msg, byPlayer);
		}

		private void OnGameReady()
		{
			GetDatabase();
		}

		private void OnPlayerJoin(IServerPlayer byPlayer)
		{
			if(string.IsNullOrEmpty(byPlayer.PlayerUID)) return;

			if(cachedConnTypesData == default) InitConnTypesDataCache();

			var drafts = (ServerPlayerDrafts)GetPlayerDrafts(byPlayer.PlayerUID, true)!;
			drafts.ServerPlayer = byPlayer;
			serverChannel.SendPacket(new InitDraftsMsg() {
				DraftCount = drafts.Codes.Count,
				DraftsData = SerializationUtils.SerializeCodes(drafts.Codes),
				TypeCount = cachedConnTypesData.count,
				TypesData = cachedConnTypesData.data!
			}, byPlayer);
		}

		private void OnPlayerLeave(IServerPlayer byPlayer)
		{
			if(string.IsNullOrEmpty(byPlayer.PlayerUID)) return;
			if(draftsByPlayer.TryGetValue(byPlayer.PlayerUID, out var drafts))
			{
				drafts.ServerPlayer = null;
				drafts.Dispose();
			}
		}

		private void InitConnTypesDataCache()
		{
			var tmpTypesData = new List<AssetLocation>();
			var types = assetsSystem.ConnectionTypes;
			foreach(var pair in types)
			{
				tmpTypesData.Add(pair.Value.Code);
				tmpTypesData.Add(pair.Value.Textures.Input);
				tmpTypesData.Add(pair.Value.Textures.Output);
				tmpTypesData.Add(pair.Value.Textures.Connected);
			}
			cachedConnTypesData = (SerializationUtils.SerializeCodes(tmpTypesData), types.Count);
		}

		private DraftsDatabase GetDatabase()
		{
			if(database == null)
			{
				database = new DraftsDatabase(sapi.World, sapi.Logger);
				database.Init();
			}
			return database;
		}
	}
}
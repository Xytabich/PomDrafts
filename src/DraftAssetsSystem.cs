using Newtonsoft.Json.Linq;
using PowerOfMind.Drafts.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.ServerMods;

namespace PowerOfMind.Drafts
{
	public class DraftAssetsSystem : ModSystem
	{
		/// <summary>
		/// List of connection types between drafts
		/// </summary>
		public readonly Dictionary<AssetLocation, ConnectionType> ConnectionTypes = new();

		/// <summary>
		/// List of recipes sorted by nodes using <see cref="SchematicRecipe.RecipeNodesComparer"/>
		/// </summary>
		public List<SchematicRecipe> SchematicRecipes { get; private set; } = default!;

		public IReadOnlyDictionary<AssetLocation, IDraftsDescriptor> DraftDescriptors { get; internal set; } = default!;

		private ICoreClientAPI? capi = null;
		private TextureAtlasManager? connectionsAtlas = null;

		public override double ExecuteOrder()
		{
			return 2;
		}

		public override void Start(ICoreAPI api)
		{
			capi = api as ICoreClientAPI;
			base.Start(api);

			SchematicRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<SchematicRecipe>>("pomdrafts").Recipes;
		}

		public override void Dispose()
		{
			connectionsAtlas?.Dispose();
		}

		public override void AssetsLoaded(ICoreAPI api)
		{
			if(api.Side == EnumAppSide.Server)
			{
				var typeAssets = api.Assets.GetMany<JToken>(api.Logger, "config/pomdrafts/connections.json");
				foreach(var pair in typeAssets)
				{
					try
					{
						foreach(var conn in pair.Value.ToObject<ConnectionType[]>(pair.Key.Domain))
						{
							if(ConnectionTypes.ContainsKey(conn.Code))
							{
								api.Logger.Error("[pomdrafts] Cannot add connection type {0} to the list because such a type already exists. File '{1}'", conn.Code, pair.Key);
								continue;
							}
							ConnectionTypes[conn.Code] = conn;
						}
					}
					catch(Exception e)
					{
						api.Logger.Error("[pomdrafts] Unable to load list of connections from file '{0}': {1}", pair.Key, e.Message);
					}
				}

				var errors = new List<string>();
				var connectionTypeKeys = ConnectionTypes.Keys;
				var draftDescriptors = new Dictionary<AssetLocation, IDraftsDescriptor>();
				var locker = new object();
				Parallel.ForEach(api.World.Blocks.AsEnumerable<CollectibleObject>().Union(api.World.Items), () => {
					return (list: new List<(AssetLocation code, IDraftsDescriptor descriptor)>(), errors: new List<string>());
				}, (item, _, state) => {
					if(item.Code == null) return state;

					foreach(var beh in item.CollectibleBehaviors)
					{
						if(beh is IDraftsDescriptor descriptor)
						{
							try
							{
								descriptor.InitDescriptor(connectionTypeKeys);
							}
							catch(Exception e)
							{
								state.errors.Add(e.ToString());
							}
							foreach(var code in descriptor.EnumerateDraftCodes())
							{
								state.list.Add((code, descriptor));
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
						errors.AddRange(state.errors);
					}
				});
				if(errors.Count != 0)
				{
					foreach(var log in errors)
					{
						api.Logger.Error(log);
					}
				}
				errors = null;
				GC.Collect();

				draftDescriptors.TrimExcess();
				DraftDescriptors = draftDescriptors;

				var recipeLoader = api.ModLoader.GetModSystem<RecipeLoader>();
				recipeLoader.LoadRecipes<SchematicRecipe>("schematic recipe", "recipes/pomdrafts", AddRecipe);
				SchematicRecipes.Sort(new SchematicRecipe.RecipeNodesComparer());
				for(int i = 0; i < SchematicRecipes.Count; i++)
				{
					SchematicRecipes[i].RecipeId = i;
				}
			}
		}

		public TextureAtlasPosition GetConnectionTexture(AssetLocation connectionType, ConnectionTextureType textureType)
		{
			if(capi == null) throw new InvalidOperationException("Textures are only available on the client side");
			if(connectionsAtlas == null)
			{
				connectionsAtlas = new TextureAtlasManager((ClientMain)capi!.World);
				connectionsAtlas.Size = new Size2i(512, 512);
				connectionsAtlas.CreateNewAtlas("entities");
				connectionsAtlas.PopulateTextureAtlassesFromTextures();
				connectionsAtlas.ComposeTextureAtlasses_StageA();
				connectionsAtlas.ComposeTextureAtlasses_StageB();
				connectionsAtlas.ComposeTextureAtlasses_StageC();
			}
			if(connectionType == null || !ConnectionTypes.TryGetValue(connectionType, out var info))
			{
				return connectionsAtlas.TextureAtlasPositionsByTextureSubId[0];
			}

			var texture = textureType switch {
				ConnectionTextureType.Input => info.Textures.Input,
				ConnectionTextureType.Output => info.Textures.Output,
				_ => info.Textures.Connected
			};
			if(connectionsAtlas.ContainsKey(texture))
			{
				return connectionsAtlas[texture];
			}

			var bitmap = capi.Assets.TryGet(texture.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi);
			if(!connectionsAtlas.GetOrInsertTexture(texture, out _, out var texPos, () => bitmap))
			{
				texPos = connectionsAtlas.TextureAtlasPositionsByTextureSubId[0];
			}
			return texPos;
		}

		private void AddRecipe(SchematicRecipe recipe)
		{
			SchematicRecipes.Add(recipe);
		}
	}
}
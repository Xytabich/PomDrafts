using PowerOfMind.Drafts.Blocks;
using PowerOfMind.Drafts.Blocks.Behaviors;
using PowerOfMind.Drafts.Blocks.Entities;
using PowerOfMind.Drafts.Blocks.TriggerBehaviors;
using PowerOfMind.Drafts.Common;
using PowerOfMind.Drafts.Entities.TriggerBehaviors;
using PowerOfMind.Drafts.Items.Behaviors;
using PowerOfMind.Drafts.Items.TriggerBehaviors;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts
{
	public partial class DraftsModSystem : ModSystem
	{
		public const string DRAFT_SELECT_HOTKEY = "pom:select-draft";

		internal ICoreAPI Api = default!;

		private DraftAssetsSystem assetsSystem = default!;

		public override void Start(ICoreAPI api)
		{
			this.Api = api;
			base.Start(api);

			assetsSystem = api.ModLoader.GetModSystem<DraftAssetsSystem>();

			api.RegisterBlockClass("pomdrafts:draftingtable", typeof(BlockDraftingTable));
			api.RegisterBlockEntityClass("pomdrafts:draftingtable", typeof(BlockEntityDraftingTable));
			api.RegisterBlockBehaviorClass("pomcore:mbhorplacer", typeof(HorizontalMultiblockPlacer));//TODO: move to core

			api.RegisterCollectibleBehaviorClass("pomdrafts:draft", typeof(ItemBehaviorDraft));

			//api.RegisterCollectibleBehaviorClass("pomdrafts:folder", typeof(ItemFolderBehavior));
			//api.RegisterCollectibleBehaviorClass("pomdrafts:folder-interact", typeof(ItemFolderInteractBehavior));

			api.RegisterBlockBehaviorClass("pomdrafts:trigger-block-interact", typeof(BlockInteractTrigger));
			api.RegisterBlockBehaviorClass("pomdrafts:trigger-block-break", typeof(BlockBreakTrigger));
			api.RegisterBlockBehaviorClass("pomdrafts:trigger-block-place", typeof(BlockPlaceTrigger));

			api.RegisterCollectibleBehaviorClass("pomdrafts:trigger-item-interact", typeof(ItemInteractTrigger));
			api.RegisterCollectibleBehaviorClass("pomdrafts:trigger-item-attack", typeof(ItemAttackTrigger));
			api.RegisterCollectibleBehaviorClass("pomdrafts:trigger-item-mining", typeof(ItemMiningTrigger));

			api.RegisterEntityBehaviorClass("pomdrafts:trigger-entity-interact", typeof(EntityInteractTrigger));
			api.RegisterEntityBehaviorClass("pomdrafts:trigger-entity-attack", typeof(EntityAttackedTrigger));
			api.RegisterEntityBehaviorClass("pomdrafts:trigger-entity-death", typeof(EntityDeathTrigger));

			api.RegisterEntityBehaviorClass("pomdrafts:trigger-player-attack", typeof(AttackingPlayerTrigger));

			api.RegisterEntityBehaviorClass("pomdrafts:trigger-dialogue", typeof(EntityDialogueTrigger));
		}

		public DraftShape GetDraftShape(AssetLocation draftCode)
		{
			return assetsSystem.DraftDescriptors[draftCode].GetDraftShape(draftCode);
		}

		public IDraftsDescriptor? GetDescriptor(AssetLocation draftCode)
		{
			return assetsSystem.DraftDescriptors.TryGetValue(draftCode, out var descriptor) ? descriptor : null;
		}

		public void FindMatchingRecipes(IReadOnlyDictionary<(int x, int y), (AssetLocation code, int rotation)> drafts, ICollection<SchematicRecipe> outRecipes)
		{
			var connSlots = new Dictionary<(int x, int y, int dir), (AssetLocation type, AssetLocation draft, bool isOutput, (int, int) draftKey)>();//TODO: cache collection
			foreach(var draft in drafts)
			{
				var index = draft.Key;
				var rotation = draft.Value.rotation;
				var code = draft.Value.code;
				var shape = GetDraftShape(code);

				var xMax = shape.Width - 1;
				var yMax = shape.Height - 1;

				foreach(var cell in shape.Cells)
				{
					if(cell.Connections == null) continue;

					var offset = Utils.RotateOffset(cell.X, cell.Y, xMax, yMax, rotation);
					offset.x += index.x;
					offset.y += index.y;
					for(int j = 0; j < 4; j++)
					{
						var info = cell.Connections[j];
						if(info != default)
						{
							connSlots[(offset.x, offset.y, (j + rotation) & 3)] = (info.type!, code, info.isOutput, draft.Key);
						}
					}
				}
			}

			var draftOutputs = new Dictionary<(int, int), int>();//TODO: use refdict & store DictIndex in draftKey
			var connections = new List<SchematicRecipe.ConnectionInfo>();//TODO: cache collection
			foreach(var pair in connSlots)
			{
				if(pair.Value.isOutput)
				{
					var (x, y) = Utils.OffsetByDirection(pair.Key.x, pair.Key.y, pair.Key.dir);
					if(connSlots.TryGetValue((x, y, (pair.Key.dir + 2) & 3), out var info) && !info.isOutput)
					{
						if(info.type.Equals(pair.Value.type))
						{
							connections.Add(new(pair.Value.draft, info.draft, info.type));

							if(!draftOutputs.TryGetValue(pair.Value.draftKey, out int count))
							{
								count = 0;
							}
							draftOutputs[pair.Value.draftKey] = count + 1;
						}
					}
				}
			}
			connections.Sort();

			var sortedNodes = new List<SchematicRecipe.NodeInfo>(drafts.Count);//TODO: cache list
			foreach(var pair in drafts)
			{
				if(!draftOutputs.TryGetValue(pair.Key, out int count))
				{
					count = 0;
				}
				sortedNodes.Add(new(pair.Value.code, count));
			}
			sortedNodes.Sort();

			var allRecipes = assetsSystem.SchematicRecipes;

			int i;
			int lo = 0;
			int hi = allRecipes.Count - 1;
			while(lo <= hi)
			{
				i = lo + ((hi - lo) >> 1);
				int order = CompareNodes(allRecipes[i], sortedNodes);

				if(order == 0)
				{
					goto _found;
				}
				if(order < 0)
				{
					lo = i + 1;
				}
				else
				{
					hi = i - 1;
				}
			}
			return;

_found:
			int from = i;
			while(from > 0)
			{
				if(NodesEquals(allRecipes[from - 1], sortedNodes))
				{
					from--;
					continue;
				}
				break;
			}

			int latest = allRecipes.Count - 1;
			int to = i;
			while(to < latest)
			{
				if(NodesEquals(allRecipes[to + 1], sortedNodes))
				{
					to++;
					continue;
				}
				break;
			}

			i = from;
			while(i <= to)
			{
				if(allRecipes[i].Enabled)
				{
					if(ConnectionsEqual(connections, allRecipes[i].SortedConnections))
					{
						outRecipes.Add(allRecipes[i]);
					}
				}
				i++;
			}
		}

		private static bool ConnectionsEqual(List<SchematicRecipe.ConnectionInfo> connections, SchematicRecipe.ConnectionInfo[] recipeConnections)
		{
			int connLength = recipeConnections.Length;
			if(connLength == connections.Count)
			{
				int connIndex = 0;
				foreach(var conn in connections)
				{
					if(!recipeConnections[connIndex].Equals(conn))
					{
						return false;
					}
					connIndex++;
				}

				return true;
			}
			return false;
		}

		private static int CompareNodes(SchematicRecipe recipe, List<SchematicRecipe.NodeInfo> nodes)
		{
			int c = recipe.SortedNodes.Length.CompareTo(nodes.Count);
			if(c != 0) return c;

			int len = nodes.Count;
			for(int i = 0; i < len; i++)
			{
				c = recipe.SortedNodes[i].CompareTo(nodes[i]);
				if(c != 0) return c;
			}

			return 0;
		}

		private static bool NodesEquals(SchematicRecipe recipe, List<SchematicRecipe.NodeInfo> nodes)
		{
			if(recipe.SortedNodes.Length != nodes.Count)
			{
				return false;
			}

			int len = nodes.Count;
			for(int i = 0; i < len; i++)
			{
				if(!recipe.SortedNodes[i].Equals(nodes[i]))
				{
					return false;
				}
			}

			return true;
		}
	}
}
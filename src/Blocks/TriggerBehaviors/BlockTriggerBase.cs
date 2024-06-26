using PowerOfMind.Drafts.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace PowerOfMind.Drafts.Blocks.TriggerBehaviors
{
	public abstract class BlockTriggerBase : BlockBehavior
	{
		protected AssetLocation? draftCode = null;
		protected CraftingRecipeIngredient? requiredItem = null;
		protected DraftsModSystem draftsSystem = default!;
		protected ICoreAPI api = default!;

		public BlockTriggerBase(Block block) : base(block)
		{
		}

		public override void Initialize(JsonObject properties)
		{
			base.Initialize(properties);

			var props = properties.AsObject<Properties?>(null, collObj.Code.Domain);
			draftCode = props?.Draft;
			requiredItem = props?.ByItem;
			if(requiredItem?.Code == null)
			{
				requiredItem = null;
			}
		}

		public override void OnLoaded(ICoreAPI api)
		{
			this.api = api;
			base.OnLoaded(api);

			draftsSystem = api.ModLoader.GetModSystem<DraftsModSystem>();
			if(draftCode == null)
			{
				api.Logger.Warning("[pomdrafts] Missing draft code for block {0}", collObj.Code);
			}
			if(api.Side == EnumAppSide.Client)
			{
				requiredItem = null;// Don't check conditions on client side
			}
			else
			{
				if(requiredItem != null && !requiredItem.Resolve(api.World, "draft block trigger"))
				{
					draftCode = null;
				}
			}
		}

		protected EnumLearnResult TryTrigger(IPlayer? player, ItemStack? itemStack)
		{
			if(draftCode == null) return EnumLearnResult.None;
			if(player == null) return EnumLearnResult.None;

			bool apply = true;
			if(requiredItem != null)
			{
				apply = itemStack != null && requiredItem.SatisfiesAsIngredient(itemStack);
			}
			if(apply)
			{
				var result = draftsSystem.TryLearnDraft(player, draftCode);
				if(result == EnumLearnResult.Failed)
				{
					api.Logger.Warning("[pomdrafts] Failed to learn draft {0} using {1}. Probably such a draft does not exist.", draftCode, collObj.Code);
				}
				return result;
			}
			return EnumLearnResult.None;
		}

		protected class Properties
		{
			public AssetLocation? Draft;
			public CraftingRecipeIngredient? ByItem;
		}
	}
}
using PowerOfMind.Drafts.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace PowerOfMind.Drafts.Entities.TriggerBehaviors
{
	public abstract class EntityTriggerBase : EntityBehavior
	{
		protected AssetLocation? draftCode = null;
		protected CraftingRecipeIngredient? requiredItem = null;
		protected DraftsModSystem draftsSystem = default!;
		protected ICoreAPI api = default!;

		public EntityTriggerBase(Entity entity) : base(entity)
		{
		}

		public override void Initialize(EntityProperties properties, JsonObject attributes)
		{
			base.Initialize(properties, attributes);

			var props = attributes.AsObject<Properties?>(null, entity.Code.Domain);
			draftCode = props?.Draft;
			requiredItem = props?.ByItem;
			if(requiredItem?.Code == null)
			{
				requiredItem = null;
			}
		}

		public override void AfterInitialized(bool onFirstSpawn)
		{
			api = entity.Api;

			draftsSystem = api.ModLoader.GetModSystem<DraftsModSystem>();
			if(draftCode == null)
			{
				api.Logger.Warning("[pomdrafts] Missing draft code for entity {0}", entity.Code);
			}
			if(api.Side == EnumAppSide.Client)
			{
				requiredItem = null;// Don't check conditions on client side
			}
			else
			{
				if(requiredItem != null && !requiredItem.Resolve(api.World, "draft entity trigger"))
				{
					draftCode = null;
				}
			}
		}

		protected EnumLearnResult TryTrigger(Entity byEntity, ItemStack? itemStack)
		{
			if(draftCode == null) return EnumLearnResult.None;
			if(byEntity is not EntityPlayer player) return EnumLearnResult.None;

			bool apply = true;
			if(requiredItem != null)
			{
				apply = itemStack != null && requiredItem.SatisfiesAsIngredient(itemStack);
			}
			if(apply)
			{
				var result = draftsSystem.TryLearnDraft(player.Player, draftCode);
				if(result == EnumLearnResult.Failed)
				{
					api.Logger.Warning("[pomdrafts] Failed to learn draft {0} using {1}. Probably such a draft does not exist.", draftCode, entity.Code);
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
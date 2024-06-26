using PowerOfMind.Drafts.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace PowerOfMind.Drafts.Items.TriggerBehaviors
{
	public abstract class ItemTriggerBase : CollectibleBehavior
	{
		protected AssetLocation? draftCode = null;
		protected TargetInfo? target = null;
		protected DraftsModSystem draftsSystem = default!;
		protected ICoreAPI api = default!;

		public ItemTriggerBase(CollectibleObject collObj) : base(collObj)
		{
		}

		public override void Initialize(JsonObject properties)
		{
			base.Initialize(properties);

			var props = properties.AsObject<Properties?>(null, collObj.Code.Domain);
			draftCode = props?.Draft;
			target = props?.Target;
			if(target?.Code == null)
			{
				target = null;
			}
		}

		public override void OnLoaded(ICoreAPI api)
		{
			this.api = api;
			base.OnLoaded(api);

			draftsSystem = api.ModLoader.GetModSystem<DraftsModSystem>();
			if(draftCode == null)
			{
				api.Logger.Warning("[pomdrafts] Missing draft code for item {0}", collObj.Code);
			}
			if(api.Side == EnumAppSide.Client)
			{
				target = null;// Don't check conditions on client side
			}
		}

		protected EnumLearnResult TryTrigger(Entity byEntity, BlockSelection? blockSel, EntitySelection? entitySel)
		{
			if(draftCode == null) return EnumLearnResult.None;
			if(byEntity is not EntityPlayer player) return EnumLearnResult.None;

			bool apply = true;
			if(target != null)
			{
				apply = false;
				switch(target.Type)
				{
					case TargetType.Block:
						if(blockSel != null)
						{
							apply = WildcardUtil.Match(target.Code, blockSel.Block.Code, target.AllowedVariants);
						}
						break;
					case TargetType.Entity:
						if(entitySel != null)
						{
							apply = WildcardUtil.Match(target.Code, entitySel.Entity.Code, target.AllowedVariants);
						}
						break;
				}
			}
			if(apply)
			{
				var result = draftsSystem.TryLearnDraft(player.Player, draftCode);
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
			public TargetInfo? Target;
		}

		protected class TargetInfo
		{
			public TargetType Type = TargetType.Block;
			public AssetLocation? Code;
			public string[]? AllowedVariants;
		}

		protected enum TargetType
		{
			Block,
			Entity
		}
	}
}
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace PowerOfMind.Drafts.Items.TriggerBehaviors
{
	public class ItemMiningTrigger : ItemTriggerBase
	{
		public ItemMiningTrigger(CollectibleObject collObj) : base(collObj)
		{
		}

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);
			if(target == null)
			{
				api.Logger.Warning("[pomdrafts] Missing target for item {0}", collObj.Code);
			}
			else
			{
				target.Type = TargetType.Block;
			}
		}

		public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier, ref EnumHandling bhHandling)
		{
			TryTrigger(byEntity, blockSel, null);
			return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier, ref bhHandling);
		}
	}
}
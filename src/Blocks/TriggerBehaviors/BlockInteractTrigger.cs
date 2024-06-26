using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Blocks.TriggerBehaviors
{
	public class BlockInteractTrigger : BlockTriggerBase
	{
		public BlockInteractTrigger(Block block) : base(block)
		{
		}

		public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
		{
			if(TryTrigger(byPlayer, byPlayer?.InventoryManager.ActiveHotbarSlot?.Itemstack) == Common.EnumLearnResult.Learned)
			{
				if(api.Side == EnumAppSide.Client)
				{
					handling = EnumHandling.Handled;
					return true;
				}
			}
			return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
		}
	}
}
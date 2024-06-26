using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Blocks.TriggerBehaviors
{
	public class BlockPlaceTrigger : BlockTriggerBase
	{
		public BlockPlaceTrigger(Block block) : base(block)
		{
		}

		public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack, ref EnumHandling handling)
		{
			TryTrigger(byPlayer, byItemStack);
			return base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack, ref handling);
		}
	}
}
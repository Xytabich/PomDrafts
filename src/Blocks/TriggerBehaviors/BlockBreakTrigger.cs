using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Drafts.Blocks.TriggerBehaviors
{
	public class BlockBreakTrigger : BlockTriggerBase
	{
		public BlockBreakTrigger(Block block) : base(block)
		{
		}

		public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handling)
		{
			TryTrigger(byPlayer, byPlayer?.InventoryManager.ActiveHotbarSlot?.Itemstack);
			base.OnBlockBroken(world, pos, byPlayer, ref handling);
		}
	}
}
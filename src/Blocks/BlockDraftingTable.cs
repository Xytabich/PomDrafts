using PowerOfMind.Drafts.Blocks.Entities;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Blocks
{
	public class BlockDraftingTable : Block
	{
		public int HorizontalTiles, VerticalTiles;

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);

			var values = Attributes["size"].AsArray<int>();
			(HorizontalTiles, VerticalTiles) = (values[0], values[1]);
		}

		public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			if(!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
			{
				return false;
			}
			var be = GetBlockEntity<BlockEntityDraftingTable>(blockSel);
			if(be != null && be.OnPlayerInteract(byPlayer, blockSel))
			{
				return true;
			}
			return base.OnBlockInteractStart(world, byPlayer, blockSel);
		}
	}
}
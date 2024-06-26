using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PowerOfMind.Drafts.Blocks.Behaviors
{
	public class HorizontalMultiblockPlacer : BlockBehavior
	{
		private Vec3i[] offsets = Array.Empty<Vec3i>();

		public HorizontalMultiblockPlacer(Block block) : base(block)
		{
		}

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);
			offsets = block.Attributes["pom-multiblock"].AsArray(Array.Empty<Vec3i>());
		}

		public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
		{
			var dir = Block.SuggestedHVOrientation(byPlayer, blockSel)[0].Index;
			if(IterateOffsetPositions(blockSel.Position, offsets, dir, placement => {
				if(placement.position == blockSel.Position)
				{
					return false;
				}
				if(!world.BlockAccessor.GetBlock(placement.position, 1).IsReplacableBy(block))
				{
					return true;
				}
				return false;
			}))
			{
				handling = EnumHandling.PreventDefault;
				failureCode = "notenoughspace";
				return false;
			}
			return base.CanPlaceBlock(world, byPlayer, blockSel, ref handling, ref failureCode);
		}

		public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack, ref EnumHandling handling)
		{
			handling = EnumHandling.PreventDefault;
			var pos = blockSel.Position;
			var ba = world.BlockAccessor;
			var orientation = Block.SuggestedHVOrientation(byPlayer, blockSel)[0];
			ba.SetBlock(ba.GetBlock(block.CodeWithVariant("side", orientation.Code)).BlockId, pos);
			if(world.Side == EnumAppSide.Server)
			{
				IterateOffsetPositions(blockSel.Position, offsets, orientation.Index, placement => {
					if(placement.position == blockSel.Position)
					{
						return false;
					}
					string x = ((placement.offset.X < 0) ? "n" : ((placement.offset.X > 0) ? "p" : "")) + Math.Abs(placement.offset.X);
					string y = ((placement.offset.Y < 0) ? "n" : ((placement.offset.Y > 0) ? "p" : "")) + Math.Abs(placement.offset.Y);
					string z = ((placement.offset.Z < 0) ? "n" : ((placement.offset.Z > 0) ? "p" : "")) + Math.Abs(placement.offset.Z);
					var blockCode = new AssetLocation("multiblock-monolithic-" + x + "-" + y + "-" + z);
					world.BlockAccessor.SetBlock(world.GetBlock(blockCode).Id, placement.position);
					return false;
				});
			}
			return true;
		}

		public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
		{
			if(world.Side == EnumAppSide.Client)
			{
				return;
			}
			int dir = BlockFacing.FromCode(block.Variant["side"]).Index;
			IterateOffsetPositions(pos, offsets, dir, placement => {
				if(placement.position == pos)
				{
					return false;
				}
				if(world.BlockAccessor.GetBlock(placement.position) is BlockMultiblock)
				{
					world.BlockAccessor.SetBlock(0, placement.position);
				}
				return false;
			});
			base.OnBlockRemoved(world, pos, ref handling);
		}

		private static bool IterateOffsetPositions(BlockPos mainPos, Vec3i[] offsets, int direction, System.Func<(BlockPos position, Vec3i offset), bool> callback)
		{
			var tmpOffset = new Vec3i();
			var tmpPos = mainPos.Copy();
			foreach(var offset in offsets)
			{
				(tmpOffset.X, tmpOffset.Z) = Utils.RotateOffset(offset.X, offset.Z, direction);
				tmpOffset.Y = offset.Y;

				tmpPos.Set(mainPos);
				tmpPos.Add(tmpOffset);

				if(callback((tmpPos, tmpOffset))) return true;
			}
			return false;
		}
	}
}
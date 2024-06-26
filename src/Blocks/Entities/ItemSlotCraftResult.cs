using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Blocks.Entities
{
	internal class ItemSlotCraftResult : ItemSlotOutput
	{
		public bool hasLeftOvers;

		private ItemStack? prevStack = null;
		private new InventoryDraftingTable inventory;

		public ItemSlotCraftResult(InventoryDraftingTable inventory)
			: base(inventory)
		{
			this.inventory = inventory;
		}

		protected override void FlipWith(ItemSlot withSlot)
		{
		}

		public override int TryPutInto(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
		{
			if(Empty || op.ActingPlayer?.Entity == null)
			{
				return 0;
			}
			op.RequestedQuantity = StackSize;
			if(hasLeftOvers)
			{
				int moved = base.TryPutInto(sinkSlot, ref op);
				if(!Empty) return moved;

				hasLeftOvers = false;
				inventory.ConsumeIngredients(sinkSlot, op.ActingPlayer.Entity);
				if(inventory.CanStillCraftCurrent())
				{
					itemstack = prevStack?.Clone();
				}
			}
			if(op.ShiftDown)
			{
				CraftMany(sinkSlot, ref op);
			}
			else
			{
				CraftSingle(sinkSlot, ref op);
			}
			return op.MovedQuantity;
		}

		public virtual int TryPutIntoNoEvent(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
		{
			if(!sinkSlot.CanTakeFrom(this) || !CanTake() || itemstack == null)
			{
				return 0;
			}
			if(sinkSlot.Itemstack == null)
			{
				int q = Math.Min(sinkSlot.GetRemainingSlotSpace(Itemstack), op.RequestedQuantity);
				if(q > 0)
				{
					sinkSlot.Itemstack = TakeOut(q);
					op.MovedQuantity = (op.MovableQuantity = Math.Min(sinkSlot.StackSize, q));
				}
				return op.MovedQuantity;
			}

			var mergeop = op.ToMergeOperation(sinkSlot, this);
			op = mergeop;

			int origRequestedQuantity = op.RequestedQuantity;
			op.RequestedQuantity = Math.Min(sinkSlot.GetRemainingSlotSpace(itemstack), op.RequestedQuantity);
			sinkSlot.Itemstack.Collectible.TryMergeStacks(mergeop);
			op.RequestedQuantity = origRequestedQuantity;
			return mergeop.MovedQuantity;
		}

		private void CraftSingle(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
		{
			int prevQuantity = StackSize;
			int num = TryPutIntoNoEvent(sinkSlot, ref op);
			if(num == prevQuantity)
			{
				inventory.ConsumeIngredients(sinkSlot, op.ActingPlayer.Entity);
			}
			if(num > 0)
			{
				sinkSlot.OnItemSlotModified(sinkSlot.Itemstack);
				OnItemSlotModified(sinkSlot.Itemstack);
			}
		}

		private void CraftMany(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
		{
			if(itemstack == null)
			{
				return;
			}
			int movedtotal = 0;
			while(true)
			{
				prevStack = itemstack.Clone();
				int stackSize = StackSize;
				op.RequestedQuantity = stackSize;
				op.MovedQuantity = 0;
				int mv = TryPutIntoNoEvent(sinkSlot, ref op);
				movedtotal += mv;
				if(stackSize > mv)
				{
					hasLeftOvers = mv > 0;
					break;
				}
				inventory.ConsumeIngredients(sinkSlot, op.ActingPlayer.Entity);
				if(!inventory.CanStillCraftCurrent())
				{
					break;
				}
				itemstack = prevStack;
			}
			if(movedtotal > 0)
			{
				sinkSlot.OnItemSlotModified(sinkSlot.Itemstack);
				OnItemSlotModified(sinkSlot.Itemstack);
			}
		}
	}
}
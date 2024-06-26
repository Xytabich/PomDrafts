using PowerOfMind.Drafts.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;

namespace PowerOfMind.Drafts.Blocks.Entities
{
	public class InventoryDraftingTable : InventoryGeneric
	{
		public readonly List<SchematicRecipe> Recipes = new();

		public override int Count => base.Count + 1;
		public override ItemSlot? this[int slotId]
		{
			get
			{
				if(slotId < 0 || slotId >= Count)
				{
					return null;
				}
				if(slotId == base.Count)
				{
					return outputSlot;
				}
				return slots[slotId];
			}
			set
			{
				if(slotId < 0 || slotId >= base.Count)
				{
					throw new ArgumentOutOfRangeException(nameof(slotId));
				}
				if(value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}
				slots[slotId] = value;
			}
		}

		private SchematicRecipe? matchingRecipe = null;
		private bool isCrafting = false;

		private readonly ItemSlotCraftResult outputSlot;

		public InventoryDraftingTable(int quantitySlots, string? invId, ICoreAPI? api)
			: base(quantitySlots, invId, api, null)
		{
			outputSlot = new ItemSlotCraftResult(this);
			InvNetworkUtil = new CraftingInventoryNetworkUtil(this, api);
		}

		public override void LateInitialize(string inventoryID, ICoreAPI api)
		{
			base.LateInitialize(inventoryID, api);
			((CraftingInventoryNetworkUtil)InvNetworkUtil).Api = api;
		}

		public void FindMatchingRecipe()
		{
			matchingRecipe = null;
			outputSlot.Itemstack = null;
			foreach(var recipe in Recipes)
			{
				if(recipe.Matches(Api.World, slots))
				{
					FoundMatch(recipe);
					return;
				}
			}
			dirtySlots.Add(slots.Length);
		}

		internal void BeginCraft()
		{
			isCrafting = true;
		}

		internal void EndCraft()
		{
			isCrafting = false;
			FindMatchingRecipe();
		}

		public bool CanStillCraftCurrent()
		{
			if(matchingRecipe != null)
			{
				return matchingRecipe.Matches(Api.World, slots);
			}
			return false;
		}

		public override float GetTransitionSpeedMul(EnumTransitionType transType, ItemStack stack)
		{
			if(stack == outputSlot.Itemstack)
			{
				return 0f;
			}
			return base.GetTransitionSpeedMul(transType, stack);
		}

		public override object? ActivateSlot(int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
		{
			object packet;
			if(slotId == base.Count)
			{
				if(op.ActingPlayer?.Entity == null) return null;

				BeginCraft();
				packet = base.ActivateSlot(slotId, sourceSlot, ref op);
				if(!outputSlot.Empty && op.ShiftDown)
				{
					if(Api.Side == EnumAppSide.Client)
					{
						outputSlot.Itemstack = null;
					}
					else
					{
						op.ActingPlayer?.InventoryManager.DropItem(outputSlot, true);
					}
				}
				EndCraft();
			}
			else
			{
				packet = base.ActivateSlot(slotId, sourceSlot, ref op);
			}
			return packet;
		}

		public override void OnItemSlotModified(ItemSlot slot)
		{
			if(!isCrafting && !(slot is ItemSlotCraftingOutput))
			{
				FindMatchingRecipe();
			}
		}

		public override bool TryMoveItemStack(IPlayer player, string[] invIds, int[] slotIds, ref ItemStackMoveOperation op)
		{
			bool result = base.TryMoveItemStack(player, invIds, slotIds, ref op);
			if(result)
			{
				FindMatchingRecipe();
			}
			return result;
		}

		private void FoundMatch(SchematicRecipe recipe)
		{
			matchingRecipe = recipe;
			outputSlot.Itemstack = recipe.Output.ResolvedItemstack.Clone();
			dirtySlots.Add(base.Count);
		}

		internal void ConsumeIngredients(ItemSlot outputSlot, Entity byEntity)
		{
			if(matchingRecipe != null && outputSlot.Itemstack != null)
			{
				matchingRecipe.ConsumeInput(Api.World, byEntity, slots);
				for(int i = Count - 1; i >= 0; i--)
				{
					dirtySlots.Add(i);
				}
			}
		}

		public override WeightedSlot GetBestSuitedSlot(ItemSlot sourceSlot, ItemStackMoveOperation op, List<ItemSlot>? skipSlots = null)
		{
			return new WeightedSlot {
				slot = null,
				weight = 0f
			};
		}

		public override void FromTreeAttributes(ITreeAttribute tree)
		{
			var attrSlots = SlotsFromTreeAttributes(tree);
			if(attrSlots?.Length == slots.Length)
			{
				slots = attrSlots;
			}
		}

		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			SlotsToTreeAttributes(slots, tree);
			ResolveBlocksOrItems();
		}

		private class CraftingInventoryNetworkUtil : InventoryNetworkUtil
		{
			public CraftingInventoryNetworkUtil(InventoryBase inv, ICoreAPI? api)
				: base(inv, api)
			{
			}

			public override void UpdateFromPacket(IWorldAccessor resolver, Packet_InventoryContents packet)
			{
				for(int i = 0; i < packet.ItemstacksCount; i++)
				{
					inv[i].Itemstack = ItemStackFromPacket(resolver, packet.Itemstacks[i]);
				}
				((InventoryDraftingTable)inv).FindMatchingRecipe();
			}
		}
	}
}
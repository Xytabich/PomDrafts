using System.Diagnostics.CodeAnalysis;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace PowerOfMind.Drafts.Items.Behaviors
{
	public class ItemFolderInteractBehavior : CollectibleBehavior
	{
		private ICoreAPI? api = null;

		public ItemFolderInteractBehavior(CollectibleObject collObj) : base(collObj)
		{
		}

		public override void OnLoaded(ICoreAPI api)
		{
			this.api = api;
			base.OnLoaded(api);
		}

		public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				draftItem.Collectible.OnHeldAttackStart(new ProxySlot(slot, draftItem), byEntity, blockSel, entitySel, ref handHandling);
				return;
			}
			handling = EnumHandling.PassThrough;
		}

		public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.OnHeldAttackCancel(secondsPassed, new ProxySlot(slot, draftItem), byEntity, blockSelection, entitySel, cancelReason);
			}
			handling = EnumHandling.PassThrough;
			return false;
		}

		public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.OnHeldAttackStep(secondsPassed, new ProxySlot(slot, draftItem), byEntity, blockSelection, entitySel);
			}
			handling = EnumHandling.PassThrough;
			return false;
		}

		public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				draftItem.Collectible.OnHeldAttackStop(secondsPassed, new ProxySlot(slot, draftItem), byEntity, blockSelection, entitySel);
				return;
			}
			handling = EnumHandling.PassThrough;
		}

		public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				draftItem.Collectible.OnHeldInteractStart(new ProxySlot(slot, draftItem), byEntity, blockSel, entitySel, firstEvent, ref handHandling);
				return;
			}
			handling = EnumHandling.PassThrough;
		}

		public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.OnHeldInteractStep(secondsUsed, new ProxySlot(slot, draftItem), byEntity, blockSel, entitySel);
			}
			handling = EnumHandling.PassThrough;
			return false;
		}

		public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				draftItem.Collectible.OnHeldInteractStop(secondsUsed, new ProxySlot(slot, draftItem), byEntity, blockSel, entitySel);
				return;
			}
			handling = EnumHandling.PassThrough;
		}

		public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.OnHeldInteractCancel(secondsUsed, new ProxySlot(slot, draftItem), byEntity, blockSel, entitySel, cancelReason);
			}
			return true;
		}

		public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
		{
			if(TryGetItem(itemstack, out var draftItem))
			{
				draftItem.Collectible.OnBeforeRender(capi, itemstack, target, ref renderinfo);
			}
		}

		public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot slot, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.GetHeldInteractionHelp(new ProxySlot(slot, draftItem));
			}
			handling = EnumHandling.PassThrough;
			return Array.Empty<WorldInteraction>();
		}

		public override SkillItem[]? GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				return draftItem.Collectible.GetToolModes(new ProxySlot(slot, draftItem), forPlayer, blockSel);
			}
			return null;
		}

		public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				return draftItem.Collectible.GetToolMode(new ProxySlot(slot, draftItem), byPlayer, blockSelection);
			}
			return 0;
		}

		public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				draftItem.Collectible.SetToolMode(new ProxySlot(slot, draftItem), byPlayer, blockSelection, toolMode);
			}
		}

		public override void GetHeldItemInfo(ItemSlot slot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				draftItem.Collectible.GetHeldItemInfo(new ProxySlot(slot, draftItem), dsc, world, withDebugInfo);
			}
		}

		public override void GetHeldItemName(StringBuilder sb, ItemStack itemStack)
		{
			if(TryGetItem(itemStack, out var draftItem))
			{
				sb.AppendLine(draftItem.Collectible.GetHeldItemName(draftItem));
			}
		}

		public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot slot, BlockSelection blockSel, float dropQuantityMultiplier, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.OnBlockBrokenWith(world, byEntity, new ProxySlot(slot, draftItem), blockSel, dropQuantityMultiplier);
			}
			handling = EnumHandling.PassThrough;
			return true;
		}

		public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot slot, float remainingResistance, float dt, int counter, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.OnBlockBreaking(player, blockSel, new ProxySlot(slot, draftItem), remainingResistance, dt, counter);
			}
			handling = EnumHandling.PassThrough;
			return remainingResistance;
		}

		public override string? GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.GetHeldTpHitAnimation(new ProxySlot(slot, draftItem), byEntity);
			}
			return null;
		}

		public override string? GetHeldReadyAnimation(ItemSlot slot, Entity forEntity, EnumHand hand, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.GetHeldReadyAnimation(new ProxySlot(slot, draftItem), forEntity, hand);
			}
			return null;
		}

		public override string? GetHeldTpIdleAnimation(ItemSlot slot, Entity forEntity, EnumHand hand, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.GetHeldTpIdleAnimation(new ProxySlot(slot, draftItem), forEntity, hand);
			}
			return null;
		}

		public override string? GetHeldTpUseAnimation(ItemSlot slot, Entity forEntity, ref EnumHandling handling)
		{
			if(TryGetItem(slot.Itemstack, out var draftItem))
			{
				handling = EnumHandling.PreventSubsequent;

				return draftItem.Collectible.GetHeldTpUseAnimation(new ProxySlot(slot, draftItem), forEntity);
			}
			return null;
		}

		private bool TryGetItem(ItemStack folderItem, [MaybeNullWhen(false)] out ItemStack draftItem)
		{
			if(api != null)
			{
				var draft = folderItem.Attributes[ItemFolderBehavior.ACTIVE_DRAFT_ATTRIB];
				if(draft != null && draft is StringAttribute attrib && !string.IsNullOrWhiteSpace(attrib.value))
				{
					var code = new AssetLocation(attrib.value);
					var descriptor = api.ModLoader.GetModSystem<DraftsModSystem>().GetDescriptor(code);
					if(descriptor != null && descriptor.IsInteractableDraft(code))
					{
						draftItem = descriptor.CreateDummyStack(code);
						CopyAttribs(folderItem.TempAttributes[ItemFolderBehavior.ACTIVE_DRAFT_DATA_ATTRIB] as ITreeAttribute, draftItem.TempAttributes);
						CopyAttribs(folderItem.Attributes[ItemFolderBehavior.ACTIVE_DRAFT_DATA_ATTRIB] as ITreeAttribute, draftItem.Attributes);
						return true;
					}
				}
			}

			draftItem = null;
			return false;
		}

		private static void CopyAttribs(ITreeAttribute? source, ITreeAttribute target)
		{
			if(source == null) return;
			foreach(var pair in source)
			{
				target[pair.Key] = pair.Value;
			}
		}

		private class ProxySlot : DummySlot
		{
			private readonly ItemSlot actualSlot;

			public ProxySlot(ItemSlot actualSlot, ItemStack stack) : base(stack)
			{
				this.actualSlot = actualSlot;
			}

			public override void MarkDirty()
			{
				base.MarkDirty();

				var outStack = actualSlot.Itemstack;
				if(itemstack == null)
				{
					//TODO: remove entire draft instead, for example if there will be some attributes that was returned by the descriptor
					outStack.TempAttributes.RemoveAttribute(ItemFolderBehavior.ACTIVE_DRAFT_DATA_ATTRIB);
					outStack.Attributes.RemoveAttribute(ItemFolderBehavior.ACTIVE_DRAFT_DATA_ATTRIB);
				}
				else
				{
					if(itemstack.TempAttributes.Count == 0)
					{
						outStack.TempAttributes.RemoveAttribute(ItemFolderBehavior.ACTIVE_DRAFT_DATA_ATTRIB);
					}
					else
					{
						CopyAttribs(itemstack.TempAttributes, outStack.TempAttributes.GetOrAddTreeAttribute(ItemFolderBehavior.ACTIVE_DRAFT_DATA_ATTRIB));
					}
					if(itemstack.Attributes.Count == 0)
					{
						outStack.Attributes.RemoveAttribute(ItemFolderBehavior.ACTIVE_DRAFT_DATA_ATTRIB);
					}
					else
					{
						CopyAttribs(itemstack.Attributes, outStack.Attributes.GetOrAddTreeAttribute(ItemFolderBehavior.ACTIVE_DRAFT_DATA_ATTRIB));
					}
				}

				actualSlot.MarkDirty();
			}
		}
	}
}
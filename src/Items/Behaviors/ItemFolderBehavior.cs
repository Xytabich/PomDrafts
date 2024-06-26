using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Items.Behaviors
{
	public class ItemFolderBehavior : CollectibleBehavior
	{
		public const string ACTIVE_DRAFT_ATTRIB = "pom:draft";
		public const string ACTIVE_DRAFT_DATA_ATTRIB = "pom:draft_data";

		public ItemFolderBehavior(CollectibleObject collObj) : base(collObj)
		{
		}

		public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
		{
			//TODO: show hotkey
			return base.GetHeldInteractionHelp(inSlot, ref handling);
		}

		public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
		{
			if(!slot.Itemstack.Attributes.HasAttribute(ACTIVE_DRAFT_ATTRIB))
			{
				//TODO: show ui

				handHandling = EnumHandHandling.PreventDefault;
				handling = EnumHandling.PreventDefault;
				return;
			}

			base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
		}

		public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
		{
			if(!slot.Itemstack.Attributes.HasAttribute(ACTIVE_DRAFT_ATTRIB))
			{
				handling = EnumHandling.PreventDefault;
				return false;
			}
			return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
		}
	}
}
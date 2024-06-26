using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Items.TriggerBehaviors
{
	public class ItemInteractTrigger : ItemTriggerBase
	{
		public ItemInteractTrigger(CollectibleObject collObj) : base(collObj)
		{
		}

		public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
		{
			if(firstEvent)
			{
				if(TryTrigger(byEntity, blockSel, entitySel) == Common.EnumLearnResult.Learned && api.Side == EnumAppSide.Client)
				{
					if(handHandling == 0)
					{
						handHandling = EnumHandHandling.Handled;
					}
				}
			}
			base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
		}
	}
}
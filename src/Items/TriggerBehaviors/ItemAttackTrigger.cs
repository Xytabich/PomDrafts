using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Items.TriggerBehaviors
{
	public class ItemAttackTrigger : ItemTriggerBase
	{
		public ItemAttackTrigger(CollectibleObject collObj) : base(collObj)
		{
		}

		public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
		{
			if(TryTrigger(byEntity, blockSel, entitySel) == Common.EnumLearnResult.Learned && api.Side == EnumAppSide.Client)
			{
				if(handHandling == 0)
				{
					handHandling = EnumHandHandling.Handled;
				}
			}
			base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
		}
	}
}
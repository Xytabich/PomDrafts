﻿using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Drafts.Entities.TriggerBehaviors
{
	public class EntityInteractTrigger : EntityTriggerBase
	{
		public EntityInteractTrigger(Entity entity) : base(entity)
		{
		}

		public override string PropertyName()
		{
			return "pomdrafts:trigger-entity-interact";
		}

		public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
		{
			if(mode == EnumInteractMode.Interact) TryTrigger(byEntity, itemslot?.Itemstack);
			base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
		}
	}
}
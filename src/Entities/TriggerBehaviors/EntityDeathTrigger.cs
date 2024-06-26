using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace PowerOfMind.Drafts.Entities.TriggerBehaviors
{
	public class EntityDeathTrigger : EntityTriggerBase
	{
		public EntityDeathTrigger(Entity entity) : base(entity)
		{
		}

		public override string PropertyName()
		{
			return "pomdrafts:trigger-entity-death";
		}

		public override void OnEntityDeath(DamageSource source)
		{
			TryTrigger(source.SourceEntity, (source.SourceEntity as EntityPlayer)?.ActiveHandItemSlot?.Itemstack);
		}
	}
}
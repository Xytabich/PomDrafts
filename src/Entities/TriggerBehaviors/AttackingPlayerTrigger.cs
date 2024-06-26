using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace PowerOfMind.Drafts.Entities.TriggerBehaviors
{
	public class AttackingPlayerTrigger : EntityTriggerBase
	{
		public AttackingPlayerTrigger(Entity entity) : base(entity)
		{
		}

		public override string PropertyName()
		{
			return "pomdrafts:trigger-player-attack";
		}

		public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
		{
			TryTrigger(targetEntity, null);
			base.DidAttack(source, targetEntity, ref handled);
		}
	}
}
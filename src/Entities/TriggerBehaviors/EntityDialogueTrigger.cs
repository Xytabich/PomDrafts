using PowerOfMind.Drafts.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace PowerOfMind.Drafts.Entities.TriggerBehaviors
{
	public class EntityDialogueTrigger : EntityBehavior
	{
		protected DraftsModSystem draftsSystem = default!;

		public EntityDialogueTrigger(Entity entity) : base(entity)
		{
		}

		public override string PropertyName()
		{
			return "pomdrafts:trigger-dialogue";
		}

		public override void Initialize(EntityProperties properties, JsonObject attributes)
		{
			base.Initialize(properties, attributes);

			draftsSystem = entity.Api.ModLoader.GetModSystem<DraftsModSystem>();

			var beh = entity.GetBehavior<EntityBehaviorConversable>();
			if(beh != null)
			{
				beh.onControllerCreated += InitController;
			}
		}

		private void InitController(DialogueController controller)
		{
			controller.DialogTriggers += OnDialogTrigger;
		}

		private int OnDialogTrigger(EntityAgent triggeringEntity, string value, JsonObject data)
		{
			if(value == "pomdrafts:givedraft")
			{
				TryTrigger(triggeringEntity, data["draft"].AsObject<AssetLocation>());
			}
			return -1;
		}

		private void TryTrigger(Entity byEntity, AssetLocation? draftCode)
		{
			if(draftCode == null) return;
			if(byEntity is not EntityPlayer player) return;

			var result = draftsSystem.TryLearnDraft(player.Player, draftCode);
			if(result == EnumLearnResult.Failed)
			{
				entity.Api.Logger.Warning("[pomdrafts] Failed to learn draft {0} using {1}. Probably such a draft does not exist.", draftCode, entity.Code);
			}
		}
	}
}
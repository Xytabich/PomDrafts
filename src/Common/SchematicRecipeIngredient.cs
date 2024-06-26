using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace PowerOfMind.Drafts.Common
{
	public class SchematicRecipeIngredient : CraftingRecipeIngredient
	{
		[JsonIgnore]
		public bool IsLiquid = false;

		[JsonProperty]
		private float litres = 0;

		public new bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
		{
			if(base.Resolve(world, sourceForErrorLogging))
			{
				var liquidProps = BlockLiquidContainerBase.GetContainableProps(ResolvedItemstack);
				if(liquidProps != null)
				{
					if(litres <= float.Epsilon)
					{
						if(Quantity > 0)
						{
							world.Logger.Warning("[pomdrafts] Schematic recipe {0}, ingredient {1} does not define a litres attribute but a quantity, will assume quantity=litres for backwards compatibility.", sourceForErrorLogging, Code);
							litres = Quantity;
						}
						else
						{
							litres = 1f;
						}
					}
					Quantity = (int)(liquidProps.ItemsPerLitre * litres);
					ResolvedItemstack.StackSize = Quantity;
					IsLiquid = true;
				}
				return true;
			}
			return false;
		}

		public new SchematicRecipeIngredient Clone()
		{
			var ingredient = CloneTo<SchematicRecipeIngredient>();
			ingredient.litres = litres;
			ingredient.IsLiquid = IsLiquid;
			return ingredient;
		}

		public override void FromBytes(BinaryReader reader, IWorldAccessor resolver)
		{
			base.FromBytes(reader, resolver);
			IsLiquid = ResolvedItemstack?.ItemAttributes?.KeyExists("waterTightContainerProps") ?? false;
		}

		public void InitFromItemstack(ItemStack itemStack)
		{
			ResolvedItemstack = itemStack.Clone();
			Type = itemStack.Class;
			Quantity = itemStack.StackSize;
			Code = itemStack.Collectible.Code;
			ToolDurabilityCost = 0;
			foreach(var key in GlobalConstants.IgnoredStackAttributes)
			{
				if(ResolvedItemstack.Attributes.HasAttribute(key))
				{
					ResolvedItemstack.Attributes.RemoveAttribute(key);
				}
			}
			if(ResolvedItemstack.Attributes.Count != 0)
			{
				Attributes = JsonObject.FromJson(ResolvedItemstack.Attributes.ToJsonToken());
			}
			var liquidProps = BlockLiquidContainerBase.GetContainableProps(ResolvedItemstack);
			if(liquidProps != null)
			{
				litres = Quantity / liquidProps.ItemsPerLitre;
				Quantity = 0;
			}
		}
	}
}
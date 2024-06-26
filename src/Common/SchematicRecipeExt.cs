using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace PowerOfMind.Drafts.Common
{
	public static class SchematicRecipeExt
	{
		public static bool Matches(this SchematicRecipe recipe, IWorldAccessor world, ItemSlot[] slots)
		{
			var items = new List<(SchematicRecipeIngredient ingredient, int quantity)>();//TODO: cache collections
			var tools = new List<(SchematicRecipeIngredient ingredient, int quantity)>();
			var liquids = new List<(SchematicRecipeIngredient ingredient, int quantity)>();
			CollectIngredients(recipe, items, tools, liquids);

			foreach(var slot in slots)
			{
				var stack = slot.Itemstack;
				if(stack != null)
				{
					int quantity = stack.StackSize;
					for(int i = items.Count - 1; i >= 0; i--)
					{
						if(items[i].ingredient.SatisfiesAsIngredient(stack, false))
						{
							int consumed = SatisfyIngredient(items, quantity, i);
							quantity -= consumed;
							if(quantity == 0) break;
						}
					}
					if(quantity == 0) continue;

					quantity = -1;
					for(int i = tools.Count - 1; i >= 0; i--)
					{
						if(tools[i].ingredient.SatisfiesAsIngredient(stack, false))
						{
							if(quantity < 0)
							{
								quantity = stack.Collectible.GetRemainingDurability(stack);
							}

							int consumed = SatisfyIngredient(tools, quantity, i);
							quantity -= consumed;
							if(quantity == 0) break;
						}
					}

					if(quantity == 0) continue;
					if(liquids.Count != 0 && stack.Collectible is ILiquidSink liquidSink)
					{
						var liquidStack = liquidSink.GetContent(stack);
						if(liquidStack == null) continue;
						quantity = liquidStack.StackSize;

						for(int i = liquids.Count - 1; i >= 0; i--)
						{
							if(liquids[i].ingredient.SatisfiesAsIngredient(liquidStack, false))
							{
								int consumed = SatisfyIngredient(liquids, quantity, i);
								quantity -= consumed;
								if(quantity == 0) break;
							}
						}
					}
				}
			}

			return items.Count == 0 && tools.Count == 0 && liquids.Count == 0;
		}

		public static void ConsumeInput(this SchematicRecipe recipe, IWorldAccessor world, Entity byEntity, ItemSlot[] slots)
		{
			var items = new List<(SchematicRecipeIngredient ingredient, int quantity)>();//TODO: cache collections
			var tools = new List<(SchematicRecipeIngredient ingredient, int quantity)>();
			var liquids = new List<(SchematicRecipeIngredient ingredient, int quantity)>();
			CollectIngredients(recipe, items, tools, liquids);

			foreach(var slot in slots)
			{
				var stack = slot.Itemstack;
				if(stack != null)
				{
					int totalQuantity;
					int quantity = totalQuantity = stack.StackSize;
					for(int i = items.Count - 1; i >= 0; i--)
					{
						if(items[i].ingredient.SatisfiesAsIngredient(stack, false))
						{
							int consumed = SatisfyIngredient(items, quantity, i);
							quantity -= consumed;
							if(quantity == 0) break;
						}
					}
					if(quantity != totalQuantity)
					{
						if(quantity == 0)
						{
							slot.Itemstack = null;
						}
						else
						{
							stack.StackSize = quantity;
						}
					}
					if(quantity == 0) continue;

					quantity = totalQuantity = -1;
					for(int i = tools.Count - 1; i >= 0; i--)
					{
						if(tools[i].ingredient.SatisfiesAsIngredient(stack, false))
						{
							if(quantity < 0)
							{
								quantity = totalQuantity = stack.Collectible.GetRemainingDurability(stack);
							}

							int consumed = SatisfyIngredient(tools, quantity, i);
							quantity -= consumed;
							if(quantity == 0) break;
						}
					}
					if(quantity != totalQuantity)
					{
						stack.Collectible.DamageItem(world, byEntity, slot, totalQuantity - quantity);
					}
					if(quantity == 0) continue;

					if(liquids.Count != 0 && stack.Collectible is ILiquidSink liquidSink)
					{
						var liquidStack = liquidSink.GetContent(stack);
						if(liquidStack == null) continue;
						quantity = totalQuantity = liquidStack.StackSize;

						for(int i = liquids.Count - 1; i >= 0; i--)
						{
							if(liquids[i].ingredient.SatisfiesAsIngredient(liquidStack, false))
							{
								int consumed = SatisfyIngredient(liquids, quantity, i);
								quantity -= consumed;
								if(quantity == 0) break;
							}
						}
						if(quantity != totalQuantity)
						{
							if(quantity == 0)
							{
								liquidStack = null;
							}
							else
							{
								liquidStack.StackSize = quantity;
							}
							liquidSink.SetContent(stack, liquidStack);
						}
					}
				}
			}
		}

		private static int SatisfyIngredient(List<(SchematicRecipeIngredient ingr, int quantity)> resources, int amount, int index)
		{
			int quantity = resources[index].quantity;
			int consumed = Math.Min(amount, quantity);
			quantity -= consumed;

			if(quantity == 0)
			{
				resources.RemoveAt(index);
			}
			else
			{
				resources[index] = (resources[index].ingr, quantity);
			}

			return consumed;
		}

		private static void CollectIngredients(SchematicRecipe recipe,
			List<(SchematicRecipeIngredient ingredient, int quantity)> outItems,
			List<(SchematicRecipeIngredient ingredient, int quantity)> outTools,
			List<(SchematicRecipeIngredient ingredient, int quantity)> outLiquids)
		{
			foreach(var ingr in recipe.Ingredients)
			{
				if(ingr.IsTool)
				{
					outTools.Add((ingr, ingr.ToolDurabilityCost));
				}
				else if(ingr.IsLiquid)
				{
					outLiquids.Add((ingr, ingr.Quantity));
				}
				else
				{
					outItems.Add((ingr, ingr.Quantity));
				}
			}
		}
	}
}
using Cairo;
using PowerOfMind.Drafts.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PowerOfMind.Drafts.GUI
{
	public class GuiElementSlideshowDraftRecipe : GuiElement
	{
		private readonly ItemSlot dummySlot = new DummySlot();
		private readonly IReadOnlyList<SchematicRecipe> recipes;
		private readonly ItemInfoRenderer itemInfoRenderer;
		private readonly double size;

		private float secondsVisible = 1f;
		private int curItemIndex;

		private bool isHovered = false;

		public GuiElementSlideshowDraftRecipe(ICoreClientAPI capi, ElementBounds bounds, IReadOnlyList<SchematicRecipe> recipes, ItemInfoRenderer itemInfoRenderer)
			: base(capi, bounds)
		{
			this.recipes = recipes;
			this.itemInfoRenderer = itemInfoRenderer;

			size = GuiElement.scaled(GuiElementPassiveItemSlot.unscaledSlotSize);
		}

		public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
		{
			if(isHovered)
			{
				secondsVisible = 1f;
				int len = recipes.Count;
				curItemIndex = (((curItemIndex + args.value) % len) + len) % len;
				args.SetHandled();
			}
		}

		public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
		{
			isHovered = Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
		}

		public override void ComposeElements(Context ctx, ImageSurface surface)
		{
			Bounds.CalcWorldBounds();

			var renderX = Bounds.drawX;
			var renderY = Bounds.drawY;

			var marginAndSize = size + GuiElement.scaled(GuiElementItemSlotGridBase.unscaledSlotPadding);

			ctx.SetSourceRGBA(1.0, 1.0, 1.0, 0.2);
			for(int x = 0; x < 4; x++)
			{
				for(int y = 0; y < 4; y++)
				{
					ctx.Rectangle(
						renderX + x * marginAndSize,
						renderY + y * marginAndSize,
						size,
						size
					);
					ctx.Fill();
				}
			}

			ctx.Rectangle(
				renderX + Bounds.InnerWidth - marginAndSize,
				renderY + marginAndSize * 1.5,
				size,
				size
			);
			ctx.Fill();
		}

		public override void RenderInteractiveElements(float deltaTime)
		{
			if((uint)curItemIndex >= (uint)recipes.Count)
			{
				curItemIndex = 0;
				return;
			}

			var renderX = Bounds.renderX;
			var renderY = Bounds.renderY;

			var recipe = recipes[curItemIndex];
			if(!isHovered && (secondsVisible -= deltaTime) <= 0f)
			{
				secondsVisible = 1f;
				curItemIndex = (curItemIndex + 1) % recipes.Count;
			}

			var marginAndSize = size + GuiElement.scaled(GuiElementItemSlotGridBase.unscaledSlotPadding);

			double rx, ry;
			int index = 0;
			for(int x = 0; x < 4; x++)
			{
				for(int y = 0; y < 4; y++, index++)
				{
					if(index == recipe.Ingredients.Length) break;

					var ingred = recipe.Ingredients[index];
					if(ingred == null) continue;

					rx = renderX + x * marginAndSize;
					ry = renderY + y * marginAndSize;

					dummySlot.BackgroundIcon = index.ToString();
					RenderItem(ingred.ResolvedItemstack, deltaTime, rx, ry);
				}
			}

			dummySlot.BackgroundIcon = "16";
			rx = renderX + Bounds.InnerWidth - marginAndSize;
			ry = renderY + marginAndSize * 1.5;
			RenderItem(recipe.Output.ResolvedItemstack, deltaTime, rx, ry);
		}

		private void RenderItem(ItemStack itemStack, float deltaTime, double rx, double ry)
		{
			var scale = 1.0 / RuntimeEnv.GUIScale;
			var scissorBounds = ElementBounds.Fixed(rx * scale, ry * scale, size * scale, size * scale).WithEmptyParent();
			scissorBounds.CalcWorldBounds();

			api.Render.PushScissor(scissorBounds, stacking: true);
			dummySlot.Itemstack = itemStack.Clone();
			api.Render.RenderItemstackToGui(
				dummySlot,
				rx + size * 0.5,
				ry + size * 0.5,
				100.0,
				(float)size * 0.58f,
				-1
			);
			api.Render.PopScissor();

			var dx = api.Input.MouseX - rx + 1.0;
			var dy = api.Input.MouseY - ry + 2.0;
			if(dx >= 0.0 && dx < size && dy >= 0.0 && dy < size)
			{
				itemInfoRenderer.RenderItemstackTooltip(itemStack, rx + dx, ry + dy, deltaTime);
			}
			dummySlot.BackgroundIcon = null;
		}
	}
}
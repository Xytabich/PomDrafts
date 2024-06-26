using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace PowerOfMind.Drafts.GUI
{
	public class GuiElementDraftButton : IFlatListItemInteractable
	{
		public bool Visible => true;

		private LoadedTexture? textTexture = null;
		private ElementBounds scissorBounds = default!;

		private readonly AssetLocation code;
		private readonly ItemStack stack;
		private readonly DummySlot dummySlot;
		private readonly Action<(float x, float y), AssetLocation> beginDrag;
		private readonly Action<ItemStack, (float x, float y), float> renderDraftInfo;

		private (double x, double y, double width, double height) rect;

		public GuiElementDraftButton(AssetLocation code, ItemStack stack, Action<(float x, float y), AssetLocation> beginDrag,
			Action<ItemStack, (float x, float y), float> renderDraftInfo)
		{
			this.code = code;
			this.stack = stack;
			this.beginDrag = beginDrag;
			this.renderDraftInfo = renderDraftInfo;

			dummySlot = new DummySlot(stack);
		}

		public void OnMouseDown(ICoreClientAPI api, MouseEvent args)
		{
			if(args.Button == EnumMouseButton.Left && PointInRect(args.X, args.Y))
			{
				beginDrag((args.X, args.Y), code);
				args.Handled = true;
			}
		}

		public void OnMouseMove(ICoreClientAPI api, MouseEvent args)
		{
		}

		public void OnMouseUp(ICoreClientAPI api, MouseEvent args)
		{
		}

		public void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
		{
			rect = (x, y, cellWidth, cellHeight);
			y += 5.0 + GuiElement.scaled(3.0);//...
			float size = (float)GuiElement.scaled(25.0);
			float pad = (float)GuiElement.scaled(10.0);
			if(scissorBounds == null)
			{
				scissorBounds = ElementBounds.FixedSize(50.0, 50.0);
				scissorBounds.ParentBounds = capi.Gui.WindowBounds;
			}
			scissorBounds.fixedX = ((double)pad + x - (double)(size / 2f)) / (double)RuntimeEnv.GUIScale;
			scissorBounds.fixedY = (y - (double)(size / 2f)) / (double)RuntimeEnv.GUIScale;
			scissorBounds.CalcWorldBounds();
			if(!(scissorBounds.InnerWidth <= 0.0) && !(scissorBounds.InnerHeight <= 0.0))
			{
				if(textTexture == null)
				{
					textTexture = new TextTextureUtil(capi).GenTextTexture(stack.GetName(), CairoFont.WhiteSmallText());
				}

				var rapi = capi.Render;
				rapi.PushScissor(scissorBounds, stacking: true);
				rapi.RenderItemstackToGui(dummySlot, x + (double)pad + (double)(size / 2f), y + (double)(size / 2f), 100.0, size, -1, shading: true, rotate: false, showStackSize: false);
				rapi.PopScissor();
				rapi.Render2DTexturePremultipliedAlpha(
					textTexture!.TextureId,
					x + (double)size + GuiElement.scaled(25.0),
					y + (double)size / 4 - GuiElement.scaled(3.0),
					textTexture.Width,
					textTexture.Height
				);

				if(PointInRect(capi.Input.MouseX, capi.Input.MouseY))
				{
					renderDraftInfo(stack, (capi.Input.MouseX, capi.Input.MouseY), dt);
				}
			}
		}

		public void Dispose()
		{
			textTexture?.Dispose();
			textTexture = null;
		}

		private bool PointInRect(float x, float y)
		{
			return x >= rect.x && x <= rect.x + rect.width && y >= rect.y && y <= rect.y + rect.height;
		}
	}
}
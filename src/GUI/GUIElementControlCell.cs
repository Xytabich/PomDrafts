using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.GUI
{
	public class GUIElementControlCell : GuiElement, IGuiElementCell
	{
		private double unScaledTextCellHeight = 25.0;
		private double unScaledButtonCellHeight = 35.0;

		public bool Visible => true;

		private GuiElementRichtext labelTextElem;
		private GuiElementRichtext? contentTextElem;
		private GuiElementTextButton? buttonElem;

		private bool compose = true;

		ElementBounds IGuiElementCell.Bounds => Bounds;

		public GUIElementControlCell(ICoreClientAPI capi, ElementBounds bounds, string label, string content, ActionConsumable? onClick)
			: base(capi, bounds)
		{
			var height = onClick == null ? unScaledTextCellHeight : unScaledButtonCellHeight;

			var font = CairoFont.WhiteDetailText();
			var offY = (height - font.UnscaledFontsize) / 2.0;
			var labelTextBounds = ElementBounds.Fixed(0.0, offY, 270.0, height).WithParent(Bounds);
			labelTextElem = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, label, font), labelTextBounds);
			if(onClick == null)
			{
				var contentTextBounds = ElementBounds.Fixed(0.0, offY, 100.0, height).WithParent(Bounds).FixedRightOf(Bounds, -100);
				contentTextElem = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, content, font), contentTextBounds);
			}
			else
			{
				var contentTextBounds = ElementBounds.Fixed(0.0, 5, 100.0, height - 10).WithParent(Bounds).FixedRightOf(Bounds, -100);
				buttonElem = new GuiElementTextButton(capi, content, font, font, onClick, contentTextBounds, EnumButtonStyle.Small);
			}
		}

		public void OnRenderInteractiveElements(ICoreClientAPI api, float deltaTime)
		{
			if(compose)
			{
				compose = false;
				Recompose();
			}
			labelTextElem.RenderInteractiveElements(deltaTime);
			contentTextElem?.RenderInteractiveElements(deltaTime);
			if(buttonElem != null)
			{
				buttonElem.RenderInteractiveElements(deltaTime);
				MouseOverCursor = buttonElem.MouseOverCursor;
			}
		}

		public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
		{
			if(buttonElem == null) return;

			buttonElem.OnMouseMove(api, args);
		}

		public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
		{
			if(buttonElem == null) return;

			int x = api.Input.MouseX;
			int y = api.Input.MouseY;
			if(buttonElem.Bounds.PointInside(x, y))
			{
				buttonElem.OnMouseDownOnElement(api, args);
			}
		}

		public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
		{
			if(buttonElem == null) return;

			int x = api.Input.MouseX;
			int y = api.Input.MouseY;
			if(buttonElem.Bounds.PointInside(x, y))
			{
				buttonElem.OnMouseUpOnElement(api, args);
			}
		}

		public void UpdateCellHeight()
		{
			Bounds.CalcWorldBounds();
			labelTextElem.BeforeCalcBounds();
			contentTextElem?.BeforeCalcBounds();
			buttonElem?.BeforeCalcBounds();
			Bounds.fixedHeight = buttonElem == null ? unScaledTextCellHeight : unScaledButtonCellHeight;
		}

		public override void Dispose()
		{
			labelTextElem.Dispose();
			contentTextElem?.Dispose();
			buttonElem?.Dispose();
		}

		private void Recompose()
		{
			labelTextElem.Compose();
			contentTextElem?.Compose();

			if(buttonElem != null)
			{
				var surface = new ImageSurface(Format.Argb32, buttonElem.Bounds.OuterWidthInt, buttonElem.Bounds.OuterHeightInt);
				var context = genContext(surface);
				buttonElem.ComposeElements(context, surface);
				context.Dispose();
				surface.Dispose();
			}
		}
	}
}
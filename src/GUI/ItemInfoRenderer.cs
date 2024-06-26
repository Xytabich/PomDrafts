using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PowerOfMind.Drafts.GUI
{
	public class ItemInfoRenderer : IDisposable
	{
		private const int tooltipOffsetX = 10;
		private const int tooltipOffsetY = 40;

		private readonly ICoreClientAPI capi;

		private readonly ItemSlot renderedTooltipSlot;

		private readonly GuiElementItemstackInfo stackInfo;

		private readonly DummyInventory dummyInv;

		private readonly ElementBounds stackInfoBounds;

		private readonly ElementBounds parentBounds;

		private bool bottomOverlap = false;

		private bool rightOverlap = false;

		private long hoverDelayMs;

		public ItemInfoRenderer(ICoreClientAPI capi)
		{
			this.capi = capi;
			dummyInv = new DummyInventory(capi);
			dummyInv.OnAcquireTransitionSpeed = (EnumTransitionType transType, ItemStack stack, float mul) => 0f;
			renderedTooltipSlot = new DummySlot(null, dummyInv);
			stackInfoBounds = ElementBounds.FixedSize(EnumDialogArea.None, GuiElementItemstackInfo.BoxWidth, 0.0).WithFixedPadding(10.0).WithFixedPosition(20.0, 40.0);
			parentBounds = ElementBounds.Fixed(0.0, 0.0, 1.0, 1.0);
			parentBounds.WithParent(ElementBounds.Empty);
			stackInfoBounds.WithParent(parentBounds);
			stackInfo = new GuiElementItemstackInfo(capi, stackInfoBounds, OnRequireInfoText);
			stackInfo.SetSourceSlot(renderedTooltipSlot);
			stackInfo.ComposeElements(null, null);
			stackInfo.RecompCheckIgnoredStackAttributes = GlobalConstants.IgnoredStackAttributes;
		}

		protected virtual string OnRequireInfoText(ItemSlot slot)
		{
			return slot.GetStackDescription(capi.World, capi.Settings.Bool["extendedDebugInfo"]);
		}

		public void RenderItemstackTooltip(ItemStack itemStack, double renderX, double renderY, float dt)
		{
			bool isChanged = renderedTooltipSlot.Itemstack != itemStack;
			renderedTooltipSlot.Itemstack = itemStack;
			renderedTooltipSlot.BackgroundIcon = null;

			if(isChanged)
			{
				hoverDelayMs = capi.ElapsedMilliseconds + 100;
				stackInfo.SetSourceSlot(null);
				return;
			}

			if(capi.ElapsedMilliseconds < hoverDelayMs)
			{
				return;
			}

			parentBounds.fixedX = renderX / RuntimeEnv.GUIScale;
			parentBounds.fixedY = renderY / RuntimeEnv.GUIScale;
			parentBounds.CalcWorldBounds();

			stackInfo.SetSourceSlot(renderedTooltipSlot);
			bool rightOverlap = capi.Input.MouseX + stackInfoBounds.OuterWidth > (capi.Render.FrameWidth - 5);
			bool bottomOverlap = capi.Input.MouseY + stackInfoBounds.OuterHeight > (capi.Render.FrameHeight - 5);
			if(this.bottomOverlap != bottomOverlap || rightOverlap != this.rightOverlap)
			{
				stackInfoBounds.WithFixedAlignmentOffset(rightOverlap ? (-stackInfoBounds.OuterWidth / RuntimeEnv.GUIScale - tooltipOffsetX) : 0.0, bottomOverlap ? (-stackInfoBounds.OuterHeight / RuntimeEnv.GUIScale - tooltipOffsetY) : 0.0);
				stackInfoBounds.CalcWorldBounds();
				stackInfoBounds.fixedOffsetY += Math.Max(0.0, -stackInfoBounds.renderY);
				stackInfoBounds.CalcWorldBounds();
				this.bottomOverlap = bottomOverlap;
				this.rightOverlap = rightOverlap;
			}

			if(capi.Render.ScissorStack.Count > 0)
			{
				capi.Render.GlScissorFlag(enable: false);
				stackInfo.RenderInteractiveElements(dt);
				capi.Render.GlScissorFlag(enable: true);
			}
			else
			{
				stackInfo.RenderInteractiveElements(dt);
			}
		}

		public void Dispose()
		{
			stackInfo.Dispose();
		}
	}
}
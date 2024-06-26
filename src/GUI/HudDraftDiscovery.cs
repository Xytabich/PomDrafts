using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Drafts.GUI
{
	public class HudDraftDiscovery : HudElement//TODO: item icon
	{
		private const int MAX_QUEUE = 4;

		public override bool Focusable => false;
		public override double InputOrder => 1.0;
		public override string? ToggleKeyCombinationCode => null;

		private readonly DraftsModSystem draftsSystem;

		private readonly Queue<ItemStack> queue = new();

		private int durationVisibleMs = 2000;
		private long textActiveMs;

		private readonly Vec4f fadeCol = new Vec4f(1f, 1f, 1f, 0f);
		private GuiElementHoverText? hoverElement;

		public HudDraftDiscovery(ICoreClientAPI capi)
			: base(capi)
		{
			draftsSystem = capi.ModLoader.GetModSystem<DraftsModSystem>();
			draftsSystem.OnClientDraftsUpdate += OnDraftsUpdate;
			capi.Event.RegisterGameTickListener(OnGameTick, 20);
		}

		public override void OnOwnPlayerDataReceived()
		{
			ComposeGuis();
			TryOpen();
		}

		public void ComposeGuis()
		{
			var dialogBounds = new ElementBounds {
				Alignment = EnumDialogArea.CenterMiddle,
				BothSizing = ElementSizing.Fixed,
				fixedWidth = 600.0,
				fixedHeight = 0.0
			};
			var iteminfoBounds = ElementBounds.Fixed(0.0, -155.0, 700.0, 30.0);
			ClearComposers();
			var font = CairoFont.WhiteMediumText()
				.WithFont(GuiStyle.DecorativeFontName)
				.WithColor(GuiStyle.DiscoveryTextColor)
				.WithStroke(GuiStyle.DialogBorderColor, 2.0)
				.WithOrientation(EnumTextOrientation.Center);
			Composers["pomdrafts"] = capi.Gui.CreateCompo("pomdrafts", dialogBounds.FlatCopy())
				.PremultipliedAlpha(false)
				.BeginChildElements(dialogBounds)
				.AddTranspHoverText("", font, 700, iteminfoBounds, "discoverytext")
				.EndChildElements()
				.Compose();
			hoverElement = Composers["pomdrafts"].GetHoverText("discoverytext");
			hoverElement.SetFollowMouse(false);
			hoverElement.SetAutoWidth(false);
			hoverElement.SetAutoDisplay(false);
			hoverElement.fillBounds = true;
			hoverElement.RenderColor = fadeCol;
			hoverElement.ZPosition = 60f;
			hoverElement.RenderAsPremultipliedAlpha = false;
		}

		public override bool TryClose()
		{
			return false;
		}

		public override bool ShouldReceiveKeyboardEvents()
		{
			return false;
		}

		public override bool ShouldReceiveMouseEvents()
		{
			return false;
		}

		public override void OnRenderGUI(float deltaTime)
		{
			if(fadeCol.A > 0f)
			{
				base.OnRenderGUI(deltaTime);
			}
		}

		protected override void OnFocusChanged(bool on)
		{
		}

		private void OnDraftsUpdate(IReadOnlySet<AssetLocation> added, IReadOnlySet<AssetLocation> removed)
		{
			if(queue.Count >= MAX_QUEUE || added.Count == 0) return;

			var code = added.First();
			var descriptor = draftsSystem.GetDescriptor(code);
			if(descriptor != null)
			{
				queue.Enqueue(descriptor.CreateDummyStack(code));
			}
		}

		private void OnGameTick(float dt)
		{
			if(textActiveMs == 0L && queue.Count == 0)
			{
				return;
			}
			if(hoverElement == null)
			{
				return;
			}
			if(textActiveMs == 0L)
			{
				fadeCol.A = 0f;
				textActiveMs = capi.InWorldEllapsedMilliseconds;

				var item = queue.Dequeue();
				hoverElement.SetNewText(item.GetName());
				hoverElement.SetVisible(true);

				capi.Gui.PlaySound("effect/deepbell", volume: 0.5f);//TODO: sound
				return;
			}
			long visibleMsPassed = capi.InWorldEllapsedMilliseconds - textActiveMs;
			long visibleMsLeft = durationVisibleMs - visibleMsPassed;
			if(visibleMsLeft <= 0)
			{
				fadeCol.A = 0f;
				textActiveMs = 0L;
				hoverElement.SetVisible(false);
				return;
			}
			if(visibleMsPassed < 250)
			{
				fadeCol.A = visibleMsPassed / 240f;
			}
			else
			{
				fadeCol.A = 1f;
			}
			if(visibleMsLeft < 1000)
			{
				fadeCol.A = visibleMsLeft / 990f;
			}
		}
	}
}
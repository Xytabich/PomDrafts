using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PowerOfMind.Drafts.Blocks.Entities;
using PowerOfMind.Drafts.Common;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace PowerOfMind.Drafts.GUI
{
	public class GuiDialogDraftingTable : GuiDialogGeneric
	{
		public bool IsDuplicate { get; }

		public override bool PrefersUngrabbedMouse => false;
		public override EnumDialogType DialogType => EnumDialogType.Dialog;
		public override double DrawOrder => 0.11;

		public readonly int CanvasWidth, CanvasHeight;
		public readonly int HorizontalTiles, VerticalTiles;

		private List<GuiTab> tabs = default!;
		private GuiComposer? fullDialog = null;

		private readonly DraftsModSystem draftsSystem = default!;
		private readonly List<string> draftGroups = default!;
		private readonly InventoryDraftingTable inventory = default!;
		private readonly BlockPos blockPos = default!;
		private readonly int areaWidth, areaHeight;

		private readonly ItemInfoRenderer itemInfoRenderer = default!;

		private readonly Dictionary<(int x, int y), (AssetLocation draft, int rotation)> drafts = new();
		private readonly Dictionary<(int x, int y), (int x, int y)> cellToMain = new();
		private readonly List<IFlatListItem> draftElements = new();

		private readonly List<SchematicRecipe> recipeList = new List<SchematicRecipe>();

		private string prevSearch = string.Empty;
		private string currentGroup = string.Empty;

		private (int x, int y, int rotation, AssetLocation? code) draggingDraft = default;

		private GuiElementDraftCanvas canvasElement = default!;
		private CancellationTokenSource? searchCancellation = null;

		private AssetLocation? prevInfoDrawCode = null;
		private ItemStack? prevInfoDrawStack = null;

		public GuiDialogDraftingTable(ICoreClientAPI api, BlockPos blockPos, InventoryDraftingTable inventory, int horTiles, int verTiles) : base("", api)
		{
			IsDuplicate = capi.World.Player.InventoryManager.Inventories.ContainsValue(inventory);
			if(IsDuplicate) return;

			this.blockPos = blockPos;
			this.inventory = inventory;

			itemInfoRenderer = new ItemInfoRenderer(capi);

			var maxWidth = api.Gui.WindowBounds.InnerWidth / ClientSettings.GUIScale - 1000;
			var maxHeight = api.Gui.WindowBounds.InnerHeight / ClientSettings.GUIScale - 120;
			CanvasHeight = (int)maxHeight;
			CanvasWidth = (int)((maxHeight / verTiles) * horTiles);

			HorizontalTiles = horTiles;
			VerticalTiles = verTiles;
			areaWidth = (int)maxWidth;
			areaHeight = (int)maxHeight;
			draftsSystem = api.ModLoader.GetModSystem<DraftsModSystem>();
			draftGroups = new(draftsSystem.GetDraftGroups());
		}

		public void SetDrafts(IReadOnlyDictionary<(int x, int y), (AssetLocation code, int rotation)> drafts)
		{
			this.drafts.Clear();
			cellToMain.Clear();
			foreach(var pair in drafts)
			{
				SetDraft(pair.Value.code, pair.Key, pair.Value.rotation);
			}

			UpdateCanvas();
			UpdateResultInfo();
		}

		public override bool TryOpen()
		{
			if(IsDuplicate)
			{
				return false;
			}
			return base.TryOpen();
		}

		public override void OnGuiOpened()
		{
			inventory.Open(capi.World.Player);
			capi.World.Player.InventoryManager.OpenInventory(inventory);
			//capi.Gui.PlaySound(OpenSound, randomizePitch: true);//TODO: sound of a ruler or chalk board

			ComposeDialog();
			SingleComposer = fullDialog;
			OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));

			prevSearch = string.Empty;
			currentGroup = string.Empty;
			StartSearch(string.Empty, string.Empty);
		}

		public override void OnGuiClosed()
		{
			CancelSearch();

			foreach(var element in draftElements)
			{
				element.Dispose();
			}
			draftElements.Clear();

			canvasElement = null!;
			inventory.Close(capi.World.Player);
			capi.World.Player.InventoryManager.CloseInventory(inventory);
			capi.Network.SendBlockEntityPacket(blockPos.X, blockPos.Y, blockPos.Z, 1001);
		}

		public override void Dispose()
		{
			base.Dispose();

			itemInfoRenderer.Dispose();

			foreach(var element in draftElements)
			{
				element.Dispose();
			}
			draftElements.Clear();
		}

		public override void OnFinalizeFrame(float dt)
		{
			base.OnFinalizeFrame(dt);
			if(!IsInRangeOfBlock(blockPos))
			{
				capi.Event.EnqueueMainThreadTask(Close, "closedlg");
			}
		}

		private void ComposeDialog()
		{
			const double titleSize = 30.0;
			var leftContainer = ElementBounds.Fixed(0.0, titleSize, 300.0, areaHeight);
			var searchFieldBounds = leftContainer.FlatCopy().WithFixedOffset(2, 4).FixedShrink(8).WithFixedHeight(30);
			var stackListBounds = leftContainer.FlatCopy().WithFixedOffset(2, searchFieldBounds.fixedHeight + 8).FixedGrow(-8 - 20 - 8, -searchFieldBounds.fixedHeight - 8);
			var stackListInner = stackListBounds.FlatCopy().WithFixedPosition(3, 3).FixedShrink(6);
			var stackListClipBounds = stackListBounds.ForkBoundingParent();
			var stackScrollBounds = stackListBounds.CopyOffsetedSibling(7.0 + stackListBounds.fixedWidth).WithFixedWidth(20.0);

			var canvasBounds = ElementBounds.Fixed(leftContainer.fixedWidth, titleSize, areaWidth, areaHeight);

			var rightContainer = ElementBounds.Fixed(leftContainer.fixedWidth + canvasBounds.fixedWidth, titleSize, 300.0, areaHeight);
			var gridSize = GuiElementItemSlotGridBase.unscaledSlotPadding * 3 + GuiElementPassiveItemSlot.unscaledSlotSize * 4;
			var inputBounds = rightContainer.FlatCopy()
				.WithFixedOffset(rightContainer.fixedWidth - 3 - gridSize, rightContainer.fixedHeight - 3 - gridSize)
				.WithFixedSize(gridSize, gridSize);
			var outputBounds = inputBounds.FlatCopy()
				.WithFixedOffset(
					-(rightContainer.fixedWidth - gridSize - 12),
					inputBounds.fixedHeight / 2 - GuiElementPassiveItemSlot.unscaledSlotSize / 2
				).WithFixedSize(
					GuiElementPassiveItemSlot.unscaledSlotSize,
					GuiElementPassiveItemSlot.unscaledSlotSize
				);

			var infoBounds = rightContainer.FlatCopy().WithFixedOffset(6, 0).FixedGrow(-6, 0);
			var infoInner = infoBounds.FlatCopy().WithFixedOffset(3, 3).FixedShrink(6);
			var recipeBounds = infoInner.FlatCopy().WithFixedHeight(4 * GuiElementPassiveItemSlot.unscaledSlotSize);

			ActionConsumable? copyJson = null;
			var controlLabelBounds = infoInner.FlatCopy().WithFixedOffset(0, recipeBounds.fixedHeight + 20).WithFixedHeight(20);
			var controlBounds = infoInner.FlatCopy().WithFixedOffset(0, recipeBounds.fixedHeight + 40);
			var controls = new List<(string label, KeyCombination combination, ActionConsumable? onClick)>() {
				(Lang.Get("pomdrafts:dragdraft"), new KeyCombination() { KeyCode = KeyCombination.MouseStart + (int)EnumMouseButton.Left }, null),
				(Lang.Get("pomdrafts:rotatedraft"), new KeyCombination() { KeyCode = (int)GlKeys.R }, null),
				(Lang.Get("pomdrafts:dragcanvas"), new KeyCombination() { KeyCode = KeyCombination.MouseStart + (int)EnumMouseButton.Right }, null),
				(Lang.Get("pomdrafts:zoomcanvas"), new KeyCombination() { KeyCode = KeyCombination.MouseStart + (int)EnumMouseButton.Wheel }, null),
				(Lang.Get("pomdrafts:clearcanvas"), new KeyCombination() { KeyCode = (int)GlKeys.Delete }, ClearCanvas),
				(Lang.Get("pomdrafts:centercanvas"), new KeyCombination() { KeyCode = (int)GlKeys.Space }, CenterCanvas),
			};
			if(capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
			{
				copyJson = CopyJson;
				controls.Add((Lang.Get("pomdrafts:makejson"), new KeyCombination() { KeyCode = (int)GlKeys.C, Ctrl = true }, copyJson));
			}

			var groupList = leftContainer.RightCopy().WithFixedSize(1.0, 350.0);

			var bgBounds = ElementBounds.Fill.WithFixedPadding(6.0);
			bgBounds.BothSizing = ElementSizing.FitToChildren;
			bgBounds.WithChildren(canvasBounds, groupList, leftContainer, rightContainer);

			var dialogBounds = ElementStdBounds.AutosizedMainDialog
				.WithAlignment(EnumDialogArea.CenterTop)
				.WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, GuiStyle.DialogToScreenPadding);

			fullDialog?.Dispose();

			tabs = new List<GuiTab>();
			if(draftGroups.Count != 0)
			{
				tabs.Add(new GuiTab {
					Name = Lang.Get("pomdrafts:groupall"),
					DataInt = -1,
					Active = true
				});
				for(int i = 0; i < draftGroups.Count; i++)
				{
					tabs.Add(new GuiTab {
						Name = Lang.Get("pomdrafts:group-" + draftGroups[i]),
						DataInt = i,
						Active = false
					});
				}
			}

			var tabBounds = ElementBounds.Fixed(-200.0, 45.0, 200.0, areaHeight - 50);
			fullDialog = capi.Gui.CreateCompo("pomdrafttable", dialogBounds)
				.AddShadedDialogBG(bgBounds, withTitleBar: false)
				.AddDialogTitleBar(Lang.Get("pomdrafts:draftingtable"), Close)
				.BeginChildElements(bgBounds)

				.AddTextInput(searchFieldBounds, FilterBySearchText, CairoFont.WhiteSmallishText(), "searchField")
				.BeginClip(stackListClipBounds)
				.AddInset(stackListBounds, 3)
				.AddFlatList(stackListInner, null, draftElements, "stacklist")
				.EndClip()
				.AddVerticalScrollbar(OnDraftsScroll, stackScrollBounds, "scrollbar")

				.AddInset(canvasBounds, 2)
				.AddVerticalToggleTabs(tabs.ToArray(), tabBounds, OnTabClicked, "verticalTabs")
				.AddInteractiveElement(new GuiElementDraftCanvas(capi, this, canvasBounds, RenderCellInfo, BeginDrag, EndDrag, CancelDrag, ClearCanvas, copyJson), "canvasElem")

				.AddInset(infoBounds, 3)
				.AddInteractiveElement(new GuiElementSlideshowDraftRecipe(capi, recipeBounds, recipeList, itemInfoRenderer))
				.AddRichtext(Lang.Get("pomdrafts:tablecontrols"), CairoFont.WhiteDetailText(), controlLabelBounds)
				.AddCellList(controlBounds, CreateControlCell, controls)//TODO: make scroll?
				.AddItemSlotGridExcl(inventory, SendNetworkPacket, 4, new[] { 16 }, inputBounds)
				.AddItemSlotGrid(inventory, SendNetworkPacket, 4, new[] { 16 }, outputBounds)

				.EndChildElements()
				.Compose(false);

			var draftList = fullDialog.GetFlatList("stacklist");
			draftList.unscalledYPad = 0;
			fullDialog.GetScrollbar("scrollbar").SetHeights((float)stackListInner.fixedHeight, (float)draftList.insideBounds.fixedHeight);
			canvasElement = (GuiElementDraftCanvas)fullDialog.GetElement("canvasElem");
			canvasElement.SetZoomLevel(ClientSettings.GUIScale);

			((GuiElementVerticalTabs)fullDialog.GetElement("verticalTabs")).ToggleTabs = false;

			((GuiElementTextInput)fullDialog.GetElement("searchField")).SetPlaceHolderText(Lang.Get("Search..."));

			UpdateCanvas();
			UpdateResultInfo();
		}

		private bool CopyJson()
		{
			try
			{
				var recipe = new SchematicRecipe();
				recipe.InitRawPatternFromDrafts(draftsSystem, drafts);
				recipe.Output = new JsonItemStack() {
					Code = new AssetLocation("air"),
					Quantity = 0
				};
				var ingredients = new List<SchematicRecipeIngredient>();
				for(int i = 0; i < 16; i++)
				{
					var itemStack = inventory[i]?.Itemstack;
					if(itemStack != null)
					{
						var ingred = new SchematicRecipeIngredient();
						ingred.InitFromItemstack(itemStack);
						ingred.ResolvedItemstack = null;
						ingredients.Add(ingred);
					}
				}
				recipe.Ingredients = ingredients.ToArray();

				var serializer = new JsonSerializerSettings() {
					DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
					ContractResolver = new CamelCasePropertyNamesContractResolver()
				};
				serializer.Converters.Add(new FullAssetLocationSerializer());
				serializer.Converters.Add(new StringEnumConverter() { NamingStrategy = new CamelCaseNamingStrategy() });
				capi.Input.ClipboardText = JToken.FromObject(recipe, JsonSerializer.Create(serializer)).ToString(Formatting.Indented);
			}
			catch(Exception e)
			{
				capi.Logger.Error(e);
			}
			return true;
		}

		private bool CenterCanvas()
		{
			canvasElement?.CenterCanvas();
			return true;
		}

		private bool ClearCanvas()
		{
			if(drafts.Count != 0)
			{
				capi.Network.SendBlockEntityPacket(blockPos, BlockEntityDraftingTable.PACKET_CLR_DRAFTS);

				drafts.Clear();
				cellToMain.Clear();
				if(draggingDraft.code != null)
				{
					canvasElement.CancelDrag();
				}
				UpdateCanvas();
				UpdateBE();
			}
			return true;
		}

		private IGuiElementCell CreateControlCell((string label, KeyCombination combination, ActionConsumable? onClick) cell, ElementBounds bounds)
		{
			bounds.fixedPaddingY = 0;
			string control;
			if(cell.combination.KeyCode == KeyCombination.MouseStart + (int)EnumMouseButton.Wheel)
			{
				control = "Mouse wheel";
			}
			else
			{
				control = cell.combination.ToString();
			}
			return new GUIElementControlCell(capi, bounds, "<i>" + cell.label + "</i>", control, cell.onClick);
		}

		private void SendNetworkPacket(object packet)
		{
			capi.Network.SendBlockEntityPacket(blockPos.X, blockPos.Y, blockPos.Z, packet);
		}

		private void OnDraftsScroll(float value)
		{
			var flatList = fullDialog.GetFlatList("stacklist");
			flatList.insideBounds.fixedY = 3f - value;
			flatList.insideBounds.CalcWorldBounds();
		}

		private void UpdateDraftsScroll()
		{
			var stacklist = fullDialog.GetFlatList("stacklist");
			stacklist.CalcTotalHeight();
			fullDialog.GetScrollbar("scrollbar").SetHeights((float)stacklist.Bounds.fixedHeight, (float)stacklist.insideBounds.fixedHeight);
		}

		private void FilterBySearchText(string text)
		{
			if(prevSearch.AsSpan().Trim().SequenceEqual(text.AsSpan().Trim()))
			{
				return;
			}
			prevSearch = text;

			CancelSearch();
			StartSearch(text, currentGroup);
		}

		private void StartSearch(string text, string group)
		{
			searchCancellation = new CancellationTokenSource();
			var token = searchCancellation.Token;
			TyronThreadPool.QueueTask(() => SearchDrafts(text, group, token), "pomdraft:search");
		}

		private void CancelSearch()
		{
			if(searchCancellation == null) return;
			searchCancellation.Cancel();
			searchCancellation.Dispose();
			searchCancellation = null;
		}

		private void OnTabClicked(int index, GuiTab tab)
		{
			if(tab.Active)
			{
				var nextGroup = tab.DataInt < 0 ? string.Empty : draftGroups[tab.DataInt];
				if(nextGroup != currentGroup)
				{
					currentGroup = nextGroup;
					CancelSearch();
					StartSearch(prevSearch, currentGroup);
				}
			}
		}

		private void Close()
		{
			TryClose();
		}

		private void RenderCellInfo((int x, int y) index, (float x, float y) mousePos, float dt)
		{
			AssetLocation? newCode = null;
			if(cellToMain.TryGetValue(index, out var main))
			{
				newCode = drafts[main].draft;
			}

			if(newCode != prevInfoDrawCode)
			{
				prevInfoDrawCode = newCode;
				prevInfoDrawStack = newCode == null ? null : draftsSystem.GetDescriptor(newCode)?.CreateDummyStack(newCode);
			}
			if(prevInfoDrawStack != null)
			{
				itemInfoRenderer.RenderItemstackTooltip(prevInfoDrawStack, mousePos.x, mousePos.y, dt);
			}
		}

		private void RenderDraftInfo(ItemStack stack, (float x, float y) mousePos, float dt)
		{
			if(draggingDraft == default)
			{
				itemInfoRenderer.RenderItemstackTooltip(stack, mousePos.x, mousePos.y, dt);
			}
		}

		private void DragNewDraft((float x, float y) mousePos, AssetLocation code)
		{
			if(draggingDraft.code != null) return;

			draggingDraft = (-1, -1, 0, code);
			canvasElement.DragShape(mousePos.x, mousePos.y, draftsSystem.GetDraftShape(code), 0);
		}

		private void BeginDrag((int x, int y) index, (float x, float y) mousePos)
		{
			if(cellToMain.TryGetValue(index, out var main))
			{
				var info = drafts[main];
				RemoveDraft(main);

				capi.Gui.PlaySound("menubutton_press");

				capi.Network.SendBlockEntityPacket(blockPos, BlockEntityDraftingTable.PACKET_DEL_DRAFT, SerializerUtil.Serialize(main));

				draggingDraft = (main.x, main.y, info.rotation, info.draft);

				canvasElement.DragShape(mousePos.x, mousePos.y, draftsSystem.GetDraftShape(info.draft), info.rotation);
			}
		}

		private void EndDrag((int x, int y) index, int rotation)
		{
			if(draggingDraft.code != null)
			{
				bool revert = !TrySetDraft(draggingDraft.code, index, rotation);
				if(revert) TrySetDraft(draggingDraft.code, (draggingDraft.x, draggingDraft.y), draggingDraft.rotation);

				draggingDraft = default;
			}
		}

		private void CancelDrag()
		{
			draggingDraft = default;
		}

		private void RemoveDraft((int x, int y) index)
		{
			if(drafts.Remove(index, out var info))
			{
				var shape = draftsSystem.GetDraftShape(info.draft);
				if(info.rotation != 0)
				{
					shape = shape.Clone().Rotate(info.rotation);
				}
				foreach(var cell in shape.Cells)
				{
					cellToMain.Remove((cell.X + index.x, cell.Y + index.y));
				}

				UpdateCanvas();
				UpdateBE();
				UpdateResultInfo();
			}
		}

		private void SetDraft(AssetLocation draft, (int x, int y) index, int rotation)
		{
			drafts[index] = (draft, rotation);

			var shape = draftsSystem.GetDraftShape(draft);
			if(rotation != 0)
			{
				shape = shape.Clone().Rotate(rotation);
			}
			foreach(var cell in shape.Cells)
			{
				cellToMain[(cell.X + index.x, cell.Y + index.y)] = index;
			}
		}

		private bool TrySetDraft(AssetLocation draft, (int x, int y) index, int rotation)
		{
			if(!draftsSystem.GetPlayerDrafts().Contains(draft))
			{
				return false;
			}

			var shape = draftsSystem.GetDraftShape(draft);
			if(rotation != 0)
			{
				shape = shape.Clone().Rotate(rotation);
			}

			index = (Math.Clamp(index.x, 0, HorizontalTiles - shape.Width), Math.Clamp(index.y, 0, VerticalTiles - shape.Height));
			if(index.x + shape.Width > HorizontalTiles || index.y + shape.Height > VerticalTiles)
			{
				return false;
			}

			foreach(var cell in shape.Cells)
			{
				if(cellToMain.ContainsKey((cell.X + index.x, cell.Y + index.y)))
				{
					return false;
				}
			}

			capi.Gui.PlaySound("menubutton_press");

			capi.Network.SendBlockEntityPacket(blockPos, BlockEntityDraftingTable.PACKET_ADD_DRAFT,
				SerializerUtil.Serialize(new BlockEntityDraftingTable.AddDraftCmd() {
					X = index.x,
					Y = index.y,
					Rotation = rotation,
					DraftCode = draft
				}));

			SetDraft(draft, index, rotation);

			UpdateCanvas();
			UpdateBE();
			UpdateResultInfo();
			return true;
		}

		private void UpdateCanvas()
		{
			if(canvasElement == null) return;
			canvasElement.ClearShapes();
			foreach(var pair in drafts)
			{
				canvasElement.AddShape(pair.Key.x, pair.Key.y, draftsSystem.GetDraftShape(pair.Value.draft), pair.Value.rotation);
			}
		}

		private void UpdateBE()
		{
			(capi.World.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityDraftingTable)?.UpdateRenderDrafts(drafts);
		}

		private void SearchDrafts(string text, string globalGroup, CancellationToken token)
		{
			try
			{
				ReadOnlySpan<char> modid = default;
				ReadOnlySpan<char> group = default;

				int nameStartIndex = 0;
				var span = text.AsSpan();
				int index = span.IndexOf('@');
				if(index >= 0)
				{
					modid = span.Slice(index + 1);
					modid = FilterWord(modid);
					nameStartIndex = index + 1 + modid.Length;
				}
				index = span.IndexOf('#');
				if(index >= 0)
				{
					group = span.Slice(index + 1);
					group = FilterWord(group);
					nameStartIndex = Math.Max(nameStartIndex, index + 1 + group.Length);
				}
				if(nameStartIndex < span.Length)
				{
					span = span.Slice(nameStartIndex).Trim();
				}
				else
				{
					span = default;
				}

				List<(AssetLocation code, ItemStack stack)>? outList = null;
				int counter = 0;
				foreach(var code in draftsSystem.GetPlayerDrafts())
				{
					if(counter++ >= 100)
					{
						counter = 0;
						if(token.IsCancellationRequested) return;
					}
					try
					{
						var descriptor = draftsSystem.GetDescriptor(code);
						if(descriptor != null)
						{
							if(!modid.IsEmpty)
							{
								var domain = code.Domain;
								if(!domain.AsSpan().Contains(modid, StringComparison.InvariantCultureIgnoreCase))
								{
									bool skip = true;
									foreach(var mod in capi.ModLoader.Mods)
									{
										if(mod.Info.ModID == domain)
										{
											if(mod.Info.Name.AsSpan().Contains(modid, StringComparison.InvariantCultureIgnoreCase))
											{
												skip = false;
											}
											break;
										}
									}
									if(skip) continue;
								}
							}
							if(!string.IsNullOrEmpty(globalGroup))
							{
								bool skip = true;
								foreach(var groupCode in descriptor.GetDraftGroups(code))
								{
									if(groupCode.Equals(globalGroup, StringComparison.InvariantCultureIgnoreCase))
									{
										skip = false;
										break;
									}
								}
								if(skip) continue;
							}
							if(!group.IsEmpty)
							{
								bool skip = true;
								foreach(var groupCode in descriptor.GetDraftGroups(code))
								{
									var name = Lang.Get("pomdraftgroup-" + groupCode);
									if(name.AsSpan().Contains(group, StringComparison.CurrentCultureIgnoreCase))
									{
										skip = false;
										break;
									}
								}
								if(skip) continue;
							}
							var stack = descriptor.CreateDummyStack(code);
							if(!span.IsEmpty)
							{
								if(!stack.GetName().AsSpan().Contains(span, StringComparison.CurrentCultureIgnoreCase))
								{
									continue;
								}
							}
							(outList ??= new()).Add((code, stack));
						}
					}
					catch { }
				}

				try
				{
					outList?.Sort((a, b) => {
						if(a.code == b.code) return 0;

						var c = a.stack.GetName().CompareTo(b.stack.GetName());
						if(c != 0) return c;

						return Utils.Compare(a.code, b.code);
					});
				}
				catch { }

				if(token.IsCancellationRequested) return;
				capi.Event.EnqueueMainThreadTask(() => {
					if(!token.IsCancellationRequested)
					{
						if(searchCancellation != null)
						{
							searchCancellation.Dispose();
							searchCancellation = null;
						}

						UpdateList(outList);
					}
				}, "pomdraft:search-complete");
			}
			catch { }
		}

		private void UpdateList(List<(AssetLocation code, ItemStack stack)>? list)
		{
			foreach(var element in draftElements)
			{
				element.Dispose();
			}
			draftElements.Clear();

			if(list != null)
			{
				Action<(float x, float y), AssetLocation> beginDrag = DragNewDraft;
				Action<ItemStack, (float x, float y), float> renderDraftInfo = RenderDraftInfo;
				foreach(var info in list)
				{
					draftElements.Add(new GuiElementDraftButton(info.code, info.stack, beginDrag, renderDraftInfo));
				}
			}

			UpdateDraftsScroll();
		}

		private void UpdateResultInfo()
		{
			recipeList.Clear();
			draftsSystem.FindMatchingRecipes(drafts, recipeList);

			//TODO: make some sort of client preview, but without calling this stuff from gui
			inventory.Recipes.Clear();
			inventory.Recipes.AddRange(recipeList);
			draftsSystem.FindMatchingRecipes(drafts, recipeList);
			inventory.FindMatchingRecipe();
		}

		private static ReadOnlySpan<char> FilterWord(ReadOnlySpan<char> span)
		{
			for(int i = 0; i < span.Length; i++)
			{
				if(span[i] != '#' && span[i] != '@')
				{
					if(char.IsHighSurrogate(span[i]))
					{
						var rune = new Rune(span[i], span[i + 1]);
						if(Rune.IsLetterOrDigit(rune) || Rune.IsPunctuation(rune)) continue;
					}

					if(char.IsLetterOrDigit(span[i]) || char.IsPunctuation(span[i])) continue;
				}

				span = span.Slice(0, i);
			}

			return span;
		}
	}
}
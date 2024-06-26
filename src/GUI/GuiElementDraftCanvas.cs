using Cairo;
using PowerOfMind.Drafts.Common;
using System.Collections;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Drafts.GUI
{
	public class GuiElementDraftCanvas : GuiElement
	{
		private const float MIN_ZOOM = 0.5f;
		private const float MAX_ZOOM = 2f;

		private const float UV_QUARTER = 1f / 4f;
		private const float UV_MARGIN = UV_QUARTER / 32f;

		private const float NON_CONNECTED_OFFSET = 0.25f;

		private static readonly AssetLocation gridTexturePath = new AssetLocation("pomdrafts", "textures/gui/grid.png");
		private static readonly AssetLocation draftTexturePath = new AssetLocation("pomdrafts", "textures/gui/draft.png");
		private static readonly (float x, float y)[] uvOffsets = new (float x, float y)[] {
			(1 * UV_QUARTER, 0 * UV_QUARTER),
			(2 * UV_QUARTER, 0 * UV_QUARTER),
			(3 * UV_QUARTER, 0 * UV_QUARTER),
			(0 * UV_QUARTER, 1 * UV_QUARTER),
			(1 * UV_QUARTER, 1 * UV_QUARTER),
			(2 * UV_QUARTER, 1 * UV_QUARTER),
			(3 * UV_QUARTER, 1 * UV_QUARTER),
			(0 * UV_QUARTER, 2 * UV_QUARTER),
			(1 * UV_QUARTER, 2 * UV_QUARTER),
			(2 * UV_QUARTER, 2 * UV_QUARTER),
			(3 * UV_QUARTER, 2 * UV_QUARTER),
			(0 * UV_QUARTER, 3 * UV_QUARTER),
			(1 * UV_QUARTER, 3 * UV_QUARTER),
			(2 * UV_QUARTER, 3 * UV_QUARTER),
			(3 * UV_QUARTER, 3 * UV_QUARTER),
		};

		public float RenderZ = 50f;

		public override bool Focusable => true;

		private float tkeyDeltaX;
		private float tkeyDeltaY;
		private float skeyDeltaX;
		private float skeyDeltaY;
		private float zoomLevel = 1f;

		private int prevMouseX;
		private int prevMouseY;

		private bool isDraggingCanvas = false;

		private readonly GuiDialogDraftingTable dialog;
		private readonly Action<(int x, int y), (float x, float y), float> renderCellInfo;
		private readonly Action<(int x, int y), (float x, float y)> beginDrag;
		private readonly Action<(int x, int y), int> endDrag;
		private readonly Action cancelDrag;
		private readonly ActionConsumable clearCanvas;
		private readonly ActionConsumable? copyCanvas;

		private readonly DraftAssetsSystem assets;
		private readonly Vec2d areaOffset = new Vec2d();

		private bool isMeshesDirty = true;
		private readonly List<(int textureId, MeshRef handle, bool hasColor)> meshRenderList = new();
		private readonly List<(int x, int y, DraftShape shape, int rotation)> shapes = new();
		private LoadedTexture gridTexture;
		private LoadedTexture draftTexture;

		private DraftDrag? draftDrag = null;
		private DraftHover? draftHover = null;

		public GuiElementDraftCanvas(ICoreClientAPI capi, GuiDialogDraftingTable dialog, ElementBounds bounds, Action<(int x, int y), (float x, float y), float> renderCellInfo,
			Action<(int x, int y), (float x, float y)> beginDrag, Action<(int x, int y), int> endDrag, Action cancelDrag, ActionConsumable clearCanvas, ActionConsumable? copyCanvas) : base(capi, bounds)
		{
			this.dialog = dialog;
			this.renderCellInfo = renderCellInfo;
			this.beginDrag = beginDrag;
			this.endDrag = endDrag;
			this.cancelDrag = cancelDrag;
			this.clearCanvas = clearCanvas;
			this.copyCanvas = copyCanvas;

			gridTexture = new LoadedTexture(api);
			draftTexture = new LoadedTexture(api);
			assets = capi.ModLoader.GetModSystem<DraftAssetsSystem>();
		}

		public void AddShape(int x, int y, in DraftShape shape, int rotation)
		{
			shapes.Add((x, y, shape.Clone().Rotate(rotation), rotation));
			isMeshesDirty = true;
			draftHover?.SetDirty();
		}

		public void ClearShapes()
		{
			shapes.Clear();
			isMeshesDirty = true;
			draftHover?.SetDirty();
		}

		public void DragShape(float x, float y, in DraftShape shape, int rotation)
		{
			draftDrag?.Dispose();
			draftDrag = new DraftDrag(this, shape.Clone().Rotate(rotation), rotation);
			draftDrag.UpdateDrag(x, y);
			draftHover?.SetDirty();
		}

		public void CancelDrag()
		{
			draftHover?.SetDirty();
			draftDrag?.Dispose();
			draftDrag = null;
		}

		public override void ComposeElements(Context ctxStatic, ImageSurface surface)
		{
			Bounds.CalcWorldBounds();
		}

		public override void RenderInteractiveElements(float deltaTime)
		{
			var rapi = api.Render;
			if(isMeshesDirty)
			{
				isMeshesDirty = false;
				if(meshRenderList.Count == 0)
				{
					GenGridMesh();
				}

				RegenShapes();
			}

			var mx = api.Input.MouseX;
			var my = api.Input.MouseY;
			(int x, int y)? hoverIndex = null;
			if(draftDrag == null && Bounds.PointInside(mx, my))
			{
				hoverIndex = MousePosToIndex(mx, my, false);
			}

			rapi.PushScissor(Bounds);

			var zoomMult = 0.5f * zoomLevel * ClientSettings.GUIScale;
			var halfWidth = dialog.CanvasWidth * zoomMult;
			var halfHeight = dialog.CanvasHeight * zoomMult;
			var tileSize = dialog.CanvasHeight * zoomMult * 2 / (double)dialog.VerticalTiles;

			var posX = (float)((Bounds.renderX + Bounds.InnerWidth * 0.5) + areaOffset.X - halfWidth - tileSize);
			var posY = (float)((Bounds.renderY + Bounds.InnerHeight * 0.5) + areaOffset.Y - halfHeight - tileSize);
			var posZ = RenderZ;

			// Cell size is considered to be one, so scaling needs to be calculated to fit the gui
			var scaleX = (float)(halfWidth / dialog.HorizontalTiles * 4);
			var scaleY = (float)(halfHeight / dialog.VerticalTiles * 4);

			RenderLayers(meshRenderList, posX, posY, posZ, scaleX, scaleY, hoverIndex);

			rapi.PopScissor();
			rapi.CheckGlError();

			draftDrag?.Render(scaleX, scaleY, (float)tileSize);

			if(hoverIndex.HasValue)
			{
				renderCellInfo(hoverIndex.Value, (mx, my), deltaTime);
			}
		}

		public override void PostRenderInteractiveElements(float deltaTime)
		{
			if(HasFocus)
			{
				if(api.Input.KeyboardKeyStateRaw[(int)GlKeys.Up])
				{
					tkeyDeltaY = 15f;
				}
				else if(api.Input.KeyboardKeyStateRaw[(int)GlKeys.Down])
				{
					tkeyDeltaY = -15f;
				}
				else
				{
					tkeyDeltaY = 0f;
				}
				if(api.Input.KeyboardKeyStateRaw[(int)GlKeys.Left])
				{
					tkeyDeltaX = 15f;
				}
				else if(api.Input.KeyboardKeyStateRaw[(int)GlKeys.Right])
				{
					tkeyDeltaX = -15f;
				}
				else
				{
					tkeyDeltaX = 0f;
				}
				skeyDeltaX += (tkeyDeltaX - skeyDeltaX) * deltaTime * 15f;
				skeyDeltaY += (tkeyDeltaY - skeyDeltaY) * deltaTime * 15f;
				if(Math.Abs(skeyDeltaX) > 0.5f || Math.Abs(skeyDeltaY) > 0.5f)
				{
					var zoomLevel = this.zoomLevel * ClientSettings.GUIScale;
					areaOffset.X += skeyDeltaX * zoomLevel;
					areaOffset.Y += skeyDeltaY * zoomLevel;
					ClampOffset();
				}
			}
		}

		public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
		{
			base.OnMouseDownOnElement(api, args);
			switch(args.Button)
			{
				case EnumMouseButton.Right:
					isDraggingCanvas = true;
					prevMouseX = args.X;
					prevMouseY = args.Y;
					break;
				case EnumMouseButton.Left:
					beginDrag(MousePosToIndex(args.X, args.Y, false), (args.X, args.Y));
					break;
			}
		}

		public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
		{
			if(isDraggingCanvas || draftDrag != null)
			{
				args.Handled = true;

				if(isDraggingCanvas)
				{
					areaOffset.X += args.X - prevMouseX;
					areaOffset.Y += args.Y - prevMouseY;
					ClampOffset();
				}

				draftDrag?.UpdateDrag(args.X, args.Y);

				prevMouseX = args.X;
				prevMouseY = args.Y;
			}
		}

		public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
		{
			base.OnMouseUp(api, args);
			switch(args.Button)
			{
				case EnumMouseButton.Right:
					if(isDraggingCanvas)
					{
						args.Handled = true;

						isDraggingCanvas = false;
					}
					break;
				case EnumMouseButton.Left:
					if(draftDrag != null)
					{
						args.Handled = true;

						int rotation = draftDrag.Rotation;
						var size = draftDrag.Size;
						draftDrag.Dispose();
						draftDrag = null;

						if(args.X < Bounds.absX)
						{
							cancelDrag();
						}
						else
						{
							var tileScale = dialog.CanvasHeight * 0.5f * zoomLevel / (double)dialog.VerticalTiles;
							endDrag(MousePosToIndex((float)(args.X - tileScale * size.x), (float)(args.Y - tileScale * size.y), true), rotation);
						}
					}
					break;
			}
		}

		public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
		{
			if(Bounds.PointInside(api.Input.MouseX, api.Input.MouseY))
			{
				ZoomChange((args.delta > 0) ? 0.25f : -0.25f);
				args.SetHandled();
			}
		}

		public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
		{
			if(!HasFocus && draftDrag == null) return;

			if(args.KeyCode == (int)GlKeys.Space)
			{
				args.Handled = true;
				CenterCanvas();
			}
			if(args.KeyCode == (int)GlKeys.R)
			{
				args.Handled = true;
				draftDrag?.Rotate();
			}
			if(args.KeyCode == (int)GlKeys.Delete)
			{
				args.Handled = true;
				clearCanvas();
			}
			if(args.KeyCode == (int)GlKeys.C && args.CtrlPressed)
			{
				args.Handled = true;
				copyCanvas?.Invoke();
			}
			if(api.Input.KeyboardKeyStateRaw[(int)GlKeys.Up] || api.Input.KeyboardKeyStateRaw[(int)GlKeys.Down] ||
				api.Input.KeyboardKeyStateRaw[(int)GlKeys.Left] || api.Input.KeyboardKeyStateRaw[(int)GlKeys.Right])
			{
				args.Handled = true;
			}
		}

		public void SetZoomLevel(float zoomLevel)
		{
			this.zoomLevel = Math.Clamp(zoomLevel, MIN_ZOOM, MAX_ZOOM);
			ClampOffset();
		}

		public void CenterCanvas()
		{
			areaOffset.Set(0, 0);
		}

		public override void Dispose()
		{
			base.Dispose();
			foreach(var info in meshRenderList)
			{
				info.handle.Dispose();
			}
			meshRenderList.Clear();
			draftDrag?.Dispose();
			draftDrag = null;
			draftHover?.Dispose();
			draftHover = null;
		}

		private void ZoomChange(float zoomDiff)
		{
			zoomLevel = Math.Clamp(zoomLevel + zoomDiff, MIN_ZOOM, MAX_ZOOM);
			ClampOffset();
		}

		private (int x, int y) MousePosToIndex(float mouseX, float mouseY, bool round)
		{
			var zoomLevel = this.zoomLevel * ClientSettings.GUIScale;
			var halfWidth = dialog.CanvasWidth * 0.5 * zoomLevel;
			var halfHeight = dialog.CanvasHeight * 0.5 * zoomLevel;

			var x = (mouseX - (Bounds.absX + Bounds.InnerWidth * 0.5 + areaOffset.X - halfWidth)) / ((double)dialog.CanvasWidth * zoomLevel);
			var y = (mouseY - (Bounds.absY + Bounds.InnerHeight * 0.5 + areaOffset.Y - halfHeight)) / ((double)dialog.CanvasHeight * zoomLevel);
			if(round)
			{
				return (
					(int)Math.Round(x * dialog.HorizontalTiles),
					(int)Math.Round(y * dialog.VerticalTiles)
				);
			}
			return (
				(int)Math.Floor(x * dialog.HorizontalTiles),
				(int)Math.Floor(y * dialog.VerticalTiles)
			);
		}

		private void GenGridMesh()
		{
			int width = dialog.HorizontalTiles;
			int height = dialog.VerticalTiles;
			int totalTiles = width * height;
			var mesh = new MeshData(4 * totalTiles, 6 * totalTiles, false, true, false, false);

			for(int y = 0; y <= height; y++)
			{
				bool hasPrevY = y != 0;
				bool hasNextY = y != height;
				for(int x = 0; x <= width; x++)
				{
					bool hasPrevX = x != 0;
					bool hasNextX = x != width;

					int mask = 0;
					if(hasPrevX & hasPrevY) mask |= 1;
					if(hasPrevX & hasNextY) mask |= 2;
					if(hasNextX & hasPrevY) mask |= 4;
					if(hasNextX & hasNextY) mask |= 8;
					if(mask == 0) continue;
					mask--;

					var (uOffset, vOffset) = uvOffsets[mask];
					AddQuad(
						mesh,
						x - 0.5f,
						y - 0.5f,
						x + 0.5f,
						y + 0.5f,
						uOffset,
						vOffset,
						uOffset + UV_QUARTER,
						vOffset + UV_QUARTER
					);
				}
			}

			api.Render.GetOrLoadTexture(gridTexturePath, ref gridTexture);
			meshRenderList.Add((gridTexture.TextureId, api.Render.UploadMesh(mesh), false));
		}

		private void RegenShapes()
		{
			int renderCounter = 1;
			try
			{
				if(shapes.Count > 0)
				{
					var bits = new BitArray(1024);
					var backingMesh = new MeshData(24, 36, false, true, true, false);
					var draftBatches = new Dictionary<int, MeshData>();
					var connections = new List<(float px, float py, int cx, int cy, (AssetLocation? type, bool isOutput) info)>();
					foreach(var info in shapes)
					{
						int requiredBits = info.shape.Width * info.shape.Height;
						if(bits.Count < requiredBits)
						{
							bits = new BitArray(((requiredBits - 1) / 1024 + 1) * 1024);
						}
						else
						{
							bits.SetAll(false);
						}

						foreach(var cell in info.shape.Cells)
						{
							bits[cell.X + cell.Y * info.shape.Width] = true;

							if(cell.Connections == null)
							{
								continue;
							}

							int x = (info.x + cell.X) * 2;
							int y = (info.y + cell.Y) * 2;
							float px = info.x + cell.X + 0.5f;
							float py = info.y + cell.Y + 0.5f;

							if(cell.Connections[(int)DraftShape.ConnectionSide.Up] != default)
							{
								connections.Add((px, py - NON_CONNECTED_OFFSET, x, y - 1, cell.Connections[(int)DraftShape.ConnectionSide.Up]!));
							}
							if(cell.Connections[(int)DraftShape.ConnectionSide.Down] != default)
							{
								connections.Add((px, py + NON_CONNECTED_OFFSET, x, y + 1, cell.Connections[(int)DraftShape.ConnectionSide.Down]!));
							}
							if(cell.Connections[(int)DraftShape.ConnectionSide.Left] != default)
							{
								connections.Add((px - NON_CONNECTED_OFFSET, py, x - 1, y, cell.Connections[(int)DraftShape.ConnectionSide.Left]!));
							}
							if(cell.Connections[(int)DraftShape.ConnectionSide.Right] != default)
							{
								connections.Add((px + NON_CONNECTED_OFFSET, py, x + 1, y, cell.Connections[(int)DraftShape.ConnectionSide.Right]!));
							}
						}

						BuildBacking(backingMesh, info.x, info.y, info.shape.Width, info.shape.Height, bits, (int)info.shape.Color);

						var texPos = GetDraftSprite(info.shape.Texture);
						if(!draftBatches.TryGetValue(texPos.atlasTextureId, out var mesh))
						{
							mesh = new MeshData(24, 36, false, true, false, false);
							draftBatches[texPos.atlasTextureId] = mesh;
						}

						AddRotatedQuad(
							mesh,
							info.x,
							info.y,
							info.x + info.shape.Width,
							info.y + info.shape.Height,
							texPos.x1,
							texPos.y1,
							texPos.x2,
							texPos.y2,
							info.rotation
						);
					}

					var connected = new Dictionary<(int cx, int cy, AssetLocation type), (bool hasInput, bool hasOutput)>();
					foreach(var conn in connections)
					{
						var key = (conn.cx, conn.cy, conn.info.type!);
						if(!connected.TryGetValue(key, out var flags))
						{
							flags = (false, false);
						}
						if(conn.info.isOutput) flags.hasOutput = true;
						else flags.hasInput = true;
						connected[key] = flags;
					}

					var connectionBatches = new Dictionary<int, MeshData>();
					foreach(var conn in connections)
					{
						var flags = connected[(conn.cx, conn.cy, conn.info.type!)];
						if(!(flags.hasInput && flags.hasOutput))
						{
							float px = conn.px - 0.5f;
							float py = conn.py - 0.5f;
							var texPos = assets.GetConnectionTexture(conn.info.type!, conn.info.isOutput ? ConnectionTextureType.Output : ConnectionTextureType.Input);
							if(!connectionBatches.TryGetValue(texPos.atlasTextureId, out var mesh))
							{
								mesh = new MeshData(24, 36, false, true, false, false);
								connectionBatches[texPos.atlasTextureId] = mesh;
							}
							AddQuad(
								mesh,
								px,
								py,
								px + 1,
								py + 1,
								texPos.x1,
								texPos.y1,
								texPos.x2,
								texPos.y2
							);
						}
					}
					foreach(var pair in connected)
					{
						if(!(pair.Value.hasInput && pair.Value.hasOutput)) continue;

						var info = pair.Key;
						float px = info.cx * 0.5f;
						float py = info.cy * 0.5f;
						var texPos = assets.GetConnectionTexture(info.type, ConnectionTextureType.Connected);
						if(!connectionBatches.TryGetValue(texPos.atlasTextureId, out var mesh))
						{
							mesh = new MeshData(24, 36, false, true, false, false);
							connectionBatches[texPos.atlasTextureId] = mesh;
						}
						AddQuad(
							mesh,
							px,
							py,
							px + 1,
							py + 1,
							texPos.x1,
							texPos.y1,
							texPos.x2,
							texPos.y2
						);
					}

					api.Render.GetOrLoadTexture(draftTexturePath, ref draftTexture);
					UpsertMesh(renderCounter++, draftTexture.TextureId, backingMesh, true);

					foreach(var pair in draftBatches)
					{
						UpsertMesh(renderCounter++, pair.Key, pair.Value, false);
					}
					foreach(var pair in connectionBatches)
					{
						UpsertMesh(renderCounter++, pair.Key, pair.Value, false);
					}
				}
			}
			catch(Exception e)
			{
				api.Logger.Error("[pomdrafts] Exception when trying to build a canvas\n{0}", e);
			}
			for(int i = meshRenderList.Count - 1; i >= renderCounter; i--)
			{
				meshRenderList[i].handle.Dispose();
				meshRenderList.RemoveAt(i);
			}
		}

		private TextureAtlasPosition GetDraftSprite(AssetLocation texture)
		{
			if(api.BlockTextureAtlas.GetOrInsertTexture(texture, out _, out var texPos, () => {
				var asset = api.Assets.TryGet(texture.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
				return asset?.ToBitmap(api);
			}) == false)
			{
				texPos = api.BlockTextureAtlas.UnknownTexturePosition;
			}
			return texPos;
		}

		private void UpsertMesh(int index, int textureId, MeshData mesh, bool hasColor)
		{
			var rapi = api.Render;
			if(index == meshRenderList.Count)
			{
				meshRenderList.Add((textureId, rapi.UploadMesh(mesh), hasColor));
			}
			else
			{
				meshRenderList[index].handle.Dispose();
				meshRenderList[index] = (textureId, rapi.UploadMesh(mesh), hasColor);
			}
		}

		private void RenderLayers(List<(int textureId, MeshRef handle, bool hasColor)> meshRenderList,
			float posX, float posY, float posZ, float scaleX, float scaleY, (int x, int y)? hoverIndex = null)
		{
			var rapi = api.Render;
			var game = (ClientMain)api.World;
			var guiShaderProg = ShaderPrograms.Gui;
			guiShaderProg.RgbaIn = ColorUtil.WhiteArgbVec;
			guiShaderProg.ExtraGlow = 0;
			guiShaderProg.NoTexture = 0f;
			guiShaderProg.OverlayOpacity = 0f;
			guiShaderProg.NormalShaded = 0;
			game.GlPushMatrix();
			game.GlTranslate(posX, posY, posZ);
			game.GlScale(scaleX, scaleY, 0.0);
			game.GlScale(0.5, 0.5, 0.0);
			game.GlTranslate(1.0, 1.0, 0.0);
			guiShaderProg.ProjectionMatrix = game.CurrentProjectionMatrix;
			guiShaderProg.ModelViewMatrix = game.CurrentModelViewMatrix;

			for(int i = 0; i < meshRenderList.Count; i++)
			{
				var info = meshRenderList[i];
				guiShaderProg.ApplyColor = info.hasColor ? 1 : 0;
				guiShaderProg.Tex2d2D = info.textureId;
				rapi.RenderMesh(info.handle);

				//rapi.Render2DTexture(info.handle, info.textureId, posX, posY, scaleX, scaleY, posZ);

				if(i == 0 && hoverIndex.HasValue)
				{
					(draftHover ??= new(this)).TryRender(guiShaderProg, hoverIndex.Value);
				}
			}

			game.GlPopMatrix();
		}

		private void ClampOffset()
		{
			var zoomLevel = this.zoomLevel * ClientSettings.GUIScale;
			var width = dialog.CanvasWidth * zoomLevel;
			var height = dialog.CanvasHeight * zoomLevel;
			var clampWidth = Bounds.InnerWidth * 0.5 + Math.Max(0, width - Bounds.InnerWidth);
			var clampHeight = Bounds.InnerHeight * 0.5 + Math.Max(0, height - Bounds.InnerHeight);
			areaOffset.X = Math.Clamp(areaOffset.X - width * 0.5, -clampWidth, clampWidth - width) + width * 0.5;
			areaOffset.Y = Math.Clamp(areaOffset.Y - height * 0.5, -clampHeight, clampHeight - height) + height * 0.5;
		}

		private static void BuildBacking(MeshData mesh, float xOffset, float yOffset, int width, int height, BitArray bits, int color)
		{
			int bitOffset = 0;
			for(int y = 0; y <= height; y++)
			{
				for(int x = 0; x <= width; x++)
				{
					bool hasPrevX = x != 0;
					bool hasNextX = x != width;
					bool hasPrevY = y != 0;
					bool hasNextY = y != height;

					int mask = 0;
					if(hasPrevX && hasPrevY && bits[bitOffset - width + x - 1]) mask |= 1;
					if(hasPrevX && hasNextY && bits[bitOffset + x - 1]) mask |= 2;
					if(hasNextX && hasPrevY && bits[bitOffset - width + x]) mask |= 4;
					if(hasNextX && hasNextY && bits[bitOffset + x]) mask |= 8;

					if(mask == 0) continue;
					mask--;

					var (uOffset, vOffset) = uvOffsets[mask];
					AddQuad(
						mesh,
						xOffset + x - 0.5f,
						yOffset + y - 0.5f,
						xOffset + x + 0.5f,
						yOffset + y + 0.5f,
						uOffset,
						vOffset,
						uOffset + UV_QUARTER,
						vOffset + UV_QUARTER,
						color
					);
				}
				bitOffset += width;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void AddRotatedQuad(MeshData mesh, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, int rotation)
		{
			float w = u1 - u0;
			float h = v1 - v0;

			u0 += UV_MARGIN * w;
			v0 += UV_MARGIN * h;
			u1 -= UV_MARGIN * 2 * w;
			v1 -= UV_MARGIN * 2 * h;

			int vertIndex = mesh.VerticesCount;
			switch(rotation)
			{
				case 0:
					mesh.AddVertex(x0, y0, 0, u0, v0);
					mesh.AddVertex(x1, y0, 0, u1, v0);
					mesh.AddVertex(x1, y1, 0, u1, v1);
					mesh.AddVertex(x0, y1, 0, u0, v1);
					break;
				case 1:
					mesh.AddVertex(x0, y0, 0, u0, v1);
					mesh.AddVertex(x1, y0, 0, u0, v0);
					mesh.AddVertex(x1, y1, 0, u1, v0);
					mesh.AddVertex(x0, y1, 0, u1, v1);
					break;
				case 2:
					mesh.AddVertex(x0, y0, 0, u1, v1);
					mesh.AddVertex(x1, y0, 0, u0, v1);
					mesh.AddVertex(x1, y1, 0, u0, v0);
					mesh.AddVertex(x0, y1, 0, u1, v0);
					break;
				case 3:
					mesh.AddVertex(x0, y0, 0, u1, v0);
					mesh.AddVertex(x1, y0, 0, u1, v1);
					mesh.AddVertex(x1, y1, 0, u0, v1);
					mesh.AddVertex(x0, y1, 0, u0, v0);
					break;
			}

			mesh.AddIndices(vertIndex, vertIndex + 1, vertIndex + 2, vertIndex, vertIndex + 2, vertIndex + 3);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void AddQuad(MeshData mesh, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1)
		{
			float w = u1 - u0;
			float h = v1 - v0;

			u0 += UV_MARGIN * w;
			v0 += UV_MARGIN * h;
			u1 -= UV_MARGIN * 2 * w;
			v1 -= UV_MARGIN * 2 * h;

			int vertIndex = mesh.VerticesCount;
			mesh.AddVertex(x0, y0, 0, u0, v0);
			mesh.AddVertex(x1, y0, 0, u1, v0);
			mesh.AddVertex(x1, y1, 0, u1, v1);
			mesh.AddVertex(x0, y1, 0, u0, v1);

			mesh.AddIndices(vertIndex, vertIndex + 1, vertIndex + 2, vertIndex, vertIndex + 2, vertIndex + 3);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void AddQuad(MeshData mesh, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, int color)
		{
			float w = u1 - u0;
			float h = v1 - v0;

			u0 += UV_MARGIN * w;
			v0 += UV_MARGIN * h;
			u1 -= UV_MARGIN * 2 * w;
			v1 -= UV_MARGIN * 2 * h;

			int vertIndex = mesh.VerticesCount;
			mesh.AddVertex(x0, y0, 0, u0, v0, color);
			mesh.AddVertex(x1, y0, 0, u1, v0, color);
			mesh.AddVertex(x1, y1, 0, u1, v1, color);
			mesh.AddVertex(x0, y1, 0, u0, v1, color);

			mesh.AddIndices(vertIndex, vertIndex + 1, vertIndex + 2, vertIndex, vertIndex + 2, vertIndex + 3);
		}

		private class DraftHover : IDisposable
		{
			private static readonly int backingColor = ColorUtil.ColorFromRgba(new[] { 23.0 / 85.0, 52.0 / 255.0, 12.0 / 85.0, 0.8 });

			private readonly ICoreClientAPI capi;
			private readonly GuiElementDraftCanvas canvas;
			private readonly List<(int x, int y, DraftShape shape, int rotation)> shapes;

			private readonly MeshData backingMesh;
			private MeshRef handle = null!;

			private bool isDirty = true;
			private (int x, int y) prevIndex = default;
			private bool hasShape = false;

			public DraftHover(GuiElementDraftCanvas canvas)
			{
				this.canvas = canvas;
				capi = canvas.api;
				shapes = canvas.shapes;

				backingMesh = new MeshData(24, 36, false, true, true, false);
			}

			public void SetDirty()
			{
				isDirty = true;
				hasShape = false;
			}

			public void TryRender(ShaderProgramGui guiShaderProg, (int x, int y) index)
			{
				if(isDirty || prevIndex != index)
				{
					prevIndex = index;
					UpdateHoverShape(index);
				}

				if(hasShape)
				{
					guiShaderProg.ApplyColor = 1;
					capi.Render.GetOrLoadTexture(gridTexturePath, ref canvas.gridTexture);
					guiShaderProg.Tex2d2D = canvas.gridTexture.TextureId;

					capi.Render.RenderMesh(handle);
				}
			}

			private void UpdateHoverShape((int x, int y) index)
			{
				hasShape = false;
				foreach(var info in shapes)
				{
					if(index.x >= info.x && index.y >= info.y)
					{
						var offset = (x: index.x - info.x, y: index.y - info.y);
						foreach(var cell in info.shape.Cells)
						{
							if(cell.X == offset.x && cell.Y == offset.y)
							{
								hasShape = true;
								GenHoverShape(info);
								return;
							}
						}
					}
				}
			}

			private void GenHoverShape((int x, int y, DraftShape shape, int rotation) info)
			{
				var bits = new BitArray(info.shape.Width * info.shape.Height);
				foreach(var cell in info.shape.Cells)
				{
					bits[cell.X + cell.Y * info.shape.Width] = true;
				}

				backingMesh.Clear();
				BuildBacking(backingMesh, info.x, info.y, info.shape.Width, info.shape.Height, bits, backingColor);
				handle?.Dispose();
				handle = capi.Render.UploadMesh(backingMesh);
			}

			public void Dispose()
			{
				handle?.Dispose();
				handle = null!;
			}
		}

		private class DraftDrag : IDisposable
		{
			public int Rotation;
			public (int x, int y) Size => (shape.Width, shape.Height);

			private readonly GuiElementDraftCanvas canvas;

			private DraftShape shape;

			private readonly List<(int textureId, MeshRef handle, bool hasColor)> meshRenderList = new();
			private bool isDirty = true;
			private float mouseX, mouseY;

			public DraftDrag(GuiElementDraftCanvas canvas, DraftShape shape, int rotation)
			{
				this.canvas = canvas;
				this.shape = shape;
				Rotation = rotation;
			}

			public void Render(float scaleX, float scaleY, float offset)
			{
				if(isDirty)
				{
					isDirty = false;
					UpdateMesh();
				}

				canvas.RenderLayers(meshRenderList, mouseX - offset, mouseY - offset, 200 + canvas.RenderZ, scaleX, scaleY);
			}

			public void Rotate()
			{
				isDirty = true;
				shape = shape.Rotate(1);
				Rotation = (Rotation + 1) & 3;
			}

			public void UpdateDrag(float x, float y)
			{
				mouseX = x;
				mouseY = y;
			}

			public void Dispose()
			{
				foreach(var info in meshRenderList)
				{
					info.handle.Dispose();
				}
			}

			private void UpdateMesh()
			{
				float x = -shape.Width / 2f;
				float y = -shape.Height / 2f;

				var backingMesh = new MeshData(24, 36, false, true, true, false);
				var connectionBatches = new Dictionary<int, MeshData>();
				var bits = new BitArray(shape.Width * shape.Height);
				foreach(var cell in shape.Cells)
				{
					bits[cell.X + cell.Y * shape.Width] = true;
					if(cell.Connections == null) continue;

					float px = x + cell.X;
					float py = y + cell.Y;

					if(cell.Connections[(int)DraftShape.ConnectionSide.Up] != default)
					{
						AddConnection(connectionBatches, cell.Connections[(int)DraftShape.ConnectionSide.Up]!, px, py - NON_CONNECTED_OFFSET);
					}
					if(cell.Connections[(int)DraftShape.ConnectionSide.Down] != default)
					{
						AddConnection(connectionBatches, cell.Connections[(int)DraftShape.ConnectionSide.Down]!, px, py + NON_CONNECTED_OFFSET);
					}
					if(cell.Connections[(int)DraftShape.ConnectionSide.Left] != default)
					{
						AddConnection(connectionBatches, cell.Connections[(int)DraftShape.ConnectionSide.Left]!, px - NON_CONNECTED_OFFSET, py);
					}
					if(cell.Connections[(int)DraftShape.ConnectionSide.Right] != default)
					{
						AddConnection(connectionBatches, cell.Connections[(int)DraftShape.ConnectionSide.Right]!, px + NON_CONNECTED_OFFSET, py);
					}
				}

				BuildBacking(backingMesh, x, y, shape.Width, shape.Height, bits, (int)shape.Color);

				int renderCounter = 0;
				var rapi = canvas.api.Render;
				rapi.GetOrLoadTexture(draftTexturePath, ref canvas.draftTexture);
				UpsertMesh(renderCounter++, canvas.draftTexture.TextureId, backingMesh, true);

				var texPos = canvas.GetDraftSprite(shape.Texture);
				var spriteMesh = new MeshData(4, 6, false, true, false, false);
				AddRotatedQuad(
					spriteMesh,
					x,
					y,
					x + shape.Width,
					y + shape.Height,
					texPos.x1,
					texPos.y1,
					texPos.x2,
					texPos.y2,
					Rotation
				);
				UpsertMesh(renderCounter++, texPos.atlasTextureId, spriteMesh, false);

				foreach(var pair in connectionBatches)
				{
					UpsertMesh(renderCounter++, pair.Key, pair.Value, false);
				}
			}

			private void AddConnection(Dictionary<int, MeshData> connectionBatches, (AssetLocation type, bool isOutput) connectionInfo, float px, float py)
			{
				var texPos = canvas.assets.GetConnectionTexture(connectionInfo.type, connectionInfo.isOutput ? ConnectionTextureType.Output : ConnectionTextureType.Input);
				if(!connectionBatches.TryGetValue(texPos.atlasTextureId, out var mesh))
				{
					mesh = new MeshData(24, 36, false, true, false, false);
					connectionBatches[texPos.atlasTextureId] = mesh;
				}
				AddQuad(
					mesh,
					px,
					py,
					px + 1,
					py + 1,
					texPos.x1,
					texPos.y1,
					texPos.x2,
					texPos.y2
				);
			}

			private void UpsertMesh(int index, int textureId, MeshData mesh, bool hasColor)
			{
				var rapi = canvas.api.Render;
				if(index == meshRenderList.Count)
				{
					meshRenderList.Add((textureId, rapi.UploadMesh(mesh), hasColor));
				}
				else
				{
					rapi.UpdateMesh(meshRenderList[index].handle, mesh);
					meshRenderList[index] = (textureId, meshRenderList[index].handle, hasColor);
				}
			}
		}
	}
}
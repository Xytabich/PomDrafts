using PowerOfMind.Drafts.GUI;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace PowerOfMind.Drafts.Blocks.Entities
{
	public class BlockEntityDraftingTable : BlockEntity
	{
		public const int PACKET_DRAFT_LIST = 2000;
		public const int PACKET_ADD_DRAFT = 2001;
		public const int PACKET_DEL_DRAFT = 2002;
		public const int PACKET_CLR_DRAFTS = 2003;

		private static readonly float[] quadPoses = { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0 };
		private static readonly float[] quadUvs = { 0, 0, 1, 0, 1, 1, 0, 1 };

		private GuiDialogDraftingTable? guiDialog;
		private readonly InventoryDraftingTable inventory;
		private Dictionary<(int x, int y), (AssetLocation code, int rotation)> drafts = default!;
		private DraftInfo[]? renderDrafts = null;
		private HashSet<(int x, int y)> occupiedCells = default!;
		private DraftsModSystem draftsSystem = default!;
		private (int x, int y) areaSize;

		public BlockEntityDraftingTable()
		{
			inventory = new InventoryDraftingTable(16, null, null);
			inventory.SlotModified += OnSlotModifid;
		}

		public override void Initialize(ICoreAPI api)
		{
			base.Initialize(api);
			draftsSystem = api.ModLoader.GetModSystem<DraftsModSystem>();

			var block = (BlockDraftingTable)Block;
			areaSize = (block.HorizontalTiles, block.VerticalTiles);

			inventory.LateInitialize("pom:drafttable-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

			if(api.Side == EnumAppSide.Server)
			{
				if(drafts == null)
				{
					drafts = new();
					occupiedCells = new();
				}
				else
				{
					RefreshDrafts();
				}
			}
			else
			{
				if(drafts != null)
				{
					UpdateRenderDrafts(drafts);
					drafts = null!;
				}
			}
		}

		public bool OnPlayerInteract(IPlayer byPlayer, BlockSelection blockSel)
		{
			if(Api.Side == EnumAppSide.Client)
			{
				ToggleInventoryDialogClient(byPlayer);
			}
			return true;
		}

		public override void OnBlockUnloaded()
		{
			base.OnBlockUnloaded();
			if(guiDialog != null && guiDialog.IsOpened())
			{
				guiDialog?.TryClose();
			}
			guiDialog?.Dispose();
		}

		public override void OnBlockRemoved()
		{
			base.OnBlockRemoved();
			if(guiDialog != null && guiDialog.IsOpened())
			{
				guiDialog?.TryClose();
			}
			guiDialog?.Dispose();
		}

		public override void OnBlockBroken(IPlayer? byPlayer = null)
		{
			base.OnBlockBroken(byPlayer);
			inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
		}

		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
		{
			base.FromTreeAttributes(tree, world);
			inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
			if(world.Api.Side == EnumAppSide.Server)
			{
				if(drafts == null)
				{
					drafts = new();
					occupiedCells = new();
				}
				else
				{
					drafts.Clear();
					occupiedCells.Clear();
				}
				if(tree.HasAttribute("pom:drafts"))
				{
					try
					{
						foreach(var pair in SerializerUtil.Deserialize<Dictionary<(int x, int y), (AssetLocation code, int rotation)>>(tree.GetBytes("pom:drafts")))
						{
							drafts[pair.Key] = pair.Value;
						}
						if(Api != null)
						{
							RefreshDrafts();
						}
					}
					catch(Exception e)
					{
						world.Logger.Error(e);
					}
				}
			}
			else
			{
				if(tree.HasAttribute("pom:drafts"))
				{
					try
					{
						var drafts = SerializerUtil.Deserialize<Dictionary<(int x, int y), (AssetLocation code, int rotation)>>(tree.GetBytes("pom:drafts"));
						if(Api == null)
						{
							this.drafts = drafts;
						}
						else
						{
							UpdateRenderDrafts(drafts);
						}
					}
					catch(Exception e)
					{
						world.Logger.Error(e);
					}
				}
			}
		}

		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			base.ToTreeAttributes(tree);
			tree.SetBytes("pom:drafts", SerializerUtil.Serialize(drafts));
			inventory.ToTreeAttributes(tree.GetOrAddTreeAttribute("inventory"));
		}

		public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
		{
			var toRender = renderDrafts;
			if(toRender != null)
			{
				var capi = (ICoreClientAPI)Api;
				var shape = Block.Shape;
				var resolvedShape = Shape.TryGet(capi, shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
				resolvedShape.ResolveReferences(capi.Logger, "pom:draftingtable");
				var point = resolvedShape.AttachmentPointsByCode["DraftCenter"];
				var elems = point.ParentElement.GetParentPath();

				var localTransform = Mat4f.Create();
				var modelTransform = Mat4f.Create();
				for(int i = 0; i < elems.Count; i++)
				{
					localTransform = elems[i].GetLocalTransformMatrix(0, Mat4f.Identity(localTransform));
					Mat4f.Mul(modelTransform, modelTransform, localTransform);
				}
				Mat4f.Mul(modelTransform, modelTransform, point.ParentElement.GetLocalTransformMatrix(0, Mat4f.Identity(localTransform)));
				Mat4f.Identity(localTransform);
				Mat4f.Translate(localTransform, localTransform, (float)(point.PosX / 16.0), (float)(point.PosY / 16.0), (float)(point.PosZ / 16.0));
				Mat4f.RotateX(localTransform, localTransform, (float)point.RotationX * (MathF.PI / 180f));
				Mat4f.RotateY(localTransform, localTransform, (float)point.RotationY * (MathF.PI / 180f));
				Mat4f.RotateZ(localTransform, localTransform, (float)point.RotationZ * (MathF.PI / 180f));
				Mat4f.Mul(modelTransform, modelTransform, localTransform);

				var canvasMesh = new MeshData(24, 36).WithColorMaps().WithRenderpasses().WithXyzFaces();
				Span<float> uvs = stackalloc float[8];
				var subPixelPadding = (capi.BlockTextureAtlas.SubPixelPaddingX, capi.BlockTextureAtlas.SubPixelPaddingY);
				var facing = BlockFacing.NORTH;
				var faceIndex = facing.MeshDataIndex;
				var faceFlags = facing.NormalPackedFlags;
				var offset = (x: areaSize.x * 0.5f, y: areaSize.y * 0.5f);
				const float scale = 1f / 16f;
				foreach(var info in toRender)
				{
					int vertIndex = canvasMesh.VerticesCount;
					GetRotatedUVs(info.Texture, info.Rotation, subPixelPadding, uvs);
					var size = (x: info.Width, y: info.Height);
					if((info.Rotation & 1) != 0)
					{
						size = (size.y, size.x);
					}
					for(int pi = 0, ti = 0; pi < 12; pi += 3, ti += 2)
					{
						canvasMesh.AddVertexWithFlagsSkipColor(
							(quadPoses[pi] * size.x + info.X - offset.x) * scale,
							(quadPoses[pi + 1] * size.y + info.Y - offset.y) * scale,
							0,
							uvs[ti],
							uvs[ti + 1],
							faceFlags
						);
					}

					canvasMesh.AddIndices(vertIndex, vertIndex + 1, vertIndex + 2, vertIndex, vertIndex + 2, vertIndex + 3);
					canvasMesh.AddXyzFace(faceIndex);
					canvasMesh.AddTextureId(info.Texture.atlasTextureId);
					canvasMesh.AddColorMapIndex(0, 0);
					canvasMesh.AddRenderPass((short)EnumChunkRenderPass.OpaqueNoCull);
				}

				canvasMesh.MatrixTransform(modelTransform);
				if(shape.rotateX != 0f || shape.rotateY != 0f || shape.rotateZ != 0f)
				{
					canvasMesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), shape.rotateX * (MathF.PI / 180f), shape.rotateY * (MathF.PI / 180f), shape.rotateZ * (MathF.PI / 180f));
				}
				if(shape.offsetX != 0f || shape.offsetY != 0f || shape.offsetZ != 0f)
				{
					canvasMesh.Translate(shape.offsetX, shape.offsetY, shape.offsetZ);
				}

				capi.Tesselator.TesselateBlock(Block, out var blockMesh);
				mesher.AddMeshData(blockMesh);

				mesher.AddMeshData(canvasMesh);
				return true;
			}
			return base.OnTesselation(mesher, tessThreadTesselator);
		}

		public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
		{
			if(packetid < 1000)
			{
				inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);
				Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
				return;
			}
			if(packetid == 1001)
			{
				player.InventoryManager?.CloseInventory(inventory);
			}
			if(packetid == 1000)
			{
				player.InventoryManager?.OpenInventory(inventory);
			}

			if(!Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.Use))
			{
				return;
			}
			if(packetid == PACKET_DRAFT_LIST)
			{
				((ICoreServerAPI)Api).Network.SendBlockEntityPacket((IServerPlayer)player, Pos, PACKET_DRAFT_LIST, drafts);
			}
			if(packetid == PACKET_ADD_DRAFT)
			{
				var packet = SerializerUtil.Deserialize<AddDraftCmd>(data);
				using var storage = draftsSystem.GetPlayerDrafts(player.PlayerUID, false);
				if(storage == null) return;
				if(storage.Contains(packet.DraftCode) && TryAddDraft(packet.DraftCode, (packet.X, packet.Y), packet.Rotation))
				{
					OnDraftsChanged((IServerPlayer)player);
				}
				else
				{
					((ICoreServerAPI)Api).Network.SendBlockEntityPacket((IServerPlayer)player, Pos, PACKET_DRAFT_LIST, drafts);
				}
			}
			if(packetid == PACKET_DEL_DRAFT)
			{
				if(TryRemoveDraft(SerializerUtil.Deserialize<(int, int)>(data)))
				{
					OnDraftsChanged((IServerPlayer)player);
				}
				else
				{
					((ICoreServerAPI)Api).Network.SendBlockEntityPacket((IServerPlayer)player, Pos, PACKET_DRAFT_LIST, drafts);
				}
			}
			if(packetid == PACKET_CLR_DRAFTS)
			{
				if(drafts.Count != 0)
				{
					drafts.Clear();
					occupiedCells.Clear();
					OnDraftsChanged((IServerPlayer)player);
				}
				else
				{
					((ICoreServerAPI)Api).Network.SendBlockEntityPacket((IServerPlayer)player, Pos, PACKET_DRAFT_LIST, drafts);
				}
			}
		}

		public override void OnReceivedServerPacket(int packetid, byte[] data)
		{
			if(packetid == 1001)
			{
				((IClientWorldAccessor)Api.World).Player.InventoryManager.CloseInventory(inventory);
				if(guiDialog != null && guiDialog.IsOpened())
				{
					guiDialog?.TryClose();
				}
				guiDialog?.Dispose();
				guiDialog = null;
			}
			if(packetid == PACKET_DRAFT_LIST)
			{
				var drafts = SerializerUtil.Deserialize<Dictionary<(int x, int y), (AssetLocation code, int rotation)>>(data);
				UpdateRenderDrafts(drafts);
				guiDialog?.SetDrafts(drafts);
			}
		}

		public void UpdateRenderDrafts(Dictionary<(int x, int y), (AssetLocation code, int rotation)> drafts)
		{
			if(drafts == null || drafts.Count == 0)
			{
				renderDrafts = null;
				return;
			}

			var list = new List<DraftInfo>(drafts.Count);//TODO: cache
			foreach(var pair in drafts)
			{
				var descriptor = draftsSystem.GetDescriptor(pair.Value.code);
				if(descriptor == null) continue;

				var shape = descriptor.GetDraftShape(pair.Value.code);
				list.Add(new(pair.Key.x, pair.Key.y, shape.Width, shape.Height, GetDraftSprite(shape.Texture), pair.Value.rotation));
			}
			renderDrafts = list.ToArray();

			if(Api != null)
			{
				Api.World.BlockAccessor.MarkBlockDirty(Pos);
			}
		}

		private void ToggleInventoryDialogClient(IPlayer byPlayer)
		{
			if(guiDialog == null)
			{
				var capi = (ICoreClientAPI)Api;
				guiDialog = CreateGuiDialog();
				guiDialog.OnClosed += () => {
					guiDialog = null;
					capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, 1001);
					capi.Network.SendPacketClient(inventory.Close(byPlayer));
				};
				guiDialog.TryOpen();
				capi.Network.SendPacketClient(inventory.Open(byPlayer));
				capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, 1000);
				capi.Network.SendBlockEntityPacket(Pos, PACKET_DRAFT_LIST);
			}
			else
			{
				guiDialog.TryClose();
			}
		}

		private GuiDialogDraftingTable CreateGuiDialog()
		{
			var block = (BlockDraftingTable)Block;
			return new GuiDialogDraftingTable((ICoreClientAPI)Api, Pos, inventory, block.HorizontalTiles, block.VerticalTiles);
		}

		private void OnDraftsChanged(IServerPlayer ignorePlayer)
		{
			UpdateRecipes();

			var packet = new Packet_BlockEntityMessage {
				PacketId = PACKET_DRAFT_LIST,
				X = Pos.X,
				Y = Pos.Y,
				Z = Pos.Z
			};
			packet.SetData(SerializerUtil.Serialize(drafts));

			((ICoreServerAPI)Api).Network.BroadcastArbitraryPacket(Packet_ServerSerializer.SerializeToBytes(new Packet_Server {
				Id = Packet_Server.BlockEntityMessageFieldID,
				BlockEntityMessage = packet
			}), ignorePlayer);
			Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
		}

		private bool TryAddDraft(AssetLocation draft, (int x, int y) index, int rotation)
		{
			if(index.x < 0 || index.x >= areaSize.x || index.y < 0 || index.y >= areaSize.y)
			{
				return false;
			}
			if(draftsSystem.GetDescriptor(draft) == null)
			{
				return false;
			}

			var shape = draftsSystem.GetDraftShape(draft);
			if(rotation != 0)
			{
				shape = shape.Clone().Rotate(rotation);
			}
			if(index.x + shape.Width > areaSize.x || index.y + shape.Height > areaSize.y)
			{
				return false;
			}

			foreach(var cell in shape.Cells)
			{
				if(occupiedCells.Contains((cell.X + index.x, cell.Y + index.y)))
				{
					return false;
				}
			}

			drafts[index] = (draft, rotation);

			foreach(var cell in shape.Cells)
			{
				occupiedCells.Add((cell.X + index.x, cell.Y + index.y));
			}

			return true;
		}

		private bool TryRemoveDraft((int x, int y) index)
		{
			if(drafts.Remove(index, out var info))
			{
				var shape = draftsSystem.GetDraftShape(info.code);
				if(info.rotation != 0)
				{
					shape = shape.Clone().Rotate(info.rotation);
				}
				foreach(var cell in shape.Cells)
				{
					occupiedCells.Remove((cell.X + index.x, cell.Y + index.y));
				}
				return true;
			}
			return false;
		}

		private void RefreshDrafts()
		{
			List<(int, int)>? toRemove = null;//TODO: cached collection
			foreach(var pair in drafts)
			{
				if(draftsSystem.GetDescriptor(pair.Value.code) == null)
				{
					(toRemove ??= new()).Add(pair.Key);
				}
			}
			if(toRemove != null)
			{
				foreach(var key in toRemove)
				{
					drafts.Remove(key);
				}
				MarkDirty();
			}

			UpdateRecipes();

			foreach(var pair in drafts)
			{
				var shape = draftsSystem.GetDraftShape(pair.Value.code);
				if(pair.Value.rotation != 0)
				{
					shape = shape.Clone().Rotate(pair.Value.rotation);
				}
				var index = pair.Key;
				foreach(var cell in shape.Cells)
				{
					occupiedCells.Add((cell.X + index.x, cell.Y + index.y));
				}
			}
		}

		private TextureAtlasPosition GetDraftSprite(AssetLocation texture)
		{
			var capi = (ICoreClientAPI)Api;
			if(capi.BlockTextureAtlas.GetOrInsertTexture(texture, out _, out var texPos, () => {
				var asset = capi.Assets.TryGet(texture.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
				return asset?.ToBitmap(capi);
			}) == false)
			{
				texPos = capi.BlockTextureAtlas.UnknownTexturePosition;
			}
			return texPos;
		}

		private void UpdateRecipes()
		{
			inventory.Recipes.Clear();
			draftsSystem.FindMatchingRecipes(drafts, inventory.Recipes);
			inventory.FindMatchingRecipe();
		}

		private void OnSlotModifid(int slotId)
		{
			if(slotId == 16) return;
			MarkDirty(true);
		}

		private static void GetRotatedUVs(TextureAtlasPosition texPos, int rotation, (float x, float y) subPixelPadding, Span<float> uvs)
		{
			var texcoords = (x1: texPos.x1 + subPixelPadding.x, y1: texPos.y1 + subPixelPadding.y, x2: texPos.x2 - subPixelPadding.x, y2: texPos.y2 - subPixelPadding.y);
			int targetIndex = rotation * 2;
			for(int i = 0; i < 8; i += 2)
			{
				uvs[targetIndex] = texcoords.x1 + quadUvs[i] * (texcoords.x2 - texcoords.x1);
				uvs[targetIndex + 1] = texcoords.y1 + quadUvs[i + 1] * (texcoords.y2 - texcoords.y1);
				targetIndex = (targetIndex + 2) & 7;
			}
		}

		private record struct DraftInfo(int X, int Y, int Width, int Height, TextureAtlasPosition Texture, int Rotation) { }

		[ProtoContract]
		public class AddDraftCmd
		{
			[ProtoMember(1)]
			public int X;
			[ProtoMember(2)]
			public int Y;
			[ProtoMember(3)]
			public int Rotation;

			[ProtoMember(4)]
			public AssetLocation DraftCode = default!;
		}
	}
}
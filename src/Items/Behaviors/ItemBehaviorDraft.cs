using Newtonsoft.Json;
using PowerOfMind.Drafts.Common;
using System.Drawing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Drafts.Items.Behaviors
{
	/// <summary>
	/// Marks an item as a draft and provides its shape.
	/// The code of an item marked with this behavior will be registered in the draft list.
	/// </summary>
	public class ItemBehaviorDraft : CollectibleBehavior, IDraftsDescriptor
	{
		private bool isValid = false;

		private DraftShape shape;
		private string[] groups = Array.Empty<string>();
		private bool isInteractive = false;

		private JsonObject properties = default!;

		public ItemBehaviorDraft(CollectibleObject collObj) : base(collObj)
		{
		}

		public void InitDescriptor(ICollection<AssetLocation>? connectionTypes)
		{
			try
			{
				var info = properties.AsObject<DraftInfo>(null!, collObj.Code.Domain);
				isInteractive = info.Interactive;

				var cells = new List<DraftShape.ShapeCell>();//TODO: cached list (poolable reflist or collections cache utility)
				int height = info.Shape.Length;
				int maxWidth = 0;
				for(int y = 0; y < height; y++)
				{
					var line = info.Shape[y];
					int width = line.Length;
					maxWidth = Math.Max(width, maxWidth);
					for(int x = 0; x < width; x++)
					{
						if(info.Cells.TryGetValue(line[x], out var cell))
						{
							var inputDict = cell?.Inputs;
							var outputDict = cell?.Outputs;

							(AssetLocation? type, bool isOutput)[]? connections = null;

							if(inputDict != null || outputDict != null)
							{
								connections = new (AssetLocation? type, bool isOutput)[4];

								if(inputDict != null && inputDict.Count != 0)
								{
									foreach(var pair in inputDict)
									{
										if(connectionTypes != null && !connectionTypes.Contains(pair.Value!))
										{
											throw new Exception($"Connection type {pair.Value} not found for '{collObj.Code}''");
										}

										connections[(int)pair.Key] = (pair.Value, false);
									}
								}

								if(outputDict != null && outputDict.Count != 0)
								{
									foreach(var pair in outputDict)
									{
										if(connectionTypes != null && !connectionTypes.Contains(pair.Value!))
										{
											throw new Exception($"Connection type {pair.Value} not found for '{collObj.Code}''");
										}

										connections[(int)pair.Key] = (pair.Value, true);
									}

									if(inputDict != null && inputDict.Count != 0)
									{
										foreach(var pair in inputDict)
										{
											if(outputDict.ContainsKey(pair.Key))
											{
												throw new Exception($"Input/output collision detected for draft '{collObj.Code}'");
											}
										}
									}
								}
							}

							cells.Add(new DraftShape.ShapeCell(x, y, connections));
						}
					}
				}
				if(cells.Count == 0)
				{
					throw new Exception($"Missing shape for draft '{collObj.Code}'");
				}
				shape = new DraftShape(cells.ToArray(), maxWidth, height, info.Texture, ParseColor(info.Color));

				if(info.Groups != null)
				{
					groups = info.Groups.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => string.Intern(g!.ToLowerInvariant())).ToArray();
				}
			}
			catch
			{
				propertiesAtString = "{}";
				throw;
			}

			isValid = true;
		}

		public override void Initialize(JsonObject properties)
		{
			this.properties = properties;
			base.Initialize(properties);
		}

		public IEnumerable<AssetLocation> EnumerateDraftCodes()
		{
			if(isValid) yield return collObj.Code;
		}

		public string[] GetDraftGroups(AssetLocation draftCode)
		{
			return groups;
		}

		public DraftShape GetDraftShape(AssetLocation draftCode)
		{
			return shape;
		}

		public bool IsInteractableDraft(AssetLocation draftCode)
		{
			return isInteractive;
		}

		public ItemStack CreateDummyStack(AssetLocation draftCode)
		{
			return new ItemStack(collObj);
		}

		private static uint ParseColor(string? str)
		{
			try
			{
				if(!string.IsNullOrWhiteSpace(str))
				{
					var c = ColorTranslator.FromHtml(str.Trim());
					return (uint)ColorUtil.ColorFromRgba(c.R, c.G, c.B, c.A);
				}
			}
			catch { }
			return uint.MaxValue;
		}

		private class DraftInfo
		{
			[JsonProperty(Required = Required.Always)]
			public string[] Shape = default!;
			[JsonProperty(Required = Required.Always)]
			public Dictionary<char, CellInfo?> Cells = default!;
			[JsonProperty(Required = Required.Always)]
			public AssetLocation Texture = default!;
			public string? Color = null;
			public string?[]? Groups = null;
			public bool Interactive = false;

			public class CellInfo
			{
				public Dictionary<Side, AssetLocation?>? Inputs = null;
				public Dictionary<Side, AssetLocation?>? Outputs = null;
			}
		}

		private enum Side
		{
			Up = 0,
			U = Up,
			T = Up,
			Top = Up,

			Right = 1,
			R = Right,

			Down = 2,
			D = Down,
			B = Down,
			Bottom = Down,

			Left = 3,
			L = Left,
		}
	}
}
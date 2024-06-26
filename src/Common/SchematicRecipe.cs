using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace PowerOfMind.Drafts.Common
{
	/// <summary>
	/// Represents a recipe based on a schematic composed of parts (drafts).
	/// The parts must be registered in the player's draftsman folder, if any of them are missing the recipe cannot be crafted.
	/// </summary>
	public class SchematicRecipe : IRecipeBase<SchematicRecipe>, IByteSerializable
	{
		public bool Enabled { get; set; } = true;

		public int RecipeId;

		public AssetLocation Name { get; set; } = default!;

		[JsonProperty(Required = Required.Always)]
		public JsonItemStack Output = default!;

		[JsonProperty(Required = Required.DisallowNull)]
		public SchematicRecipeIngredient[] Ingredients = Array.Empty<SchematicRecipeIngredient>();

		[JsonIgnore]
		public NodeInfo[] SortedNodes = default!;
		[JsonIgnore]
		public ConnectionInfo[] SortedConnections = default!;

		/// <summary>
		/// A list of parts and their connections from which the schematic is made.
		/// Required only for recipe parsing, and is set to null after <see cref="Resolve"/>.
		/// </summary>
		[JsonProperty(Required = Required.Always)]
		private Dictionary<string, PatternPart> pattern = default!;

		IRecipeIngredient[] IRecipeBase<SchematicRecipe>.Ingredients => Ingredients;
		IRecipeOutput IRecipeBase<SchematicRecipe>.Output => Output;

		public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
		{
			var mappings = new Dictionary<string, string[]>();
			if(Ingredients.Length == 0)
			{
				return mappings;
			}
			var ingredients = Ingredients;
			var codes = new List<string>();
			foreach(var ingred in ingredients)
			{
				if(!ingred.Code.Path.Contains('*'))
				{
					continue;
				}
				codes.Clear();
				int wildcardStartLen = ingred.Code.Path.IndexOf('*');
				int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;
				if(ingred.Type == EnumItemClass.Block)
				{
					FindCodepart(world.Blocks, ingred.Code, wildcardStartLen, wildcardEndLen, ingred.AllowedVariants, codes);
				}
				else
				{
					FindCodepart(world.Items, ingred.Code, wildcardStartLen, wildcardEndLen, ingred.AllowedVariants, codes);
				}
				if(string.IsNullOrEmpty(ingred.Name))
				{
					ingred.Name = "wildcard" + mappings.Count;
				}
				mappings[ingred.Name] = codes.ToArray();
			}
			return mappings;
		}

		public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
		{
			bool ok = true;
			for(int i = 0; i < Ingredients.Length; i++)
			{
				if(Ingredients[i].Resolve(world, sourceForErrorLogging))
				{
					if(Ingredients[i].IsWildCard)
					{
						world.Logger.Warning("[pomdrafts] Draft recipe {0}, ingredient {1} is wildcard", sourceForErrorLogging, i);
					}
					else
					{
						continue;
					}
				}

				ok = false;
			}
			ok &= Output.Resolve(world, sourceForErrorLogging);

			if(pattern.Count == 0)
			{
				ok = false;
				world.Logger.Warning("[pomdrafts] Draft recipe {0}, pattern is empty", sourceForErrorLogging);
			}
			if(ok)
			{
				var assets = world.Api.ModLoader.GetModSystem<DraftAssetsSystem>();
				var conns = assets.ConnectionTypes;
				var drafts = assets.DraftDescriptors;
				foreach(var part in pattern)
				{
					if(!drafts.ContainsKey(part.Value.Code))
					{
						ok = false;
						world.Logger.Warning("[pomdrafts] Draft recipe {0}, part {1} refers to a non-existent draft {2}", sourceForErrorLogging, part.Key, part.Value.Code);
						continue;
					}
					if(part.Value.Outputs != null)
					{
						var outputs = part.Value.Outputs;
						for(int i = outputs.Length - 1; i >= 0; i--)
						{
							var output = outputs[i];
							if(string.IsNullOrEmpty(output.Target) || !pattern.ContainsKey(output.Target))
							{
								ok = false;
								world.Logger.Warning("[pomdrafts] Draft recipe {0}, part {1}, output {2} targets unknown part {3}", sourceForErrorLogging, part.Key, i, output.Target);
							}
							if(!conns.ContainsKey(output.Type))
							{
								ok = false;
								world.Logger.Warning("[pomdrafts] Draft recipe {0}, part {1}, output {2} uses unknown connection type {3}", sourceForErrorLogging, part.Key, i, output.Type);
							}
						}
					}
				}
				if(ok)
				{
					InitSortedFields();

					pattern = null!;
				}
			}
			return ok;
		}

		public void ToBytes(BinaryWriter writer)
		{
			writer.Write(RecipeId);
			writer.Write(Enabled);
			writer.Write(Name.ToShortString());
			writer.Write(Ingredients.Length);
			foreach(var ingred in Ingredients)
			{
				ingred.ToBytes(writer);
			}
			Output.ToBytes(writer);

			var codeToIndex = new Dictionary<AssetLocation, int>();//TODO: cache collection
			int len = SortedNodes.Length;
			writer.Write(len);
			for(int i = 0; i < len; i++)
			{
				var info = SortedNodes[i];
				writer.Write(info.DraftCode.ToShortString());
				writer.Write(info.OutputCount);
				codeToIndex[info.DraftCode] = i;
			}
			writer.Write(SortedConnections.Length);
			foreach(var info in SortedConnections)
			{
				writer.Write(codeToIndex[info.From]);
				writer.Write(codeToIndex[info.To]);
				writer.Write(info.Type.ToShortString());
			}
		}

		public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
		{
			RecipeId = reader.ReadInt32();
			Enabled = reader.ReadBoolean();
			Name = AssetLocation.Create(reader.ReadString());
			int count = reader.ReadInt32();
			Ingredients = new SchematicRecipeIngredient[count];
			for(int i = 0; i < count; i++)
			{
				Ingredients[i] = new SchematicRecipeIngredient();
				Ingredients[i].FromBytes(reader, resolver);
			}
			Output = new JsonItemStack();
			Output.FromBytes(reader, resolver.ClassRegistry);
			Output.Resolve(resolver, "[Draft recipe FromBytes]");

			count = reader.ReadInt32();
			SortedNodes = new NodeInfo[count];
			for(int i = 0; i < count; i++)
			{
				var code = AssetLocation.Create(reader.ReadString());
				int outCount = reader.ReadInt32();
				SortedNodes[i] = new NodeInfo(code, outCount);
			}
			count = reader.ReadInt32();
			SortedConnections = new ConnectionInfo[count];
			for(int i = 0; i < count; i++)
			{
				var from = SortedNodes[reader.ReadInt32()].DraftCode;
				var to = SortedNodes[reader.ReadInt32()].DraftCode;
				var type = AssetLocation.Create(reader.ReadString());
				SortedConnections[i] = new ConnectionInfo(from, to, type);
			}
		}

		public SchematicRecipe Clone()
		{
			return new SchematicRecipe() {
				Output = Output.Clone(),
				Enabled = Enabled,
				Name = Name,
				RecipeId = RecipeId,
				Ingredients = Array.ConvertAll(Ingredients, i => i.Clone()),
				pattern = pattern
			};
		}

		public void InitRawPatternFromDrafts(DraftsModSystem draftsSystem, IReadOnlyDictionary<(int x, int y), (AssetLocation code, int rotation)> drafts)
		{
			pattern = new();

			var posToName = new Dictionary<(int x, int y), string>();
			var nameCounter = new Dictionary<char, int>();
			var connSlots = new Dictionary<(int x, int y, int dir), (AssetLocation type, AssetLocation draft, bool isOutput, (int, int) draftKey)>();//TODO: cache collection
			foreach(var pair in drafts)
			{
				var c = pair.Value.code.Path[0];
				if(!nameCounter.TryGetValue(c, out var count))
				{
					count = 0;
				}
				nameCounter[c] = count + 1;

				var part = new PatternPart() { Code = pair.Value.code };
				var name = $"{c}{count}";
				pattern[name] = part;
				posToName[pair.Key] = name;

				var index = pair.Key;
				var rotation = pair.Value.rotation;
				var code = pair.Value.code;
				var shape = draftsSystem.GetDraftShape(code);

				var xMax = shape.Width - 1;
				var yMax = shape.Height - 1;

				foreach(var cell in shape.Cells)
				{
					if(cell.Connections == null) continue;

					var offset = Utils.RotateOffset(cell.X, cell.Y, xMax, yMax, rotation);
					offset.x += index.x;
					offset.y += index.y;
					for(int j = 0; j < 4; j++)
					{
						var info = cell.Connections[j];
						if(info != default)
						{
							connSlots[(offset.x, offset.y, (j + rotation) & 3)] = (info.type!, code, info.isOutput, pair.Key);
						}
					}
				}
			}

			foreach(var pair in connSlots)
			{
				if(pair.Value.isOutput)
				{
					var (x, y) = Utils.OffsetByDirection(pair.Key.x, pair.Key.y, pair.Key.dir);
					if(connSlots.TryGetValue((x, y, (pair.Key.dir + 2) & 3), out var target) && !target.isOutput)
					{
						if(target.type.Equals(pair.Value.type))
						{
							var part = pattern[posToName[pair.Value.draftKey]];
							var output = new OutputInfo() {
								Target = posToName[target.draftKey],
								Type = target.type
							};
							if(part.Outputs == null)
							{
								part.Outputs = new OutputInfo[] { output };
							}
							else
							{
								part.Outputs = part.Outputs.Append(output);
							}
						}
					}
				}
			}
		}

		private void InitSortedFields()
		{
			SortedNodes = new NodeInfo[pattern.Count];

			int index = 0;
			int connCounter = 0;
			foreach(var pair in pattern)
			{
				int outLen = pair.Value.Outputs?.Length ?? 0;
				SortedNodes[index] = new NodeInfo(pair.Value.Code, outLen);
				connCounter += outLen;
				index++;
			}
			Array.Sort(SortedNodes);

			SortedConnections = new ConnectionInfo[connCounter];

			index = 0;
			foreach(var pair in pattern)
			{
				if(pair.Value.Outputs == null) continue;
				foreach(var output in pair.Value.Outputs)
				{
					SortedConnections[index] = new ConnectionInfo(pair.Value.Code, pattern[output.Target].Code, output.Type);
					index++;
				}
			}
			Array.Sort(SortedConnections);
		}

		private static void FindCodepart<T>(IList<T> list, AssetLocation ingredCode,//TODO: move to corelib
			int wildcardStart, int wildcardEnd, string[]? allowedVariants, List<string> outCodes) where T : CollectibleObject
		{
			Parallel.ForEach(list, () => new List<string>(), (obj, _, state) => {//TODO: cached collection
				if(!(obj.Code == null) && !obj.IsMissing && WildcardUtil.Match(ingredCode, obj.Code))
				{
					var code = obj.Code.Path.AsSpan().Slice(wildcardStart);
					var codepart = code.Slice(0, code.Length - wildcardEnd);
					if(allowedVariants == null)
					{
						state.Add(codepart.ToString());
						return state;
					}
					foreach(var variant in allowedVariants)
					{
						if(codepart.Equals(variant))
						{
							state.Add(codepart.ToString());
						}
					}
				}
				return state;
			}, state => {
				if(state.Count != 0)
				{
					lock(outCodes)
					{
						outCodes.AddRange(state);
					}
				}
			});
		}

		private class PatternCodeComparer : IComparer<PatternPart>
		{
			public static readonly PatternCodeComparer Instance = new();

			public int Compare(PatternPart? x, PatternPart? y)
			{
				if(x == y) return 0;

				int c = Utils.Compare(x!.Code, y!.Code);
				if(c != 0) return c;

				return 1;
			}
		}

		private class PatternPart
		{
			/// <summary>
			/// Code of the draft item
			/// </summary>
			[JsonProperty(Required = Required.Always)]
			public AssetLocation Code = default!;
			/// <summary>
			/// List of parts to which the outputs of this part are connected
			/// </summary>
			public OutputInfo[]? Outputs = null;
		}

		private class OutputInfo
		{
			/// <summary>
			/// Target part name
			/// </summary>
			[JsonProperty(Required = Required.Always)]
			public string Target = default!;
			/// <summary>
			/// Connection type.
			/// If a part has several outputs of the same type, any of them can be used.
			/// </summary>
			[JsonProperty(Required = Required.Always)]
			public AssetLocation Type = default!;
		}

		/// <summary>
		/// Used for sorting.
		/// Compares two recipes by <see cref="SortedNodes"/>
		/// </summary>
		public class RecipeNodesComparer : IComparer<SchematicRecipe>
		{
			public int Compare(SchematicRecipe? x, SchematicRecipe? y)
			{
				if(x == y) return 0;

				int length = x!.SortedNodes.Length;
				int c = length.CompareTo(y!.SortedNodes.Length);
				if(c != 0) return c;

				for(int i = 0; i < length; i++)
				{
					c = x.SortedNodes[i].CompareTo(y.SortedNodes[i]);
					if(c != 0) return c;
				}

				return 1;
			}
		}

		public readonly struct NodeInfo : IEquatable<NodeInfo>, IComparable<NodeInfo>
		{
			public readonly AssetLocation DraftCode;
			public readonly int OutputCount;

			public NodeInfo(AssetLocation code, int outputCount)
			{
				DraftCode = code;
				OutputCount = outputCount;
			}

			public int CompareTo(NodeInfo other)
			{
				int c = Utils.Compare(DraftCode, other.DraftCode);
				if(c != 0) return c;
				return OutputCount.CompareTo(other.OutputCount);
			}

			public override bool Equals(object? obj)
			{
				return obj is NodeInfo info && Equals(info);
			}

			public bool Equals(NodeInfo other)
			{
				return DraftCode.Equals(other.DraftCode) && OutputCount == other.OutputCount;
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(DraftCode, OutputCount);
			}
		}

		public readonly struct ConnectionInfo : IEquatable<ConnectionInfo>, IComparable<ConnectionInfo>
		{
			public readonly AssetLocation From, To, Type;

			public ConnectionInfo(AssetLocation from, AssetLocation to, AssetLocation type)
			{
				From = from;
				To = to;
				Type = type;
			}

			public int CompareTo(ConnectionInfo other)
			{
				int c = Utils.Compare(From, other.From);
				if(c != 0) return c;

				c = Utils.Compare(To, other.To);
				if(c != 0) return c;

				return Utils.Compare(Type, other.Type);
			}

			public override bool Equals(object? obj)
			{
				return obj is ConnectionInfo info && Equals(info);
			}

			public bool Equals(ConnectionInfo other)
			{
				return From.Equals(other.From) && To.Equals(other.To) && Type.Equals(other.Type);
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(From, To, Type);
			}
		}
	}
}
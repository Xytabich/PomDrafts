using System.Runtime.CompilerServices;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Common
{
	/// <summary>
	/// Represents a draft shape and also describes possible connections
	/// </summary>
	public readonly struct DraftShape
	{
		/// <summary>
		/// Dimensions of the shape
		/// </summary>
		public readonly int Width, Height;
		/// <summary>
		/// List of cells composing the shape
		/// </summary>
		public readonly ShapeCell[] Cells;
		/// <summary>
		/// Draft texture, drawn over the shape area
		/// </summary>
		public readonly AssetLocation Texture;
		public readonly uint Color;

		public DraftShape(ShapeCell[] cells, int width, int height, AssetLocation texture, uint color)
		{
			Cells = cells;
			Width = width;
			Height = height;
			Texture = texture;
			Color = color;
		}

		public DraftShape Clone()
		{
			return new DraftShape(
				Array.ConvertAll(Cells, c => new ShapeCell(c.X, c.Y, ((AssetLocation?, bool)[]?)(c.Connections?.Clone()))),
				Width,
				Height,
				Texture,
				Color
			);
		}

		public DraftShape Rotate(int steps)
		{
			if(steps == 0) return this;
			int xMax = Width - 1;
			int yMax = Height - 1;
			int len = Cells.Length;
			if((steps & 1) == 0)
			{
				for(int i = 0; i < len; i++)
				{
					Cells[i] = Cells[i].Turn180(xMax, yMax);
				}
				return new DraftShape(
					Cells,
					Width,
					Height,
					Texture,
					Color
				);
			}
			else
			{
				if(steps == 1)
				{
					for(int i = 0; i < len; i++)
					{
						Cells[i] = Cells[i].Turn90(yMax);
					}
				}
				else
				{
					for(int i = 0; i < len; i++)
					{
						Cells[i] = Cells[i].Turn180(xMax, yMax).Turn90(yMax);
					}
				}
				return new DraftShape(
					Cells,
					Height,
					Width,
					Texture,
					Color
				);
			}
		}

		/// <summary>
		/// Cell shape, describes position and connections
		/// </summary>
		public readonly struct ShapeCell
		{
			/// <summary>
			/// Cell position
			/// </summary>
			public readonly int X, Y;
			/// <summary>
			/// List of connections.
			/// Has size 4 if assigned, where index is <see cref="ConnectionSide"/>.
			/// </summary>
			public readonly (AssetLocation? type, bool isOutput)[]? Connections;

			public ShapeCell(int x, int y, (AssetLocation? type, bool isOutput)[]? connections)
			{
				X = x;
				Y = y;
				Connections = connections;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public (AssetLocation type, bool isOutput)? GetConnection(ConnectionSide side)
			{
				if(Connections == null) return null;

				var conn = Connections[(int)side];
				return conn.type == null ? null : conn!;
			}

			public ShapeCell Turn180(int xMax, int yMax)
			{
				var connections = Connections;
				if(connections != null)
				{
					(connections[0], connections[1], connections[2], connections[3]) = (connections[2], connections[3], connections[0], connections[1]);
				}
				return new ShapeCell(xMax - X, yMax - Y, connections);
			}

			public ShapeCell Turn90(int yMax)
			{
				var connections = Connections;
				if(connections != null)
				{
					(connections[0], connections[1], connections[2], connections[3]) = (connections[3], connections[0], connections[1], connections[2]);
				}
				return new ShapeCell(yMax - Y, X, connections);
			}
		}

		public enum ConnectionSide
		{
			Up = 0,
			Right = 1,
			Down = 2,
			Left = 3,
		}
	}
}
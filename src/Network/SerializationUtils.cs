using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Network
{
	public static class SerializationUtils//TODO: move to corelib
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] SerializeCodes<T>(T codes) where T : IEnumerable<AssetLocation>
		{
			using(var stream = new MemoryStream())
			{
				SerializeCodes(stream, codes);
				return stream.ToArray();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DeserializeCodes<T>(ArraySegment<byte> data, int count, T outList) where T : ICollection<AssetLocation>
		{
			using(var stream = new MemoryStream(data.Array!, data.Offset, data.Count))
			{
				DeserializeCodes(stream, count, outList);
			}
		}

		public static void SerializeCodes<T>(Stream stream, T codes) where T : IEnumerable<AssetLocation>
		{
			using(var deflate = new DeflateStream(stream, CompressionLevel.Optimal, true))
			{
				using(var writer = new StreamWriter(deflate, Encoding.UTF8, leaveOpen: true))
				{
					writer.NewLine = "\n";
					foreach(var code in codes)
					{
						writer.WriteLine(code.Domain);
						writer.WriteLine(code.Path);
					}
				}
			}
		}

		public static void DeserializeCodes<T>(Stream stream, int count, T outList) where T : ICollection<AssetLocation>
		{
			using(var deflate = new DeflateStream(stream, CompressionMode.Decompress, true))
			{
				using(var reader = new StreamReader(deflate, Encoding.UTF8, leaveOpen: true))
				{
					for(int i = 0; i < count; i++)
					{
						var domain = reader.ReadLine();
						var path = reader.ReadLine();
						outList.Add(new AssetLocation(domain, path));
					}
				}
			}
		}

		public static byte[] SerializeCodes(ReadOnlySpan<AssetLocation> codes)
		{
			using(var stream = new MemoryStream())
			{
				SerializeCodes(stream, codes);
				return stream.ToArray();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DeserializeCodes(ArraySegment<byte> data, Span<AssetLocation> outList)
		{
			using(var stream = new MemoryStream(data.Array!, data.Offset, data.Count))
			{
				DeserializeCodes(stream, outList);
			}
		}

		public static void SerializeCodes(Stream stream, ReadOnlySpan<AssetLocation> codes)
		{
			using(var deflate = new DeflateStream(stream, CompressionLevel.Optimal, true))
			{
				using(var writer = new StreamWriter(deflate, Encoding.UTF8, leaveOpen: true))
				{
					writer.NewLine = "\n";
					foreach(var code in codes)
					{
						writer.WriteLine(code.Domain);
						writer.WriteLine(code.Path);
					}
				}
			}
		}

		public static void DeserializeCodes(Stream stream, Span<AssetLocation> outList)
		{
			using(var deflate = new DeflateStream(stream, CompressionLevel.Optimal, true))
			{
				using(var reader = new StreamReader(deflate, Encoding.UTF8, leaveOpen: true))
				{
					for(int i = 0; i < outList.Length; i++)
					{
						var domain = reader.ReadLine();
						var path = reader.ReadLine();
						outList[i] = new AssetLocation(domain, path);
					}
				}
			}
		}
	}
}
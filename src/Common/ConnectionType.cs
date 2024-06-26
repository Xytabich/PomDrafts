using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Common
{
	public class ConnectionType
	{
		[JsonProperty(Required = Required.Always)]
		public AssetLocation Code = default!;

		[JsonProperty(Required = Required.Always)]
		public TextureList Textures = default!;

		public class TextureList
		{
			[JsonProperty(Required = Required.Always)]
			public AssetLocation Input = default!;

			[JsonProperty(Required = Required.Always)]
			public AssetLocation Output = default!;

			[JsonProperty(Required = Required.Always)]
			public AssetLocation Connected = default!;
		}
	}

	public enum ConnectionTextureType
	{
		Input = 0,
		Output = 1,
		Connected = 2
	}
}
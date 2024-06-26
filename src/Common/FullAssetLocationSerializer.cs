using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts.Common
{
	public class FullAssetLocationSerializer : JsonConverter<AssetLocation>
	{
		public override AssetLocation? ReadJson(JsonReader reader, Type objectType, AssetLocation? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			var value = new AssetLocation(reader.ReadAsString());
			if(existingValue != null)
			{
				existingValue.Domain = value.Domain;
				existingValue.Path = value.Path;
				return existingValue;
			}
			return value;
		}

		public override void WriteJson(JsonWriter writer, AssetLocation? value, JsonSerializer serializer)
		{
			writer.WriteValue(value!.ToString());
		}
	}
}
using ProtoBuf;

namespace PowerOfMind.Drafts.Network
{
	[ProtoContract]
	public class InitDraftsMsg
	{
		[ProtoMember(1)]
		public byte[] DraftsData = default!;
		[ProtoMember(2)]
		public int DraftCount;

		[ProtoMember(3)]
		public byte[] TypesData = default!;
		[ProtoMember(4)]
		public int TypeCount;
	}
}
using ProtoBuf;

namespace PowerOfMind.Drafts.Network
{
	[ProtoContract]
	public class UpdateDraftListMsg
	{
		[ProtoMember(1)]
		public byte[] Data = default!;
		[ProtoMember(2)]
		public int Count;

		[ProtoMember(3)]
		public DraftListAction Action;
	}
}
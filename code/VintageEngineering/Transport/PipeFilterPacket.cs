using ProtoBuf;
using Vintagestory.API.Datastructures;

namespace VintageEngineering.Transport
{
    [ProtoContract]
    public class PipeFilterPacket
    {
        [ProtoMember(1)]
        public byte[] SyncedStack;
    }
}

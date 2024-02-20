using ProtoBuf;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// Contains crutial data for the server to process the Add and Remove connection events.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WireConnectionData
    {
        /// <summary>
        /// Single WireConnection to add or remove.
        /// </summary>
        public WireConnection connection;
        /// <summary>
        /// Add, Remove, RemoveAll (uses BlockPos), Cancel (Uses EntityAgent)
        /// </summary>
        public WireConnectionOpCode opcode;
        /// <summary>
        /// Players UID to determine EntityAgent to use for the CancelPlace code
        /// </summary>
        public string playerUID;
        /// <summary>
        /// BlockPos to use for the RemoveAll code
        /// </summary>
        public BlockPos _pos;

        public WireConnectionData(WireConnectionOpCode opc, WireConnection con, string uid, BlockPos pos)
        {
            this.connection = con;
            this.opcode = opc;
            this.playerUID = uid;
            _pos = pos;
        }   

        public WireConnectionData() { }
    }

    public enum WireConnectionOpCode
    {
        None = 0,
        Add = 1,
        Remove = 2,
        Cancel = 3,
        RemoveAll = 4
    }
}

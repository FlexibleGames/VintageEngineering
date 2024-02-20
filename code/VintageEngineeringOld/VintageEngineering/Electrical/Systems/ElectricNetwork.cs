using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical.Systems
{
    public interface IElectricNetwork
    {
        long NetworkID { get; }
        bool IsDirty { get; set; }
        void AddNode(IElectricNode node);
        void RemoveNode(IElectricNode node);
        IEnumerable<IElectricNode> GetNodes();
    }

    /// <summary>
    /// A single Electric Network
    /// </summary>
    public class ElectricNetwork : IElectricNetwork
    {
        public Dictionary<BlockPos, IElectricNode> nodes = new Dictionary<BlockPos, IElectricNode>();
        internal ElectricalNetworkMod enm;

        /// <summary>
        /// A valid network id should never be 0 or negative.
        /// </summary>
        private long networkID;
        private bool isDirty;

        public long NetworkID { get { return networkID; } }
        public bool IsDirty { get => isDirty; set => isDirty = value; }

        public ElectricNetwork(ElectricalNetworkMod mod, long networkid)
        {
            enm = mod;
            this.networkID = networkid;
        }

        public void AddNode(IElectricNode node)
        {
            if (nodes.ContainsKey(node.Position))
            {

            }
            nodes[node.Position] = node;
        }

        public void RemoveNode(IElectricNode node)
        {

        }

        public IEnumerable<IElectricNode> GetNodes()
        {
            return nodes.Values;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical.Systems;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Base BlockEntity for machines that require a GUI and/or have an inventory.
    /// <br>Holds Electric Network Connections and Network ID mapping.</br>
    /// </summary>
    public abstract class ElectricBEGUI : BlockEntityOpenableContainer, IElectricalBlockEntity, IWireNetwork
    {
        /// <summary>
        /// Total power this Block Entity has
        /// </summary>
        protected ulong electricpower = 0L;

        /// <summary>
        /// Is this machine 'sleeping'?
        /// </summary>
        protected bool isSleeping = false;

        /// <summary>
        /// What Priority is this machine in the Electric Network its attached too?
        /// </summary>
        protected int priority = 5;

        /// <summary>
        /// Is this machine on?
        /// </summary>
        protected bool isEnabled = true;

        /// <summary>
        /// All WireNodes that this block connects to. Index is the WireNode index the connections attach too.
        /// </summary>
        protected Dictionary<int, List<WireNode>> electricConnections = null;

        /// <summary>
        /// Network ID's indexed by the WireNode index (Set in JSON)
        /// </summary>
        protected Dictionary<int, long> NetworkIDs = null;

        #region IElectricBlockEntity
        public virtual ulong MaxPower 
        { 
            get
            {
                if (Block.Attributes != null)
                {
                    return (ulong)Block.Attributes["maxpower"].AsDouble(0);
                }
                return 0;
            }
        }

        public ulong CurrentPower => electricpower;

        public virtual ulong MaxPPS 
        { 
            get
            {
                if (Block.Attributes != null)
                {
                    return (ulong)Block.Attributes["maxpps"].AsDouble(0);
                }
                return 0;
            }
        }

        public virtual EnumElectricalEntityType ElectricalEntityType 
        { 
            get
            {
                if (Block.Attributes != null)
                {
                    return Enum.Parse<EnumElectricalEntityType>(Block.Attributes["entitytype"].AsString("Other"));
                }
                return EnumElectricalEntityType.Other;
            }
        }

        public virtual bool CanReceivePower => throw new NotImplementedException();

        public virtual bool CanExtractPower => throw new NotImplementedException();       

        public bool IsPowerFull => MaxPower == CurrentPower;

        public bool IsSleeping => isSleeping;

        public int Priority => priority;

        public bool IsEnabled => isEnabled;



        public void CheatPower(bool drain = false)
        {
            if (!drain)
            {
                electricpower = MaxPower;
            }
            else
            {
                electricpower = 0;
            }
            this.MarkDirty();
        }

        public ulong ExtractPower(ulong powerWanted, float dt, bool simulate = false)
        {
            if (electricpower == 0) return powerWanted; // we have no power to give

            // what is the max power transfer of this machine for this DeltaTime update tick?
            ulong pps = (ulong)(base.Block.Attributes["maxpps"].AsDouble(0) * dt); // rounding issues abound
            // pps at this point is the PPS from JSON multiplied by DeltaTime (fractional second timing).
            // NOT going to deal with fractinal amounts of power. So Rounding errors are expected.

            if (pps == 0) pps = ulong.MaxValue; // PPS of 0 means NO LIMIT ***This would break recipes***

            // PPS can't exceed the amount of power we have in this generator
            // i.e. we can't provide power we don't have
            pps = (pps > electricpower) ? electricpower : pps;

            if (pps >= powerWanted) // this will probably rarely fire.
            {
                // PPS meets or exceeds power wanted, this machine can cover all power needs.
                if (!simulate) electricpower -= powerWanted;
                isSleeping = false;
                this.MarkDirty();
                return 0; // all power wanted was supplied
            }
            else
            {
                // powerWanted exceeds how much we can supply
                if (!simulate) electricpower -= pps; // simulation mode doesn't change machines power total.                
                isSleeping = false;
                this.MarkDirty();
                return powerWanted - pps; // return powerWanted reduced by our PPS.
            }
        }

        public ulong ReceivePower(ulong powerOffered, float dt, bool simulate = false)
        {
            if (CurrentPower == MaxPower) return powerOffered; // we're full, bounce fast

            // what is the max power transfer of this machine for this DeltaTime update tick?
            ulong pps = (ulong)(base.Block.Attributes["maxpps"].AsDouble(0) * dt); // rounding issues abound

            if (pps == 0) pps = ulong.MaxValue; // PPS of 0 means NO LIMIT ***This would break recipes***

            ulong capacityempty = MaxPower - CurrentPower;

            // simular to ExtractPower, we can't receive more power than we can store.
            pps = (pps > capacityempty) ? capacityempty : pps;
            // pps now holds the actual amount of power we can receive that won't exceed empty capacity

            if (pps >= powerOffered)  // if amount we can take exceeds amount offered
            {                
                // meaning we can take it all.
                if (!simulate) electricpower += powerOffered;
                isSleeping = false;
                this.MarkDirty();
                return 0;
            }
            else
            {
                // far more common, powerOffered exceeds PPS
                if (!simulate) electricpower += pps;
                isSleeping = false;
                this.MarkDirty();
                return powerOffered - pps;
            }
        }

        public List<WireNode> GetConnections(int wirenodeindex)
        {
            if (electricConnections == null) return null;
            if (electricConnections[wirenodeindex] == null) return null;
            if (electricConnections[wirenodeindex].Count == 0) return null;

            return electricConnections[wirenodeindex].ToList<WireNode>();
        }

        public int NumConnections(int wirenodeindex)
        {
            if (electricConnections == null) return 0;
            if (electricConnections[wirenodeindex] == null) return 0;
            
            return electricConnections[wirenodeindex].Count;
        }

        public void AddConnection(int wirenodeindex, WireNode newconnection)
        {
            // Electric Network should have already checked for duplicate connections.
            if (electricConnections == null)
            {
                electricConnections = new Dictionary<int, List<WireNode>>();
            }
            if (electricConnections.Count == 0 || electricConnections[wirenodeindex] == null)
            {
                electricConnections.Add(wirenodeindex, new List<WireNode> { newconnection });
            }
            electricConnections[wirenodeindex].Append(newconnection);
            this.MarkDirty();
        }

        public void RemoveConnection(int wirenodeindex, WireNode oldconnection)
        {
            // sanity checks, always
            if (electricConnections == null) return;
            if (electricConnections[wirenodeindex] == null) return;
            if (electricConnections[wirenodeindex].Count == 0) return;

            electricConnections[wirenodeindex].Remove(oldconnection);
            if (electricConnections[wirenodeindex] == null || electricConnections[wirenodeindex].Count == 0)
            {
                electricConnections.Remove(wirenodeindex);
                NetworkIDs.Remove(wirenodeindex);
            }
            this.MarkDirty();
        }
        #endregion

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (electricConnections == null) electricConnections = new Dictionary<int, List<WireNode>>();
            if (NetworkIDs == null) NetworkIDs = new Dictionary<int, long>();
        }

        public virtual string GetNetworkInfo()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(System.Environment.NewLine); // new line right away
            if (electricConnections.Count == 0)
            {
                stringBuilder.Append("No Network");
                return stringBuilder.ToString();
            }
            foreach (KeyValuePair<int, List<WireNode>> pair in electricConnections)
            {
                stringBuilder.Append($"Node {pair.Key} has {((pair.Value == null) ? "null!" : pair.Value.Count)} cons on id {(NetworkIDs.ContainsKey(pair.Key) ? NetworkIDs[pair.Key] : "NULL!")}" + System.Environment.NewLine);
            }
            return stringBuilder.ToString();
        }

        public bool IsPlayerHoldingWire(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.FirstCodePart() == "catenery")
            {
                return true;
            }
            return false;
        }

        #region AttributeTrees
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
                                                
            tree.SetBool("issleeping", isSleeping);
            tree.SetBool("isenabled", isEnabled);
            tree.SetInt("priority", priority);
            tree.SetLong("currentpower", (long)electricpower);
            tree.SetBytes("connections", SerializerUtil.Serialize(electricConnections));
            tree.SetBytes("networkids", SerializerUtil.Serialize(NetworkIDs));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            isSleeping = tree.GetBool("issleeping", false);
            isEnabled = tree.GetBool("isenabled", true);
            priority = tree.GetInt("priority", 5);
            electricpower = (ulong)tree.GetLong("currentpower", 0);

            byte[] connections = tree.GetBytes("connections");
            if (connections != null)
            {
                electricConnections = SerializerUtil.Deserialize<Dictionary<int, List<WireNode>>>(tree.GetBytes("connections"));
            }
            else
            {
                electricConnections = new Dictionary<int, List<WireNode>>();
            }
            byte[] netids = tree.GetBytes("networkids");
            if (netids != null)
            {
                NetworkIDs = SerializerUtil.Deserialize<Dictionary<int, long>>(tree.GetBytes("networkids"));
            }
            else
            {
                NetworkIDs = new Dictionary<int, long>();
            }            
        }
        #endregion

        #region IWireNetwork
        public bool SetNetworkID(long networkID, int selectionIndex = 0)
        {
            if (NetworkIDs == null) NetworkIDs = new Dictionary<int, long>();
            if (NetworkIDs.ContainsKey(selectionIndex) && NetworkIDs[selectionIndex] != networkID)
            {
                NetworkIDs[selectionIndex] = networkID;
                return true;
            }
            NetworkIDs.Add(selectionIndex, networkID);
            this.MarkDirty();
            return true;
        }

        public long GetNetworkID(int selectionIndex = 0)
        {
            if (NetworkIDs == null)
            {
                NetworkIDs = new Dictionary<int, long>();
                return 0;
            }
            if (NetworkIDs.Count == 0) return 0;

            if (NetworkIDs.ContainsKey(selectionIndex)) return NetworkIDs[selectionIndex];

            return 0;
        }        
        #endregion
    }
}

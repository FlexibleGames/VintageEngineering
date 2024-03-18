using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VintageEngineering.Electrical.Systems;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Base BlockEntity for machines that require a GUI and/or have an inventory.
    /// <br>Holds Electric Network Connections and Network ID mapping.</br>
    /// </summary>
    public abstract class ElectricBEGUI : BlockEntityOpenableContainer, IElectricalBlockEntity, IWireNetwork, IElectricalConnection
    {
        /// <summary>
        /// Total power this Block Entity has. Saved in base ElectricBE.
        /// </summary>
        protected ulong electricpower = 0L;

        /// <summary>
        /// Machine States are On, Off, and Sleeping<br/>
        /// Saved with base ToTreeAttribute call.
        /// </summary>
        private EnumBEState machineState;

        /// <summary>
        /// What Priority is this machine in the Electric Network its attached too? Saved in base ElectricBE.
        /// </summary>
        protected int priority = 5;

        /// <summary>
        /// All WireNodes that this block connects to. Index is the WireNode index the connections attach too.
        /// </summary>
        protected Dictionary<int, List<WireNode>> electricConnections = null;
        
        /// <summary>
        /// Network ID's indexed by the WireNode index (Set in JSON)
        /// </summary>
        protected Dictionary<int, long> NetworkIDs = null;

        /// <summary>
        /// Utility for setting, starting, and stopping animations.
        /// </summary>
        protected BlockEntityAnimationUtil AnimUtil
        {
            get
            {
                BEBehaviorAnimatable behavior = base.GetBehavior<BEBehaviorAnimatable>();
                if (behavior == null) return null;
                return behavior.animUtil;
            }
        }

        /// <summary>
        /// Machine States are On, Off, and Sleeping<br/>
        /// Saved with base ToTreeAttribute call.
        /// </summary>
        public EnumBEState MachineState
        {
            get { return machineState; }
            set
            {
                if (machineState != value)
                {
                    machineState = value;
                    StateChange(); 
                }
            }
        }

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

        public int Priority => priority;

        public bool IsSleeping => machineState == EnumBEState.Sleeping;

        public bool IsEnabled => machineState != EnumBEState.Off;

        public virtual void CheatPower(bool drain = false)
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

        /// <summary>
        /// Called when MachineState is changed. Override to trigger animations, GUI updates, and other fancy things.
        /// </summary>
        public virtual void StateChange()
        {

        }

        public virtual ulong ExtractPower(ulong powerWanted, float dt, bool simulate = false)
        {
            if (MachineState == EnumBEState.Off) return powerWanted; // machine is off, bounce.
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
                this.MarkDirty();
                return 0; // all power wanted was supplied
            }
            else
            {
                // powerWanted exceeds how much we can supply
                if (!simulate) electricpower -= pps; // simulation mode doesn't change machines power total.                                
                this.MarkDirty();
                return powerWanted - pps; // return powerWanted reduced by our PPS.
            }
        }

        public virtual ulong ReceivePower(ulong powerOffered, float dt, bool simulate = false)
        {
            if (MachineState == EnumBEState.Off) return powerOffered; // machine is off, bounce.
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
                this.MarkDirty();
                return 0;
            }
            else
            {
                // far more common, powerOffered exceeds PPS
                if (!simulate) electricpower += pps;
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

        /// <summary>
        /// Returns Rotation depending on what direction this block is facing.<br/>
        /// north return 0, west return 90, south returns 180, east returns 270
        /// </summary>
        /// <returns>Rotate value</returns>
        public int GetRotation()
        {
            RegistryObject block = this.Api.World.BlockAccessor.GetBlock(this.Pos);            
            string lastpart = block.LastCodePart(0); // "north", "east", "south", "west"
            switch (lastpart)
            {
                case "north": return 0; 
                case "west": return 90;
                case "south": return 180;
                case "east": return 270;
                default: return 0;
            }
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

        public virtual bool IsPlayerHoldingWire(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.FirstCodePart() == "catenary")
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
                                                
            tree.SetString("machinestate", machineState.ToString());
            tree.SetInt("priority", priority);
            tree.SetLong("currentpower", (long)electricpower);
            tree.SetBytes("connections", SerializerUtil.Serialize(electricConnections));
            tree.SetBytes("networkids", SerializerUtil.Serialize(NetworkIDs));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            MachineState = Enum.Parse<EnumBEState>(tree.GetString("machinestate", "Sleeping"));
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

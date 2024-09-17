using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using VintageEngineering.Electrical.Systems;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Base Class for Block Entities that require no GUI and have no inventory.
    /// <br>Used for Relays, Transformers, and Toggles.</br>
    /// </summary>
    public class ElectricBEBehavior : BlockEntityBehavior, IElectricalBlockEntity, IWireNetwork, IElectricalConnection
    {
        /// <summary>
        /// Total power this Block Entity has in its battery. Saved to disk in the entity.
        /// </summary>
        public ulong electricpower = 0L;

        /// <summary>
        /// Machine States are On, Off, Paused, and Sleeping<br/>
        /// Saved with base ToTreeAttribute call.
        /// </summary>
        private EnumBEState machineState;

        /// <summary>
        /// What Priority is this machine in the Electric Network its attached too?
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

        public ElectricBEBehavior(BlockEntity blockentity) : base(blockentity)
        {
        }

        /// <summary>
        /// Utility for setting, starting, and stopping animations.
        /// </summary>
        public BlockEntityAnimationUtil AnimUtil
        {
            get
            {
                return Blockentity.GetBehavior<BEBehaviorAnimatable>()?.animUtil;
            }
        }

        /// <summary>
        /// Machine States are On, Off, Paused, and Sleeping<br/>
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
                    StateChanged();
                }
            }
        }

        #region IElectricBlockEntity
        public virtual ulong MaxPower
        {
            get
            {
                if (properties != null)
                {
                    return (ulong)properties["maxpower"].AsDouble(0);
                }
                return 0;
            }
        }

        public ulong CurrentPower => electricpower;

        public virtual ulong MaxPPS
        {
            get
            {
                if (properties != null)
                {
                    return (ulong)properties["maxpps"].AsDouble(0);
                }
                return 0;
            }
        }

        public virtual EnumElectricalEntityType ElectricalEntityType
        {
            get
            {
                if (properties != null)
                {
                    return Enum.Parse<EnumElectricalEntityType>(properties["entitytype"].AsString("Other"));
                }
                return EnumElectricalEntityType.Other;
            }
        }

        public virtual bool CanReceivePower { get; private set; }

        public virtual bool CanExtractPower { get; private set; }

        public virtual bool IsPowerFull => CurrentPower >= MaxPower; // changed to >= to ensure power rebalancing doesn't break the logic.

        public virtual int Priority { get => priority; set => priority = value; }

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
            Blockentity.MarkDirty(true);
        }

        public virtual ulong RatedPower(float dt, bool isInsert = false)
        {
            if (!IsEnabled)
            {
                return 0;
            }
            ulong rate = ((ulong)Math.Round(MaxPPS * dt));
            if (isInsert)
            {
                ulong emptycap = MaxPower - CurrentPower;
                return emptycap < rate ? emptycap : rate;
            }
            else
            {
                // extracting
                return CurrentPower < rate ? CurrentPower : rate;
            }
        }

        public virtual ulong ExtractPower(ulong powerWanted, float dt, bool simulate = false)
        {
            if (MachineState == EnumBEState.Off || !CanExtractPower) return powerWanted; // machine is off, bounce.
            if (electricpower == 0) return powerWanted; // we have no power to give

            // what is the max power transfer of this machine for this DeltaTime update tick?
            ulong pps = (ulong)Math.Round(MaxPPS * dt); // rounding issues abound
            // pps at this point is the PPS from JSON multiplied by DeltaTime (fractional second timing).
            // NOT going to deal with fractinal amounts of power. So Rounding errors are expected.

            if (pps == 0) pps = ulong.MaxValue; // PPS of 0 means NO LIMIT ***This will break recipes***

            // PPS can't exceed the amount of power we have in this generator
            // i.e. we can't provide power we don't have
            pps = (pps > electricpower) ? electricpower : pps;

            if (pps >= powerWanted) // this will probably rarely fire.
            {
                // PPS meets or exceeds power wanted, this machine can cover all power needs.
                if (!simulate) electricpower -= powerWanted;
                Blockentity.MarkDirty(true);
                return 0; // all power wanted was supplied
            }
            else
            {
                // powerWanted exceeds how much we can supply
                if (!simulate) electricpower -= pps; // simulation mode doesn't change machines power total.
                Blockentity.MarkDirty(true);
                return powerWanted - pps; // return powerWanted reduced by our PPS.
            }
        }

        public virtual ulong ReceivePower(ulong powerOffered, float dt, bool simulate = false)
        {
            if (MachineState == EnumBEState.Off || !CanReceivePower) return powerOffered; // machine is off, bounce.

            // The == was changed to >= to ensure rebalancing machine power values don't break the system.
            if (CurrentPower >= MaxPower) return powerOffered; // we're full, bounce fast

            // what is the max power transfer of this machine for this DeltaTime update tick?
            ulong pps = (ulong)Math.Round(MaxPPS * dt); // rounding issues abound

            if (pps == 0) pps = ulong.MaxValue; // PPS of 0 means NO LIMIT ***This would break recipes, aka InstaCraft ***
            else pps += 2;

            ulong capacityempty = MaxPower - CurrentPower;

            // simular to ExtractPower, we can't receive more power than we can store.
            pps = (pps > capacityempty) ? capacityempty : pps;
            // pps now holds the actual amount of power we can receive that won't exceed empty capacity

            if (pps >= powerOffered)  // if amount we can take exceeds amount offered
            {
                // meaning we can take it all.
                if (!simulate) electricpower += powerOffered;
                Blockentity.MarkDirty(true);
                return 0;
            }
            else
            {
                // far more common, powerOffered exceeds PPS
                if (!simulate) electricpower += pps;
                Blockentity.MarkDirty(true);
                return powerOffered - pps;
            }
        }

        #endregion


        #region IElectricalConnection
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
            else
            {
                electricConnections[wirenodeindex].Add(newconnection);
            }
            Blockentity.MarkDirty(true);
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
            Blockentity.MarkDirty(true);
        }
        #endregion

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            CanExtractPower = properties["canExtractPower"].AsBool();
            CanReceivePower = properties["canReceivePower"].AsBool();

            if (electricConnections == null) electricConnections = new Dictionary<int, List<WireNode>>();
            if (NetworkIDs == null) NetworkIDs = new Dictionary<int, long>();
            if (NetworkIDs.Count > 0 && electricConnections.Count > 0 && api.Side == EnumAppSide.Server)
            {
                // lets try to add ourselves to the proper network
                ElectricalNetworkManager nm = api.ModLoader.GetModSystem<ElectricalNetworkMod>(true).manager;
                if (nm != null)
                {
                    foreach (KeyValuePair<int, long> networkpair in NetworkIDs)
                    {
                        if (networkpair.Value == 0)
                        {
                            NetworkIDs.Remove(networkpair.Key);
                            continue; // part of a network but with id = 0 means a corrupted BE
                        }
                        if (base.Block is WiredBlock wiredBlock)
                        {
                            /* //Example code to CHUNK LOAD the chunk column at this block position...
                            int chunkx = this.Pos.X / GlobalConstants.ChunkSize;
                            int chunkz = this.Pos.Z / GlobalConstants.ChunkSize;
                            (api as ICoreServerAPI).WorldManager.LoadChunkColumnPriority(chunkx, chunkz, new ChunkLoadOptions { KeepLoaded = true });

                            //Call to REMOVE the CHUNK LOAD
                            (api as ICoreServerAPI).WorldManager.UnloadChunkColumn(chunkx, chunkz);
                            */

                            if (wiredBlock.WireAnchors == null) continue;
                            WireNode node = wiredBlock.GetWireNodeInBlock(networkpair.Key).Copy();
                            if (node == null) continue;
                            node.blockPos = this.Pos; // extremely important that we add Block Position to this.
                            nm.JoinNetwork(networkpair.Value, node, this);
                        }
                    }
                }
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            MachineState = EnumBEState.Sleeping; // when first placed, a machine is on and not crafting.
        }

        /// <summary>
        /// Returns Rotation depending on what direction this block is facing.<br/>
        /// north return 0, west return 90, south returns 180, east returns 270
        /// </summary>
        /// <returns>Rotate value</returns>
        public int GetRotation()
        {
            string side = Block.Variant["side"];            
            // The BlockFacing horiztonal index goes counter-clockwise from east. That needs to be converted so that
            // it goes counter-clockwise from north instead.
            int adjustedIndex = ((BlockFacing.FromCode(side)?.HorizontalAngleIndex ?? 1) + 3) & 3;
            return adjustedIndex * 90;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            string onOff;
            switch (MachineState)
            {
                case EnumBEState.On: onOff = Lang.Get("vinteng:gui-word-on"); break;
                case EnumBEState.Off: onOff = Lang.Get("vinteng:gui-word-off"); break;
                case EnumBEState.Sleeping: onOff = Lang.Get("vinteng:gui-word-sleeping"); break;
                case EnumBEState.Paused: onOff = Lang.Get("vinteng:gui-word-paused"); break;
                default: onOff = "Error"; break;
            }

            dsc.AppendLine($"{onOff} | {Lang.Get("vinteng:gui-word-power")}: {CurrentPower:N0}/{MaxPower:N0}{System.Environment.NewLine}{Lang.Get("vinteng:gui-machine-pps")} : {MaxPPS}");

            bool extDebug = (Api as ICoreClientAPI)?.Settings.Bool["extendedDebugInfo"] == true;
            if (extDebug)
            {
                dsc.AppendLine(GetNetworkInfo());
            }
        }

        public virtual string GetNetworkInfo()
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (electricConnections.Count == 0)
            {
                return "No Network";
            }
            foreach (KeyValuePair<int, List<WireNode>> pair in electricConnections)
            {
                stringBuilder.AppendLine($"Node {pair.Key} has {((pair.Value == null) ? "null!" : pair.Value.Count)} cons on id {(NetworkIDs.ContainsKey(pair.Key) ? NetworkIDs[pair.Key] : "NULL!")}");
            }
            return stringBuilder.ToString().TrimEnd();
        }

        public virtual bool IsPlayerHoldingWire(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.FirstCodePart() == "catenary")
            {
                return true;
            }
            return false;
        }

        public override void OnBlockUnloaded()
        {
            if (Api.Side == EnumAppSide.Server)
            {
                ElectricalNetworkManager nm = Api.ModLoader.GetModSystem<ElectricalNetworkMod>(true).manager;
                if (nm == null) return;
                foreach (KeyValuePair<int, long> networkpair in NetworkIDs)
                {
                    if (base.Block is WiredBlock wiredBlock)
                    {
                        if (wiredBlock.WireAnchors == null) continue;

                        WireNode node = wiredBlock.GetWireNodeInBlock(networkpair.Key).Copy();
                        if (node == null) continue;
                        node.blockPos = this.Pos;  // extremely important that we add Block Position to this.
                        nm.LeaveNetwork(networkpair.Value, node, this);
                    }
                }
            }
            base.OnBlockUnloaded();
        }

        /// <summary>
        /// Called after the machine state changes. Override to define particles and animations when machine changes state.
        /// </summary>
        protected virtual void StateChanged()
        {
        }

        #region AttributeTrees
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
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
            Blockentity.MarkDirty(true);
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

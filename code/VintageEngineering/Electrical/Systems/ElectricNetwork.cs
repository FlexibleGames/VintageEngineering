using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using VintageEngineering.API;
using VintageEngineering.Electrical.Systems.Catenary;
using VintageEngineering.Multiblock;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VintageEngineering.Electrical.Systems
{
    public delegate ulong ExtractPowerHandler(ulong _maxWanted, float _dt, bool _simulate);
    public delegate ulong ReceivePowerHandler(ulong _offered, float _dt, bool _simulate);
    public delegate ulong StoragePowerHandler(ulong _power, float _dt, bool _simulate, bool _isInsert);

    /// <summary>
    /// Interface that defines common Electric Network features.
    /// </summary>
    public interface IElectricNetwork
    {
        /// <summary>
        /// NetworkID assigned to this network.
        /// </summary>
        long NetworkID { get; set; }

        /// <summary>
        /// Marks the Network dirty
        /// </summary>
        bool IsDirty { get; set; }

        /// <summary>
        /// Gets whether this network is sleeping.
        /// </summary>
        bool IsSleeping { get; }

        /// <summary>
        /// Add a WireNode to this Network.<br/>
        /// Automatically sorts internal Lists by node priority for network ticking.<br/>
        /// Optionally sets the new nodes NetworkID to the this networks ID.<br/>
        /// It is vital the given WireNode contain a BlockPos.
        /// </summary>
        /// <param name="node">WireNode to add</param>
        /// <param name="blockAccessor">BlockAccessor</param>
        /// <param name="updateEntity">Set to false to not change the Network data saved by the Enity.</param>
        void AddNode(WireNode node, IBlockAccessor blockAccessor, bool updateEntity = true);

        /// <summary>
        /// Remove a WireNode From this Network<br/>
        /// Optionally resets to 0 the NetworkID of the node if no other connections exist.<br/>
        /// It is vital the given WireNode contain a BlockPos
        /// </summary>
        /// <param name="node">WireNode to remove</param>
        /// <param name="blockAccessor">BlockAccessor</param>
        /// <param name="updateEntity">Set to false to not change the Network data saved by the Enity.</param>
        void RemoveNode(WireNode node, IBlockAccessor blockAccessor, bool updateEntity = true);

        /// <summary>
        /// Tick (Update) this network
        /// </summary>
        /// <param name="dt">Delta Time</param>
        /// <returns>Return False to delete the network.</returns>
        bool UpdateTick(float dt);

        /// <summary>
        /// Get all Nodes on this network.
        /// </summary>
        /// <returns>Enumerator</returns>
        IEnumerable<WireNode> GetNodes();
    }

    /// <summary>
    /// A single Electric Network
    /// </summary>
    [ProtoContract]
    public class ElectricNetwork : IElectricNetwork
    {
        /// <summary>
        /// Producer nodes should subscribe to this event to be included in the UpdateTick
        /// </summary>
        public event ExtractPowerHandler OnExtractPower;
        /// <summary>
        /// Consumer Nodes should subscribe to this event to be included in the UpdateTick
        /// </summary>
        public event ReceivePowerHandler OnReceivePower;
        /// <summary>
        /// Storage Nodes should subscribe to this event to be included in the UpdateTick
        /// </summary>
        public event StoragePowerHandler OnStoragePower;

        /// <summary>
        /// All of the nodes associated with this network, saved to disk.
        /// <br>Will contain nodes that are not consumers, producers, or storage.</br>
        /// </summary>
        [ProtoMember(1)]
        public List<WireNode> allNodes = new List<WireNode>();

        public ICoreServerAPI api;
        private bool isDirty;
        private bool isSleeping;
        private float sleepTimer;
        private long _networkID;
        private ulong _networkPPS = 0;
        private float _rebalanceBouncer = 0f;
        private float _logBouncer = 0f;

        [ProtoMember(2)]
        public long NetworkID { get => _networkID; set => _networkID = value; }
        public bool IsDirty { get => isDirty; set => isDirty = value; }

        public int NodeCount { get => allNodes.Count; }

        public bool IsSleeping => isSleeping;

        public ulong NetworkPPS => _networkPPS;

        [ProtoMember(3)]
        public EnumElectricalPowerTier PowerTier { get; private set; } = EnumElectricalPowerTier.LV;

        public ElectricNetwork()
        {
        }
        public ElectricNetwork(long _networkid, ICoreServerAPI _api, EnumElectricalPowerTier powerTier = EnumElectricalPowerTier.LV)
        {
            api = _api;
            this.NetworkID = _networkid;
            this.isSleeping = false;
            sleepTimer = 0;
            PowerTier = powerTier;
            ElectricalNetworkMod mod = _api.ModLoader.GetModSystem<ElectricalNetworkMod>(true);
            if (mod != null)
            {
                ElectricalNetworkConfig econfig = mod.ElectricConfig;
                switch (powerTier)
                {
                    case EnumElectricalPowerTier.LV: _networkPPS = econfig.NetworkPPS_LV; break;
                    case EnumElectricalPowerTier.MV: _networkPPS = econfig.NetworkPPS_MV; break;
                    case EnumElectricalPowerTier.HV: _networkPPS = econfig.NetworkPPS_HV; break;
                    case EnumElectricalPowerTier.EV: _networkPPS = econfig.NetworkPPS_EV; break;
                    default: _networkPPS = 1; break;
                }
            }
        }


        /// <summary>
        /// Dumps and rebuilds all Entity nodes based on the allNodes list, skipping nodes that are in unloaded chunks.
        /// </summary>
        /// <exception cref="NullReferenceException">Exception thrown if IElectricalBlocKEntity is null</exception>
        public void InitializeNetwork(float dt)
        {            
            if (allNodes.Count > 0)
            {
                int unloadedNodes = 0;
                foreach (WireNode node in allNodes)
                {
                    // if the position isn't loaded yet, skip
                    if (!VEHelpers.IsChunkLoaded(api.World, node.blockPos)) 
                    {
                        unloadedNodes++;
                        continue;
                    }

                    IElectricalBlockEntity entity = IElectricalBlockEntity.GetAtPos(api.World.BlockAccessor, node.blockPos);
                    // entity should never be null here
                    if (entity == null) 
                    {
                        if (_logBouncer > 2f || _logBouncer == dt)
                        {
                            // lets try not to spam log files to multiple GB, still every 2 seconds...
                            api.Logger.Error($"VintEng: An Electrical Entity at {node.blockPos.ToLocalPosition(api)} is null when trying to initalize network but Chunk is loaded.");
                            api.BroadcastMessageToAllGroups($"VintEng: An Electrical Entity at {node.blockPos.ToLocalPosition(api)} is null when trying to initalize network but chunk is loaded.", EnumChatType.Notification);
                            _logBouncer = 0f;
                        }
                        continue;
                        //throw new NullReferenceException("VintEng: An Electrical Entity is null when trying to initalize network."); 
                    }
                    
                    if (entity.ElectricalEntityType == EnumElectricalEntityType.PassThrough)
                    {
                        entity = PassThroughEntity(api.World.BlockAccessor, entity);
                        if (entity == null) continue;
                    }

                    switch (entity.ElectricalEntityType)
                    {
                        case EnumElectricalEntityType.Consumer:
                            this.OnReceivePower -= entity.ReceivePower; // this prevents duplicates from appearing in the list.
                            this.OnReceivePower += entity.ReceivePower;
                            break;
                        case EnumElectricalEntityType.Producer:
                            this.OnExtractPower -= entity.ExtractPower;
                            this.OnExtractPower += entity.ExtractPower;
                            break;
                        case EnumElectricalEntityType.Toggle:
                        case EnumElectricalEntityType.Storage:
                        case EnumElectricalEntityType.Transformer:
                            this.OnStoragePower -= entity.StoragePower;
                            this.OnStoragePower += entity.StoragePower;
                            break;
                        case EnumElectricalEntityType.Relay:
                            break;
                        default: break; // This seems to handle null entities
                    }
                }
                if (unloadedNodes > 0) isDirty = true;
                else isDirty = false;
            }
        }

        public IElectricalBlockEntity PassThroughEntity(IBlockAccessor access, IElectricalBlockEntity entity)
        {
            IElectricalBlockEntity passthrough = null;
            if (entity.ElectricalEntityType == EnumElectricalEntityType.PassThrough)
            {
                if (access.GetBlockEntity(entity.GetPosition()) is IMBPassThrough proxy)
                {
                    bool valid = false;
                    if (VEHelpers.IsChunkLoaded(api.World, entity.GetPosition()))
                    {
                        if (proxy.CorePosition == null)
                        {
                            valid = (proxy as BEMBPowerConnector).ValidateCore();
                        }
                        else
                        {
                            valid = (proxy as BEMBPowerConnector).GetCore();
                        }
                        if (valid && proxy.CorePosition != null)
                        {
                            BlockPos corepos = proxy.CorePosition.Copy();
                            if (corepos != null)
                            {
                                passthrough = proxy.CoreEntity.GetBehavior<IElectricalBlockEntity>();
                            }
                        }
                    }
                }
            }
            return passthrough;
        }

        public void AddNode(WireNode node, IBlockAccessor blockAccessor, bool updateEntity = true)
        {
            if (allNodes.Contains(node))
            {
                return; // only one connection per node per block on a single network.
            }
            // in the case of a toggle (switch) both (all) anchors can be the same power tier
            // however they need to be seperate networks as a toggle that is OFF severs the connection
            // and I think it best to not merge and seperate the networks every time the toggle is switched

            IElectricalBlockEntity entity = IElectricalBlockEntity.GetAtPos(blockAccessor, node.blockPos);


            if (entity == null) { throw new Exception("Attempting to add Electrical Node that is NOT an IElectricalBlockEntity!"); }

            IWireNetwork wirenet = IWireNetwork.GetAtPos(blockAccessor, node.blockPos);
            if (wirenet != null && updateEntity)
            {
                wirenet.SetNetworkID(NetworkID, node.index);
            }

            allNodes.Add(node);

            if (entity.ElectricalEntityType == EnumElectricalEntityType.PassThrough)
            {
                entity = PassThroughEntity(blockAccessor, entity);
            }
            // if entity is null here that can only mean one thing
            // it's a passthrough and the MB isn't built yet, IE there is nothing to pass through to
            if (entity == null) 
            {
                api.Logger.Error($"VintEng: Error Adding pass through node to net:{NetworkID} at {node.blockPos.ToLocalPosition(api).ToBlockPos()}");
                return; 
            }

            // a Transformer is a special type of storage, it has more than one power tier connection.
            // Toggles will be another unique type of storage, one that can have > 1 connection to a single network tier
            switch (entity.ElectricalEntityType)
            {
                case EnumElectricalEntityType.Consumer:
                    this.OnReceivePower -= entity.ReceivePower; // this prevents duplicates from appearing in the list.
                    this.OnReceivePower += entity.ReceivePower;
                    break;
                case EnumElectricalEntityType.Producer:
                    this.OnExtractPower -= entity.ExtractPower;
                    this.OnExtractPower += entity.ExtractPower;
                    break;
                case EnumElectricalEntityType.Toggle:
                case EnumElectricalEntityType.Storage:
                case EnumElectricalEntityType.Transformer:
                    this.OnStoragePower -= entity.StoragePower;
                    this.OnStoragePower += entity.StoragePower;
                    break;
                case EnumElectricalEntityType.Relay:
                    break;
                default: break; // This seems to handle null entities
            }
            blockAccessor.GetBlockEntity(node.blockPos).MarkDirty(true);
        }

        /// <summary>
        /// Join this Electric Network, used by nodes that left the network due to chunks unloading. As well as on world load.<br/>
        /// It is vital the WireNode has a Block Position set.<br/>
        /// Does alter the allNodes list if node is missing.
        /// </summary>
        /// <param name="node">WireNode Joining</param>
        /// <param name="entity">IElectricalBlockEntity Joining</param>
        public void Join(WireNode node, IElectricalBlockEntity entity)
        {
            if (!allNodes.Contains(node))
            {
                api.Logger.Warning("VintEng: A node is attempting to join a network they are not apart of. If loading an old world for the first time, you can ignore this.");
                allNodes.Add(node);
            }

            if (entity.ElectricalEntityType == EnumElectricalEntityType.PassThrough)
            {
                entity = PassThroughEntity(api.World.BlockAccessor, entity);
            }
            if (entity == null) return;            
            switch (entity.ElectricalEntityType)
            {
                case EnumElectricalEntityType.Consumer:
                    this.OnReceivePower -= entity.ReceivePower; // this prevents duplicates from appearing in the list.
                    this.OnReceivePower += entity.ReceivePower;
                    break;
                case EnumElectricalEntityType.Producer:
                    this.OnExtractPower -= entity.ExtractPower;
                    this.OnExtractPower += entity.ExtractPower;
                    break;
                case EnumElectricalEntityType.Toggle:
                case EnumElectricalEntityType.Storage:
                case EnumElectricalEntityType.Transformer:
                    this.OnStoragePower -= entity.StoragePower;
                    this.OnStoragePower += entity.StoragePower;
                    break;
                case EnumElectricalEntityType.Relay:
                    break;
                default: break; // This seems to handle null entities
            }
        }

        /// <summary>
        /// Leave this Electric Network due to chunk unloading. Will not alter the allNodes list.<br/>
        /// It is vital the WireNode has the Block Position set.
        /// </summary>
        /// <param name="node">WireNode Leaving</param>
        /// <param name="entity">IElectricBlockEntity Leaving.</param>
        public void Leave(WireNode node, IElectricalBlockEntity entity)
        {
            if (!allNodes.Contains(node) || entity == null) return; // can't leave a network we're not apart of.

            //allNodes.Remove(node); do not remove the node simply because it's unloaded
            if (entity.ElectricalEntityType == EnumElectricalEntityType.PassThrough)
            {
                entity = PassThroughEntity(api.World.BlockAccessor, entity);
            }
            if (entity == null) return;
            switch (entity.ElectricalEntityType)
            {
                case EnumElectricalEntityType.Consumer:
                    this.OnReceivePower -= entity.ReceivePower; 
                    break;
                case EnumElectricalEntityType.Producer:
                    this.OnExtractPower -= entity.ExtractPower;
                    break;
                case EnumElectricalEntityType.Toggle:
                case EnumElectricalEntityType.Storage:
                case EnumElectricalEntityType.Transformer:
                    this.OnStoragePower -= entity.StoragePower;
                    break;
                case EnumElectricalEntityType.Relay:
                    break;
                default: break; // This seems to handle null entities
            }
        }

        public void RemoveNode(WireNode node, IBlockAccessor blockAccessor, bool updateEntity = true)
        {
            if (allNodes.Contains(node))
            {
                IElectricalBlockEntity entity = IElectricalBlockEntity.GetAtPos(blockAccessor, node.blockPos);

                if (entity == null)
                {
                    throw new Exception("Attempting to remove Electrical Node that is NOT an IElectricalBlockEntity!");
                }

                IWireNetwork wirenet = IWireNetwork.GetAtPos(blockAccessor, node.blockPos);
                if (wirenet != null)
                {
                    bool found = allNodes.Remove(node);
                    if (!found) throw new Exception("VintEng: Attempting to remove an Electric Network node that does not exist in its list.");
                    // if the last node is removed, NetworkID entry is automatically removed.
                }

                if (entity.ElectricalEntityType == EnumElectricalEntityType.PassThrough)
                {
                    entity = PassThroughEntity(api.World.BlockAccessor, entity);
                }
                if (entity == null) return;
                switch (entity.ElectricalEntityType)
                {
                    case EnumElectricalEntityType.Consumer:
                        this.OnReceivePower -= entity.ReceivePower; // this prevents duplicates from appearing in the list.
                        break;
                    case EnumElectricalEntityType.Producer:
                        this.OnExtractPower -= entity.ExtractPower;
                        break;
                    case EnumElectricalEntityType.Toggle:
                    case EnumElectricalEntityType.Storage:
                    case EnumElectricalEntityType.Transformer:
                        this.OnStoragePower -= entity.StoragePower;                        
                        break;
                    case EnumElectricalEntityType.Relay:
                        break;
                    default: break; // This seems to handle null entities
                }
            }
        }

        /// <summary>
        /// Completely clears all data for this network, hopefully without lost memory.
        /// </summary>
        public void Clear()
        {
            allNodes.Clear();
            //networkID = 0;
        }

        /// <summary>
        /// Wake the network up, will sleep again if conditions are right.
        /// </summary>
        public void Wakeup()
        {
            isSleeping = false;
            sleepTimer = 0;
        }

        public bool UpdateTick(float deltaTime)
        {
            // The meat and 'tatos of the entire system.
            //ulong totalpowerwanted = 0;

            ulong totalpoweringen = 0;
            ulong totalpoweroffered = 0;
            ulong totalinstorage = 0;
            ulong totalstorageavailable = 0;
            ulong totalexcesspower = 0;
            ulong totalstorageused = 0;

            // TODO: Update PPS of entire network to use _networkPPS variable...

            if (allNodes.Count <= 1) return true; // one node, no need to tick it.            

            // power per tick, power limit for this update tick
            // By Default: LV is 500 PPS, MV is 4000 PPS, and HV is 200k PPS
            ulong networkppt = (ulong)((_networkPPS * deltaTime) + 1);

            _rebalanceBouncer += deltaTime;
            _logBouncer += deltaTime;
            if (isDirty) InitializeNetwork(deltaTime);
            if (_logBouncer > 1000f) _logBouncer = 0f; // limit how large this value can be.

            if (OnExtractPower == null &&
                OnReceivePower == null &&
                OnStoragePower == null)
            {
                // a network of all relays would have nothing subscribed, but allNodes would be > 0
                if (allNodes.Count == 0) 
                {
                    api.Logger.Warning($"All nodes removed from ElectricNetwork ID {this.NetworkID}. Deleting Network.");
                    return false; // there are zero nodes in this network, delete it.
                }
                return true;
            }

            if (isSleeping)
            {
                sleepTimer += deltaTime;
                if (sleepTimer > 5)
                {
                    Wakeup();
                }
                return true;
            }
            if (OnExtractPower != null)
            {
                foreach (Delegate del in OnExtractPower.GetInvocationList())
                {                    
                    totalpoweringen += ((ExtractPowerHandler)del).Invoke(0, deltaTime, true);
                }
            }
            if (OnStoragePower != null)
            {
                if (_rebalanceBouncer > 2f)
                {
                    RebalanceStorages(_rebalanceBouncer);
                    _rebalanceBouncer = 0f;
                }

                foreach (Delegate del in OnStoragePower.GetInvocationList())
                {
                    // ulong power, float dt, bool simulate, bool isInsert
                    totalinstorage += ((StoragePowerHandler)del).Invoke(0, deltaTime, true, false); // how much to extract
                    totalstorageavailable += ((StoragePowerHandler)del).Invoke(0, deltaTime, true, true); // how much to insert, if available
                }
            }
            totalpoweroffered = totalpoweringen + totalinstorage;

            if (OnReceivePower != null) // this actually delivers power to the machines
            {
                foreach (Delegate del in OnReceivePower.GetInvocationList())
                {
                    if (totalpoweroffered == 0) break; // no power available, no need to continue.
                    totalpoweroffered = ((ReceivePowerHandler)del).Invoke(totalpoweroffered, deltaTime, false);
                }
            }
            // totalpoweroffered will have any excess power we didn't use, it could = 0
            ulong totalpowerused = (totalpoweringen + totalinstorage) - totalpoweroffered;

            if (OnReceivePower == null && OnExtractPower == null)
            {                
                // edge case of a network ONLY having storage and/or transformer nodes
                // tries to balance all storage within 2% of one-another
                if (OnStoragePower != null)
                {
                    RebalanceStorages(deltaTime);
                    return true;
                }
                else
                {
                    // sleep, there is nothing to simulate
                    //isSleeping = true; // zzzzzzzzzzzz
                    return true;
                }
            }

            if (totalpowerused > totalpoweringen)
            {
                // we used more power than generators were able to provide, storage was used
                totalstorageused = totalpowerused - totalpoweringen;
                if (OnExtractPower != null)
                {
                    foreach (Delegate del in OnExtractPower.GetInvocationList())
                    {
                        totalpowerused = ((ExtractPowerHandler)del).Invoke(totalpowerused, deltaTime, false);
                    }
                }
                if (OnStoragePower != null)
                {
                    foreach (Delegate del in OnStoragePower.GetInvocationList())
                    {
                        if (totalpowerused == 0) break;
                        totalpowerused = ((StoragePowerHandler)del).Invoke(totalpowerused, deltaTime, false, false);
                    }
                }
                if (totalpowerused > (ulong)this.allNodes.Count) // 0 just didn't cut it due to rounding issues.
                {
                    throw new Exception("Electric Network : Power used remainder exceeds threshold.");
                }
            }
            else
            {
                // we have excess power produced and not used, push into storage
                totalexcesspower = totalpoweringen - totalpowerused;
                if (totalstorageavailable >= totalexcesspower)
                {
                    // available storage capacity exceeds leftover power, push all power into storage
                    if (OnExtractPower != null)
                    {
                        // remove all power from generators
                        foreach (Delegate del in OnExtractPower.GetInvocationList())
                        {
                            totalpoweringen = ((ExtractPowerHandler)del).Invoke(totalpoweringen, deltaTime, false);
                            if (totalpoweringen == 0) break;
                        }
                    }
                    if (OnStoragePower != null)
                    {
                        foreach (Delegate del in OnStoragePower.GetInvocationList())
                        {
                            // push excess power into storage nodes
                            totalexcesspower = ((StoragePowerHandler)del).Invoke(totalexcesspower, deltaTime, false, true);
                        }
                    }
                }
                else
                {
                    // leftover power exceeds storage capacity, remove only what is needed to fill storages
                    ulong totalpowerconsumed = totalpowerused + totalstorageavailable;
                    if (totalpowerconsumed == 0) return true;
                    if (OnExtractPower != null) //producerNodes.Count > 0)
                    {
                        foreach (Delegate del in OnExtractPower.GetInvocationList())
                        {
                            // remove power that we need
                            totalpowerconsumed = ((ExtractPowerHandler)del).Invoke(totalpowerconsumed, deltaTime, false);
                            if (totalpowerconsumed == 0) break;
                        }
                    }
                    if (OnStoragePower != null)
                    {
                        foreach (Delegate del in OnStoragePower.GetInvocationList())
                        {                            
                            // add excess available power to storage
                            totalstorageavailable = ((StoragePowerHandler)del).Invoke(totalstorageavailable, deltaTime, false, true);
                            if (totalstorageavailable == 0) break;
                        }
                    }
                    if (totalpowerconsumed != 0 && totalstorageavailable != 0)
                    {
                        api.World.Logger.Warning($"Error in Electric Network UpdateTick id : {NetworkID}");
                    }
                    // at this point, totalpowerconsumed should = 0 AND totalstorageavailable should = 0
                }
            }
            return true;
        }

        public bool RebalanceStorages(float dt)
        {
            Delegate[] storagehandlers = OnStoragePower.GetInvocationList();
            int totalstorageblocks = storagehandlers.Length;
            if (totalstorageblocks <= 1) return true;

            if (allNodes.Count <= 1) return true;

            //ulong totalcapacity = 0;
            //ulong totalcapacityavailable = 0;
            //ulong totalusedcapacity = 0;
            float totalpercentfull = 0;

            Dictionary<IElectricalBlockEntity, float> storageNodes = new();

            foreach (WireNode node in allNodes) // find the unrated total power values of these nodes
            {
                if (!VEHelpers.IsChunkLoaded(api.World, node.blockPos)) continue;

                IElectricalBlockEntity entity = IElectricalBlockEntity.GetAtPos(api.World.BlockAccessor, node.blockPos);
                if (entity != null)
                {
                    if (entity.ElectricalEntityType == EnumElectricalEntityType.Relay) continue;
                    if (entity.ElectricalEntityType == EnumElectricalEntityType.Storage ||
                        entity.ElectricalEntityType == EnumElectricalEntityType.Toggle ||
                        entity.ElectricalEntityType == EnumElectricalEntityType.Transformer)
                    {
                        //totalcapacity += entity.MaxPower;
                        //totalcapacityavailable += entity.MaxPower - entity.CurrentPower;
                        //totalusedcapacity += entity.CurrentPower;
                        float percentf = ((float)(entity.CurrentPower / (double)entity.MaxPower)) * 100;
                        totalpercentfull += percentf;
                        storageNodes.Add(entity, percentf);
                    }
                }
            }
            if (storageNodes.Count == 1) return true;
            // what is the overall pressure of the entire system, used as the base-line for individual blocks
            float targetpressure = totalpercentfull / storageNodes.Count; //(int)((totalusedcapacity / (double)totalcapacity) * 100);

            List<IElectricalBlockEntity> surplusNodes = new(); // nodes to take power from
            List<IElectricalBlockEntity> deficitNodes = new(); // nodes to push power into

            ulong surplusPower = 0; // this IS dt rated power

            foreach (KeyValuePair<IElectricalBlockEntity,float> node in storageNodes)
            {
                if (node.Key.MaxPower == 0) continue;
                float nodepressure = node.Value; //(int)((node.Key.CurrentPower / (double)node.MaxPower) * 100);
                if (nodepressure >= (Math.Min(targetpressure + 2, 100f)))  // two percent 
                {
                    surplusNodes.Add(node.Key);
                    ulong tickpower = ((ulong)(node.Key.MaxPPS * dt));
                    if (tickpower > node.Key.CurrentPower) tickpower = node.Key.CurrentPower;
                    surplusPower += tickpower;
                    continue;
                }
                if (nodepressure < (targetpressure))
                {
                    deficitNodes.Add(node.Key);
                    continue;
                }
            }

            if (deficitNodes.Count == 0 || surplusNodes.Count == 0) return true;

            if (deficitNodes.Count > 0)
            {
                // this is how much power to give to each deficit node
                ulong powerpernode = surplusPower / ((ulong)deficitNodes.Count);
                // this is how much power to take from each surplus node

                ulong powerleftover = 0;
                foreach (IElectricalBlockEntity dnode in deficitNodes)
                {
                    powerleftover += dnode.ReceivePower(powerpernode, dt, false);
                }

                surplusPower -= powerleftover;
                ulong powertakepernode = surplusPower / ((ulong)surplusNodes.Count);
                ulong unfullfilledpower = 0;
                foreach (IElectricalBlockEntity snode in surplusNodes)
                {
                    unfullfilledpower += snode.ExtractPower(powertakepernode, dt, false);
                }
                if (unfullfilledpower > ((ulong)allNodes.Count)) // due to rounding issues... could this be < allNodes.Count maybe?
                {
                    api.Logger.Error($"VintEng: Storage Only Electric Network Rebalance tick has power mismatch of {unfullfilledpower} power.");
                }
            }
            return true;
        }

        public IEnumerable<WireNode> GetNodes()
        {
            return allNodes;
        }

        /// <summary>
        /// Delete the given List of WireNodes from the nodes on this network as they are invalid.
        /// </summary>
        /// <param name="nodes">List of WireNodes to remove</param>
        public void DeleteNodes(List<WireNode> nodes)
        {
            foreach (WireNode node in nodes)
            {
                this.allNodes.Remove(node);
            }
        }

        public bool IsFullyLoaded()
        {
            if (allNodes.Count == 0 || api == null || api.World.BlockAccessor == null) return true;
            foreach (WireNode node in allNodes)
            {
                if (node.blockPos == null) continue;
                if (!VEHelpers.IsChunkLoaded(api.World, node.blockPos)) return false;
            }            
            return true;
        }

        /// <summary>
        /// Stores the bare minimum data for the Electrical Network Nodes in this network.
        /// </summary>
        /// <param name="tree">Passed in TreeAttribute object</param>
        public void ToTreeAttributes(ITreeAttribute tree)
        {
            // Need to store:
            // NetworkID, Nodes:[BlockPos, AnchorIndex]
            tree.SetLong("networkid", NetworkID);
            tree.SetInt("numnodes", allNodes.Count);

            tree.SetBytes("allnodes", SerializerUtil.Serialize(allNodes.ToArray()));

            tree.SetString("networktier", PowerTier.ToString());
        }

        /// <summary>
        /// Builds the network from data passed in via TreeAttribute object.
        /// <br>BlockAccessor used to pull data not saved in the tree but needed to initialize the network.</br>
        /// </summary>
        /// <param name="tree">TreeAttribute</param>
        /// <param name="world">BlockAccessor</param>
        public void FromTreeAttributes(ITreeAttribute tree, IBlockAccessor world)
        {
            NetworkID = tree.GetLong("networkid", 0);
            int numnodes = tree.GetInt("numnodes", 0);
            string tier = tree.GetString("networktier", "LV");
            PowerTier = Enum.Parse<EnumElectricalPowerTier>(tier);
            if (allNodes != null) allNodes.Clear();

            // potential crashable line of code...
            allNodes = SerializerUtil.Deserialize<WireNode[]>(tree.GetBytes("allnodes")).ToList<WireNode>();
        }
    }
}

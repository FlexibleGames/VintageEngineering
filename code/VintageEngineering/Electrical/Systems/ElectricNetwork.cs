using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using ProtoBuf;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VintageEngineering.Electrical.Systems
{
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
        /// All of the nodes associated with this network, saved to disk.
        /// <br>Will contain nodes that are not consumers, producers, or storage.</br>
        /// </summary>
        [ProtoMember(1)]
        public List<WireNode> allNodes = new List<WireNode>();

        /// <summary>
        /// Generator nodes producting power, data not saved to disk.
        /// </summary>
        private List<IElectricalBlockEntity> producerNodes = new List<IElectricalBlockEntity>();

        /// <summary>
        /// Consumer nodes requiring power, data not saved to disk.
        /// </summary>
        private List<IElectricalBlockEntity> consumerNodes = new List<IElectricalBlockEntity>();
        /// <summary>
        /// Storage nodes also contains Transformer blocks and toggles, data not saved to disk.
        /// </summary>
        private List<IElectricalBlockEntity> storageNodes = new List<IElectricalBlockEntity>();

        //internal ElectricalNetworkMod enm;
        public ICoreServerAPI api;        
        private bool isDirty;        
        private bool isSleeping;
        private float sleepTimer;
        private long _networkID;

        [ProtoMember(2)]
        public long NetworkID { get => _networkID; set => _networkID = value; }
        public bool IsDirty { get => isDirty; set => isDirty = value; }

        public int NodeCount { get => allNodes.Count; }

        public bool IsSleeping => isSleeping;

        public ElectricNetwork()
        {
        }
        public ElectricNetwork(long _networkid, ICoreServerAPI _api)
        {
            api = _api;
            this.NetworkID = _networkid;
            this.isSleeping = false;
            sleepTimer = 0;
            
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
            
            IElectricalBlockEntity electricalBlockEntity = IElectricalBlockEntity.GetAtPos(blockAccessor, node.blockPos);

            if (electricalBlockEntity ==  null) { throw new Exception("Attempting to add Electrical Node that is NOT an ElectricalBlockEntity!"); }

            IWireNetwork wirenet = IWireNetwork.GetAtPos(blockAccessor, node.blockPos);
            if (wirenet != null && updateEntity)
            {
                wirenet.SetNetworkID(NetworkID);
            }

            allNodes.Add(node);

            // a Transformer is a special type of storage, it has more than one power tier connection.
            // Toggles will be another unique type of storage, one that can have > 1 connection to a single network tier
            switch (electricalBlockEntity.ElectricalEntityType)
            {
                case EnumElectricalEntityType.Consumer:
                    consumerNodes.Add(electricalBlockEntity);
                    if (consumerNodes.Count > 1) consumerNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    break;
                case EnumElectricalEntityType.Producer:
                    producerNodes.Add(electricalBlockEntity);
                    if (producerNodes.Count > 1) producerNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    break;
                case EnumElectricalEntityType.Toggle:
                case EnumElectricalEntityType.Storage:
                case EnumElectricalEntityType.Transformer:
                    storageNodes.Add(electricalBlockEntity);
                    if (storageNodes.Count > 1) storageNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    break;
                default: break;
            }
            blockAccessor.GetBlockEntity(node.blockPos).MarkDirty();
        }

        /// <summary>
        /// Join this Electric Network, used by nodes that left the network due to chunks unloading.<br/>
        /// It is vital the WireNode has a Block Position set.
        /// </summary>
        /// <param name="node">WireNode Joining</param>
        /// <param name="entity">IElectricalBlockEntity Joining</param>
        public void Join(WireNode node, IElectricalBlockEntity entity)
        {
            if (allNodes.Contains(node)) return; // can't join a network we're already apart of.

            allNodes.Add(node);
            switch (entity.ElectricalEntityType)
            {
                case EnumElectricalEntityType.Consumer:
                    consumerNodes.Add(entity);
                    if (consumerNodes.Count > 1) consumerNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    break;
                case EnumElectricalEntityType.Producer:
                    producerNodes.Add(entity);
                    if (producerNodes.Count > 1) producerNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    break;
                case EnumElectricalEntityType.Toggle:
                case EnumElectricalEntityType.Storage:
                case EnumElectricalEntityType.Transformer:
                    storageNodes.Add(entity);
                    if (storageNodes.Count > 1) storageNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    break;
                default: break;
            }
        }

        /// <summary>
        /// Leave this Electric Network due to chunk unloading.<br/>
        /// It is vital the WireNode has the Block Position set.
        /// </summary>
        /// <param name="node">WireNode Leaving</param>
        /// <param name="entity">IElectricBlockEntity Leaving.</param>
        public void Leave(WireNode node, IElectricalBlockEntity entity)
        {
            if (!allNodes.Contains(node)) return; // can't leave a network we're not apart of.
            allNodes.Remove(node);
            switch (entity.ElectricalEntityType)
            {
                case EnumElectricalEntityType.Consumer:
                    if (consumerNodes.Contains(entity)) { consumerNodes.Remove(entity); }
                    if (consumerNodes.Count > 1) consumerNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    break;
                case EnumElectricalEntityType.Producer:
                    if (producerNodes.Contains(entity)) { producerNodes.Remove(entity); }
                    if (producerNodes.Count > 1) producerNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    break;
                case EnumElectricalEntityType.Toggle:
                case EnumElectricalEntityType.Storage:
                case EnumElectricalEntityType.Transformer:
                    if (storageNodes.Contains(entity)) { storageNodes.Remove(entity); }
                    if (storageNodes.Count > 1) storageNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    break;
                default: break;
            }
        }

        public void RemoveNode(WireNode node, IBlockAccessor blockAccessor, bool updateEntity = true)
        {
            if (allNodes.Contains(node))
            {
                IElectricalBlockEntity electricalBlockEntity = IElectricalBlockEntity.GetAtPos(blockAccessor, node.blockPos);

                if (electricalBlockEntity == null) 
                { 
                    throw new Exception("Attempting to remove Electrical Node that is NOT an IElectricalBlockEntity!"); 
                }

                IWireNetwork wirenet = IWireNetwork.GetAtPos(blockAccessor, node.blockPos);
                if (wirenet != null) 
                {
                    allNodes.Remove(node);
                    // if the last node is removed, NetworkID entry is automatically removed.
                }
                
                switch (electricalBlockEntity.ElectricalEntityType) 
                {
                    case EnumElectricalEntityType.Consumer:
                        consumerNodes.Remove(electricalBlockEntity);
                        break;
                    case EnumElectricalEntityType.Producer:
                        producerNodes.Remove(electricalBlockEntity);
                        break;
                    case EnumElectricalEntityType.Toggle:
                    case EnumElectricalEntityType.Storage:                    
                    case EnumElectricalEntityType.Transformer:
                        storageNodes.Remove(electricalBlockEntity);
                        break;
                    default: break;
                }
                blockAccessor.GetBlockEntity(node.blockPos).MarkDirty();
            }
        }

        /// <summary>
        /// Completely clears all data for this network, hopefully without lost memory.
        /// </summary>
        public void Clear()
        {
            allNodes.Clear();
            consumerNodes.Clear();
            producerNodes.Clear();
            storageNodes.Clear();
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

            if (allNodes.Count == 1) return true; // one node, no need to tick it.

            if (producerNodes.Count == 0 &&
                storageNodes.Count == 0 &&
                consumerNodes.Count == 0)
            {
                // a network of all relays would have 0 of the above types, but allNodes would be > 0
                if (allNodes.Count == 0) return false; // there are zero nodes in this network, delete it.
                foreach (WireNode node in allNodes)
                {
                    // if the block position is invalid remove the network as it's bad data
                    if (api.World.BlockAccessor.GetBlockEntity(node.blockPos) is null) { return false; }
                    if (IElectricalBlockEntity.GetAtPos(api.World.BlockAccessor, node.blockPos) is null) { return false; }
                }
            }

            if (isSleeping)
            {
                sleepTimer += deltaTime;
                if (sleepTimer > 5)
                {
                    isSleeping = false;
                    sleepTimer = 0;
                }
                return true;
            }
            /* Not needed
            foreach (IElectricalBlockEntity entity in consumerNodes)
            {
                totalpowerwanted += (entity.MaxPower - entity.CurrentPower);
            }
            */
            if (producerNodes.Count > 0)
            {
                foreach (IElectricalBlockEntity entity in producerNodes)
                {
                    if (!entity.CanExtractPower) continue;
                    totalpoweringen += entity.RatedPower(deltaTime, false);
                }
            }
            if (storageNodes.Count > 0)
            {
                foreach (IElectricalBlockEntity entity in storageNodes)
                {
                    if (entity.CanExtractPower) totalinstorage += entity.RatedPower(deltaTime, false); // how much to extract
                    if (entity.CanReceivePower) totalstorageavailable += entity.RatedPower(deltaTime, true); // how much to insert, if available
                }
            }
            totalpoweroffered = totalpoweringen + totalinstorage;

            if (consumerNodes.Count > 0)
            {
                foreach (IElectricalBlockEntity entity in consumerNodes)
                {
                    if (totalpoweroffered == 0) break;
                    
                    totalpoweroffered = entity.ReceivePower(totalpoweroffered, deltaTime);
                }
            }
            // totalpoweroffered will have any excess power we didn't use, it could = 0
            ulong totalpowerused = (totalpoweringen + totalinstorage) - totalpoweroffered;
            
            if (consumerNodes.Count == 0 && producerNodes.Count == 0)
            {
                // edge case of a network ONLY having storage and/or transformer nodes
                if (storageNodes.Count > 1)
                {
                    // only run if there's more than one storage node in the network
                    foreach (IElectricalBlockEntity entity in storageNodes)
                    {
                        totalinstorage = entity.ReceivePower(totalinstorage, deltaTime);
                    }
                    // totalinstorage should = 0 at this point
                    return true;
                }
                else
                {
                    // sleep, there is nothing to simulate
                    isSleeping = true; // zzzzzzzzzzzz                    
                    return true;
                }
            }

            if (totalpowerused >= totalpoweringen)
            {
                // we used more power than generators were able to provide, storage was used
                totalstorageused = totalpowerused - totalpoweringen;
                if (producerNodes.Count > 0)
                {
                    foreach (IElectricalBlockEntity entity in producerNodes)
                    {
                        totalpowerused = entity.ExtractPower(totalpowerused, deltaTime);
                    }
                }
                foreach (IElectricalBlockEntity entity in storageNodes)
                {
                    if (totalpowerused == 0) break;
                    totalpowerused = entity.ExtractPower(totalpowerused, deltaTime);
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
                    if (producerNodes.Count > 0)
                    {
                        // remove all power from generators
                        foreach (IElectricalBlockEntity entity in producerNodes)
                        {
                            //totalpoweringen = entity.ExtractPower(totalpoweringen, false);
                            entity.CheatPower(true); // remove all power from generators, does not track or return any value.
                        }
                    }
                    foreach (IElectricalBlockEntity entity in storageNodes)
                    {
                        // push excess power into storage nodes
                        totalexcesspower = entity.ReceivePower(totalexcesspower, deltaTime);
                    }
                }
                else
                {
                    // leftover power exceeds storage capacity, remove only what is needed to fill storages
                    ulong totalpowerconsumed = totalpowerused + totalstorageavailable;
                    if (producerNodes.Count > 0)
                    {
                        foreach(IElectricalBlockEntity entity in producerNodes)
                        {
                            totalpowerconsumed = entity.ExtractPower(totalpowerconsumed, deltaTime);
                        }
                    }
                    foreach (IElectricalBlockEntity entity in storageNodes)
                    {
                        //totalstorageavailable = entity.ReceivePower(totalstorageavailable, false);
                        entity.CheatPower(); // fills power buffer in storage
                    }
                    // at this point, totalpowerconsumed should = 0 AND totalstorageavailable should = 0
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
            
            if (allNodes != null) allNodes.Clear();

            // potential crashable line of code... 
            allNodes = SerializerUtil.Deserialize<WireNode[]>(tree.GetBytes("allnodes")).ToList<WireNode>();
        }
    }
}

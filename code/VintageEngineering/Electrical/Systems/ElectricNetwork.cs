using System;
using System.Collections.Generic;
using System.Linq;
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
        long NetworkID { get; }

        /// <summary>
        /// Marks the Network dirty
        /// </summary>
        bool IsDirty { get; set; }

        /// <summary>
        /// Gets whether this network is sleeping.
        /// </summary>
        bool IsSleeping { get; }

        /// <summary>
        /// Add a node to this Network.
        /// <br>Automatically sorts internal Lists by node priority for network ticking.</br>
        /// <br>Also sets the new nodes NetworkID to the this networks ID.</br>
        /// </summary>
        /// <param name="node">WireNode to add</param>
        /// <param name="blockAccessor">BlockAccessor</param>
        void AddNode(WireNode node, IBlockAccessor blockAccessor);

        /// <summary>
        /// Remove Node From this Network
        /// <br>Resets to 0 the NetworkID of the node if no other connections exist.</br>
        /// </summary>
        /// <param name="node">WireNode to remove</param>
        /// <param name="blockAccessor">BlockAccessor</param>
        void RemoveNode(WireNode node, IBlockAccessor blockAccessor);

        /// <summary>
        /// Tick (Update) this network
        /// </summary>
        /// <param name="dt">Delta Time</param>
        void UpdateTick(float dt);

        /// <summary>
        /// Get all Nodes on this network.
        /// </summary>
        /// <returns>Enumerator</returns>
        IEnumerable<WireNode> GetNodes();
    }

    /// <summary>
    /// A single Electric Network
    /// </summary>
    [ProtoContract()]
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

        /// <summary>
        /// A valid network id should never be 0 or negative.
        /// </summary>
        [ProtoMember(2)]
        private long networkID;

        public ICoreServerAPI api;        
        private bool isDirty;        
        private bool isSleeping;
        private float sleepTimer;

        public long NetworkID { get { return networkID; } }
        public bool IsDirty { get => isDirty; set => isDirty = value; }

        public bool IsSleeping => isSleeping;

        public ElectricNetwork()
        {
        }
        public ElectricNetwork(long networkid, ICoreServerAPI _api)
        {
//            enm = mod;
            api = _api;
            this.networkID = networkid;
            this.isSleeping = false;
            sleepTimer = 0;
            
        }
        
        public void AddNode(WireNode node, IBlockAccessor blockAccessor)
        {
            if (allNodes.Contains(node))
            {
                return; // only one connection per node per block on a single network.
            }
            // in the case of a toggle (switch) both (all) anchors can be the same power tier
            // however they need to be seperate networks as a toggle that is OFF severs the connection
            // and I think it best to not merge and seperate the networks every time the toggle is switched
            
            IElectricalBlockEntity electricalBlockEntity = blockAccessor.GetBlockEntity(node.blockPos) as IElectricalBlockEntity;

            if (electricalBlockEntity ==  null) { throw new Exception("Attempting to add Electrical Node that is NOT an ElectricalBlockEntity!"); }

            IWireNetwork wirenet = blockAccessor.GetBlockEntity(node.blockPos) as IWireNetwork;
            if (wirenet != null)
            {
                wirenet.SetNetworkID(networkID);
            }

            allNodes.Add(node);

            // a Transformer is a special type of storage, it has more than one power tier connection.
            // Switches will be another unique type of storage, one that 
            // has > 1 connection to a single network tier, and can put to sleep the 'output' network.
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

        public void RemoveNode(WireNode node, IBlockAccessor blockAccessor)
        {
            if (allNodes.Contains(node))
            {
                IElectricalBlockEntity electricalBlockEntity = blockAccessor.GetBlockEntity(node.blockPos) as IElectricalBlockEntity;

                if (electricalBlockEntity == null) { throw new Exception("Attempting to remove Electrical Node that is NOT an IElectricalBlockEntity!"); }

                IWireNetwork wirenet = blockAccessor.GetBlockEntity(node.blockPos) as IWireNetwork;
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
            networkID = 0;
        }

        /// <summary>
        /// Wake the network up, will sleep again if conditions are right.
        /// </summary>
        public void Wakeup()
        {
            isSleeping = false;
            sleepTimer = 0;
        }

        public void UpdateTick(float deltaTime)
        {
            // The meat and 'tatos of the entire system.
            //ulong totalpowerwanted = 0;
            ulong totalpoweringen = 0;
            ulong totalpoweroffered = 0;
            ulong totalinstorage = 0;
            ulong totalstorageavailable = 0;
            ulong totalexcesspower = 0;
            ulong totalstorageused = 0;

            if (producerNodes.Count == 0 &&
                storageNodes.Count == 0 &&
                consumerNodes.Count == 0)
            {
                if (!UpdateNetwork(api.World.BlockAccessor)) return;
            }

            if (isSleeping)
            {
                sleepTimer += deltaTime;
                if (sleepTimer > 5)
                {
                    isSleeping = false;
                    sleepTimer = 0;
                }
                return;
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
                    totalpoweringen += entity.CurrentPower;
                }
            }
            if (storageNodes.Count > 0)
            {
                foreach (IElectricalBlockEntity entity in storageNodes)
                {
                    totalinstorage += entity.CurrentPower;
                    totalstorageavailable += entity.MaxPower - entity.CurrentPower;
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
                    return;
                }
                else
                {
                    // sleep, there is nothing to simulate
                    isSleeping = true; // zzzzzzzzzzzz                    
                    return;
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
                if (totalpowerused != 0)
                {
                    throw new Exception("VintEng: Did not consume all power used.");
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
        /// Call to re-process allNodes into their respective list buckets        
        /// </summary>
        /// <returns>False if chunk not loaded.</returns>
        public bool UpdateNetwork(IBlockAccessor blockAccessor)
        {
            if (allNodes.Count > 0)
            {                
                // if we have nodes, then reset and rebuild our lists
                if (consumerNodes != null) consumerNodes.Clear();
                else { consumerNodes = new List<IElectricalBlockEntity>(); }

                if (producerNodes != null) producerNodes.Clear();
                else { producerNodes = new List<IElectricalBlockEntity>(); }

                if (storageNodes != null) storageNodes.Clear();
                else { storageNodes = new List<IElectricalBlockEntity>(); }

                List<WireNode> nodestoremove = new List<WireNode>();

                foreach (WireNode node in allNodes)
                {
                    if (!api.World.IsFullyLoadedChunk(node.blockPos))
                    {
                        return false;
                    }
                    IElectricalBlockEntity entity = blockAccessor.GetBlockEntity(node.blockPos) as IElectricalBlockEntity;
                    if (entity == null) 
                    {
                        // invalid node, probably happened while testing, or due to a crash, we need to remove it.
                        nodestoremove.Add(node);
                        continue; 
                    } 

                    switch (entity.ElectricalEntityType)
                    {
                        case EnumElectricalEntityType.Consumer:
                            consumerNodes.Add(entity);
                            break;
                        case EnumElectricalEntityType.Producer:
                            producerNodes.Add(entity);
                            break;
                        case EnumElectricalEntityType.Toggle:
                        case EnumElectricalEntityType.Storage:
                        case EnumElectricalEntityType.Transformer:
                            storageNodes.Add(entity);

                            break;
                        default: break;
                    }
                }

                if (nodestoremove.Count > 0)
                {
                    if (nodestoremove.Count == allNodes.Count)
                    {
                        Clear(); // if there are zero valid nodes, clear the lot of them
                        api.ModLoader.GetModSystem<ElectricalNetworkMod>(true).manager.DeleteNetwork(NetworkID); // delete me!
                    }
                    else DeleteNodes(nodestoremove); // delete the nodes we don't need
                }
                if (consumerNodes.Count > 1) consumerNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                if (producerNodes.Count > 1) producerNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                if (storageNodes.Count > 1) storageNodes.Sort((x, y) => x.Priority.CompareTo(y.Priority));
            }
            else
            {
                // delete empty networks to avoid a 'memory leak' in save-game files.
                api.ModLoader.GetModSystem<ElectricalNetworkMod>().manager.DeleteNetwork(NetworkID);
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
            tree.SetLong("networkid", networkID);
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
            networkID = tree.GetLong("networkid", 0);
            int numnodes = tree.GetInt("numnodes", 0);
            
            if (allNodes != null) allNodes.Clear();

            // potential crashable line of code... 
            allNodes = SerializerUtil.Deserialize<WireNode[]>(tree.GetBytes("allnodes")).ToList<WireNode>();
        }
    }
}

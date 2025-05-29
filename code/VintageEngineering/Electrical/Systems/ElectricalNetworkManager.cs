using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VintageEngineering.Electrical.Systems
{
    /// <summary>
    /// Manages all Electric Networks in the currently loaded world. 
    /// <br>Processes the Simulation Ticks of every network.</br>
    /// <br>Subscribes to the CatenaryMod events to alter the networks its managing.</br>
    /// </summary>
    
    public class ElectricalNetworkManager
    {
        /// <summary>
        /// All the Electrical Networks on the save game.
        /// </summary>
        public Dictionary<long, ElectricNetwork> networks = new Dictionary<long, ElectricNetwork>();
    
        /// <summary>
        /// The next network ID that will be assigned to a new network.
        /// </summary>
        public long nextNetworkID;

        public ICoreServerAPI sapi;
        private ElectricalNetworkMod mod;
        private CatenaryMod cm;
        private bool _doNetworkTick = true;
        private long gameTickListener;
        // gameTickListener = sapi.Event.RegisterGameTickListener(OnGameTick, 200, 0);

        public ElectricalNetworkManager(ICoreServerAPI _api, ElectricalNetworkMod _mod)
        {
            sapi = _api;
            this.mod = _mod;
        }

        public void InitializeManger()
        {
            cm = sapi.ModLoader.GetModSystem<CatenaryMod>(true);
            if (cm != null)
            {
                cm.OnWireConnected += WireConnected;
                cm.OnWireRemoved += WireDisconnected;

                // 250ms means this gets called roughly 4 times per second. Maybe make this variable dependant on how many networks the system is tracking???
                gameTickListener = sapi.Event.RegisterGameTickListener(OnGameTick, 250, 0);
            }
            else
            {
                throw new Exception("ElectricalNetworkManger: Initialization Failed to get ModSystem : CatenaryMod");
            }
            VintageEngineeringMod vem = sapi.ModLoader.GetModSystem<VintageEngineeringMod>(true);
            _doNetworkTick = vem != null ? vem.CommonConfig.DoPowerTick : false;
            if (!_doNetworkTick)
            {
                if (vem == null)
                {
                    sapi.Logger.Debug("VintEng: Error when initializing ElectricNetworkManager, could not find VintageEngineeringMod.");
                }
                sapi.Logger.Debug("VintEng: Electric Network Ticking has been disabled by config. Set config value DoPowerTick to true to enable power distribution.");
            }
        }

        private void OnGameTick(float deltatime)
        {
            // deltatime is a value on how much time (seconds) have passed since the last call, value SHOULD always be less than 1. Ideally it would be 0.25.

            // if we have no networks, bounce
            if (networks.Count == 0 || !_doNetworkTick) { return; }

            // we have networks, lets tick them
            Stopwatch sw = Stopwatch.StartNew();
            foreach (IElectricNetwork net in networks.Values)
            {
                // now I know why networks would just stop working
                // previously if a node was unloaded it would return false, which this removes the network.
                if (!net.UpdateTick(deltatime))
                {
                    networks.Remove(net.NetworkID);
                }
            }
            sw.Stop();
            if (sw.ElapsedMilliseconds >= 500L)
            {
                sapi.Logger.Warning($"Electric Networks took {sw.ElapsedMilliseconds} to update!");
            }
        }

        /// <summary>
        /// Join a network outside of Catenary wire events.<br/>
        /// Typically because a nodes chunk was unloaded and then reloaded.<br/>
        /// Or during world load.
        /// </summary>
        /// <param name="netID">NetworkID to join</param>
        /// <param name="node">WireNode (MUST include a BlockPos)</param>
        /// <param name="entity">Entity joining</param>
        public void JoinNetwork(long netID, WireNode node, IElectricalBlockEntity entity)
        {
            if (!networks.ContainsKey(netID))
            {
                // create the network if it doesn't exist yet. Typical on World Load event.
                networks.Add(netID, new ElectricNetwork(netID, sapi));
            }
            networks[netID].Join(node, entity);
        }

        /// <summary>
        /// Leave a network outside of Catenary wire events.<br/>
        /// Typically because a nodes chunk was unloaded.<br/>
        /// Will not alter allNodes list of network.
        /// </summary>
        /// <param name="netID">NetworkID to leave</param>
        /// <param name="node">WireNode leaving (MUST include a BlockPos)</param>
        /// <param name="entity">Entity leaving</param>
        /// <param name="permanently">Removes the node from the networks node list.</param>
        public void LeaveNetwork(long netID, WireNode node, IElectricalBlockEntity entity, bool permanently = false)
        {
            if (networks.ContainsKey(netID))
            {
                if (permanently) networks[netID].RemoveNode(node, sapi.World.BlockAccessor, true);

                else networks[netID].Leave(node, entity);
            }
        }
        
        /// <summary>
        /// Vital Function called when the Catenary mod throws the OnWireRemoved event
        /// </summary>
        /// <param name="start">WireNode</param>
        /// <param name="end">WireNode</param>
        /// <param name="block">Block (wire) broken.</param>
        /// <param name="consumed">Set value to true to consume the event and prevent any further processing.</param>
        private void WireDisconnected(WireNode start, WireNode end, Block block, BoolRef consumed)
        {
            // this is hit when ANY wire connection is broken
            EnumWireFunction wfunction = Enum.Parse<EnumWireFunction>(block.Attributes["wirefunction"].AsString("None"));
            if (wfunction != EnumWireFunction.Power) return; // not a wire for power, bounce without consuming.

            IElectricalConnection startentity = IElectricalConnection.GetAtPos(sapi.World.BlockAccessor, start.blockPos);
            IElectricalConnection endentity = IElectricalConnection.GetAtPos(sapi.World.BlockAccessor, end.blockPos);

            if (startentity == null || endentity == null)
            {
                sapi.Logger.Error($"VintEng: Error removing Electric connection, start or end entity was not an IElectricalBlockEntity");
                return;
            }

            IWireNetwork startnet = IWireNetwork.GetAtPos(sapi.World.BlockAccessor, start.blockPos);
            IWireNetwork endnet = IWireNetwork.GetAtPos(sapi.World.BlockAccessor, end.blockPos);

            if (startnet == null || endnet == null)
            {
                sapi.Logger.Error($"VintEng: Error removing Electric connection, start or end entity was not an IElectricNetwork");
                return;
            }

            long startid = startnet.GetNetworkID(start.index);
            long endid = endnet.GetNetworkID(end.index);

            if (startid != endid)
            {
                sapi.Logger.Error($"VintEng: Somehow removing a connection with different networksid's. Should not be possible.");                
            }

            int numconstart = startentity.NumConnections(start.index); // how many connections does the start node have? should never be 0
            int numconend = endentity.NumConnections(end.index); // how many connections does the end node have? should never be 0

            startentity.RemoveConnection(start.index, end); // remove the connection from start
            endentity.RemoveConnection(end.index, start);   // remove the connection from end

            // mark 'em dirty!
//            sapi.World.BlockAccessor.GetBlockEntity(start.blockPos).MarkDirty();
//            sapi.World.BlockAccessor.GetBlockEntity(end.blockPos).MarkDirty();

            // if either node has only 1 connection, its an end point and no further processing is needed
            if (numconstart == 1)
            {
                // start node only has 1 connection
                networks[startid].RemoveNode(start, sapi.World.BlockAccessor); // remove the start node from network
                // networkIDs of the nodes are reset in the RemoveNode function of the ElectricNetwork
            }
            if (numconend == 1)
            {
                // end node only has one connection
                networks[startid].RemoveNode(end, sapi.World.BlockAccessor); // remove the end node from the network
                // networkIDs of the nodes are reset in the RemoveNode function of the ElectricNetwork

                if (networks[startid].allNodes.Count == 0)
                {
                    // edge case, network was only 2 nodes and we just broke the connection, remove the network.
                    networks[startid].Clear();
                    networks.Remove(startid);
                }
                consumed.value = true;
                return;
            }
            // if both nodes have > 1 connection, we need to split the networks
            if (numconstart > 1 && numconend > 1)
            {
                if (!SplitNetworks(startid, start, end))
                {
                    sapi.Logger.Error($"VintEng: Error Splitting ElectricNetwork at Position: {start.blockPos}");
                    return;
                }
                consumed.value = true;
                return;
            }
        }

        /// <summary>
        /// Vital function called by the Catenary mod OnWireConnected Event
        /// </summary>
        /// <param name="start">Start Node</param>
        /// <param name="end">End Node</param>
        /// <param name="block">Block (wire) used for the connection.</param>
        /// <param name="consumed">Set to true to consume (stop further calls for this connection event)</param>        
        private void WireConnected(WireNode start, WireNode end, Block block, BoolRef consumed)
        {
            EnumWireFunction wfunction = Enum.Parse<EnumWireFunction>(block.Attributes["wirefunction"].AsString("None"));
            if (wfunction != EnumWireFunction.Power) return; // not a wire for power, bounce without consuming.

            IElectricalConnection startentity = IElectricalConnection.GetAtPos(sapi.World.BlockAccessor, start.blockPos);
            IElectricalConnection endentity = IElectricalConnection.GetAtPos(sapi.World.BlockAccessor, end.blockPos);

            if (startentity == null  || endentity == null)
            {
                sapi.Logger.Error($"VintEng: Error adding Electric connection, start or end entity was not an IElectricalBlockEntity");
                return;
            }
            
            IWireNetwork startnet = IWireNetwork.GetAtPos(sapi.World.BlockAccessor, start.blockPos);
            IWireNetwork endnet = IWireNetwork.GetAtPos(sapi.World.BlockAccessor, end.blockPos);

            if (startentity == null || endentity == null)
            {
                sapi.Logger.Error($"VintEng: Error adding Electric connection, start or end entity was not an IElectricNetwork");
                return;
            }

            long startid = startnet.GetNetworkID(start.index);
            long endid = endnet.GetNetworkID(end.index);

            // add the connection references to the entities regardless of networkID status
            startentity.AddConnection(start.index, end); // add the connection to the start node
            endentity.AddConnection(end.index, start);   // add the connection to the end node            

            if (startid == 0 && endid == 0)
            {
                // edge case, both start and end id's == 0, meaning neither have a network.
                long netid = CreateNetwork(start, end);
                sapi.Logger.Debug($"VintEng: New ElectricNetwork created with ID: {netid}");
                // networkIDs of the nodes are set in the AddNode function of the ElectricNetwork
                consumed.value = true;
                return;
            }
            if (startid == endid)
            {
                // very edge case, if both nodes are ALREADY on the same network...
                // connection info already added above, no need to do anything else
                consumed.value = true;
                return;
            }
            if (startid != 0 && endid != 0)
            {
                // both nodes have a network already, merge the networks
                if (!MergeNetworks(startid, endid))
                {
                    sapi.Logger.Error($"VintEng: Error merging networks {startid} and {endid}");                    
                }
                return;
            }
            // if we're here, only one of the id's = 0, so propagate the existing network to it
            if (startid != 0)
            {
                // the startnode has the network, set and add the endnode network
                // networkIDs of the nodes are set in the AddNode function of the ElectricNetwork
                networks[startid].AddNode(end, sapi.World.BlockAccessor);
            }
            else
            {
                // endnode has the network
                // networkIDs of the nodes are set in the AddNode function of the ElectricNetwork
                networks[endid].AddNode(start, sapi.World.BlockAccessor);
            }
        }

        /// <summary>
        /// Merge network2 into network1, removing network2
        /// </summary>
        /// <param name="network1">Network 1</param>
        /// <param name="network2">Network 2</param>
        /// <returns>True if successful</returns>        
        public bool MergeNetworks(long network1,  long network2)
        {
            List<WireNode> nodesToProcess = networks[network2].allNodes;            
            // the ACTUAL wire connections do not change, just the network IDs and ElectricNetwork lists.
            foreach (WireNode node in nodesToProcess)
            {
                IWireNetwork wnode = IWireNetwork.GetAtPos(sapi.World.BlockAccessor, node.blockPos);
                if (wnode != null) 
                { 
                    // this sets the nodes networkID as well
                    networks[network1].AddNode(node, sapi.World.BlockAccessor);
                }
            }
            networks[network2].Clear(); // clear the lists in the network
            networks.Remove(network2);  // remove the network from the manager
            nodesToProcess.Clear();     // clear this list, just in case
            return true;
        }

        /// <summary>
        /// Split a network at a specific given connection.
        /// <br>New network (if valid) is created on the startnode side.</br>
        /// </summary>
        /// <param name="networkid">NetworkID to Split</param>
        /// <param name="startnode">Start Node to split at</param>
        /// <param name="endnode">End Node to split at</param>
        /// <returns>True if successful</returns>
        public bool SplitNetworks(long networkid, WireNode startnode, WireNode endnode)
        {
            // WireNodes are a unique pair of blockpos and index
            // so first thing, we need to know what nodes are on either side of the connection...
            List<WireNode> startsidenodes = new List<WireNode>();
            List<WireNode> nodestoProcess = new List<WireNode>();

            // if we add all the node connections from one side and we contain the other sides node,
            // then the network doesn't need to be split
            // and by the time we get in here, the connection has been removed from the block entities
            IElectricalConnection startent = IElectricalConnection.GetAtPos(sapi.World.BlockAccessor, startnode.blockPos);
            IElectricalConnection endent = IElectricalConnection.GetAtPos(sapi.World.BlockAccessor, endnode.blockPos);

            if (startent == null || endent == null)
            {
                sapi.Logger.Error($"VintEng: Error Splitting Network {networkid}; start or end nodes were not type IElectricalBlockEntity");
                return false;
            }
            startsidenodes.Add(startnode); // add the starting node, of course            
            nodestoProcess.AddRange(startent.GetConnections(startnode.index)); // set up to process connected nodes
            while (nodestoProcess.Count > 0) // potentially hazardous
            {
                List<WireNode> nodestoadd = new List<WireNode>(); // limit initally to 10 
                foreach (WireNode node in nodestoProcess)
                {
                    if (!startsidenodes.Contains(node))
                    {
                        startsidenodes.Add(node); // add node to BIG list
                    }
                    else
                    {
                        continue;
                    }
                    IElectricalConnection enode = IElectricalConnection.GetAtPos(sapi.World.BlockAccessor, node.blockPos);
                    if (enode == null) continue; // something bad happened, bounce to next one
                    nodestoadd.AddRange(enode.GetConnections(node.index)); // add this nodes connection to next list
                }
                nodestoProcess.Clear();
                if (nodestoadd.Count > 0) nodestoProcess.AddRange(nodestoadd);
            }

            
            if (startsidenodes.Contains(endnode))
            {
                // um... we have all the nodes on one side, but they include the other side,
                // the network must have another connection somewhere
                return true;
            }

            // startsidenodes has all the nodes for its half of the network..
            // remove the nodes from the network
            foreach (WireNode node in startsidenodes)
            {
                networks[networkid].RemoveNode(node, sapi.World.BlockAccessor);
            }
            // create a new network with all the seperated nodes
            long newnetid = CreateNetwork(startsidenodes);
            sapi.Logger.Debug($"VintEng: Split network {networkid}, new network {newnetid} created with {startsidenodes.Count} nodes.");
            return true;
        }

        /// <summary>
        /// Removes a network from the managed list.
        /// </summary>
        /// <param name="networkid"></param>
        public void DeleteNetwork(long networkid)
        {
            networks.Remove(networkid);
        }

        /// <summary>
        /// Creates a new network and returns the networkID assigned to it.
        /// </summary>
        /// <param name="node1">Node1</param>
        /// <param name="node2">Node2</param>
        /// <returns>NetworkID used</returns>
        public long CreateNetwork(WireNode node1, WireNode node2)
        {            
            ElectricNetwork network = new ElectricNetwork(nextNetworkID, sapi);            
            network.AddNode(node1, sapi.World.BlockAccessor);
            network.AddNode(node2, sapi.World.BlockAccessor);
            networks.Add(nextNetworkID, network);
            nextNetworkID++;
            return network.NetworkID;
        }

        public void CreateNetwork(long networkID, WireNode node)
        {
            if (!networks.ContainsKey(networkID))
            {
                // network with this id doesnt exist...
                networks.Add(networkID, new ElectricNetwork(networkID, sapi));
            }
            networks[networkID].AddNode(node, sapi.World.BlockAccessor, false);
        }

        /// <summary>
        /// Creates a new network from a list of nodes and returns the network ID.
        /// </summary>
        /// <param name="nodes">List of nodes to add to the new network.</param>
        /// <returns>NetworkID</returns>
        public long CreateNetwork(List<WireNode> nodes)
        {            
            ElectricNetwork newnet = new ElectricNetwork(nextNetworkID, sapi);            
            foreach (WireNode node in nodes)
            {
                newnet.AddNode(node, sapi.World.BlockAccessor);
            }
            networks.Add(nextNetworkID, newnet);
            nextNetworkID++;
            return newnet.NetworkID;
        }

        /// <summary>
        /// Creates a new network from an Array of nodes and returns the network ID.
        /// </summary>
        /// <param name="nodes">Array of WireNodes</param>
        /// <returns>NetworkID</returns>
        public long CreateNetwork(WireNode[] nodes)
        {
            return CreateNetwork(nodes.ToList<WireNode>());
        }        

        /// Initializes the electrical networks from a Serialized object that was saved to disk.
        /// <br>Will only be called by the server when loading a save-game</br>
        /// <param name="networkbytes">Byte Array from Saved (Serialized) network data.</param>
        /// <param name="idbytes">Byte Array from Saved (Serialized) nextnetworkid data.</param>
        public void InitializeNetworks(byte[] networkbytes, byte[] idbytes)
        {
            
            if (networks == null) networks = new Dictionary<long, ElectricNetwork>();
            else networks.Clear();
            
            if (networkbytes != null)
            {                
                List<long> netstodelete = new List<long>();
                try
                {
                    List<ElectricNetwork> netlist = SerializerUtil.Deserialize<List<ElectricNetwork>>(networkbytes);
                    foreach (ElectricNetwork net in netlist)
                    {
                        networks.Add(net.NetworkID, net);
                    }
                }
                catch (Exception)
                {
                    networks = SerializerUtil.Deserialize<Dictionary<long, ElectricNetwork>>(networkbytes);
                }
                nextNetworkID = SerializerUtil.Deserialize<long>(idbytes);
                foreach (KeyValuePair<long, ElectricNetwork> net in networks)
                {
                    if (net.Value.api == null) net.Value.api = this.sapi;
                    if (net.Value.NetworkID == 0)
                    {
                        netstodelete.Add(net.Key);
                    }
                    else net.Value.InitializeNetwork();
                }
                if (netstodelete.Count > 0)
                {
                    foreach (long netid in netstodelete)
                    {
                        networks.Remove(netid);
                    }
                }
            }
        }

        /// <summary>
        /// Package networks for saving to disk, converts from Dictionary to Byte[]
        /// </summary>
        /// <returns></returns>
        public byte[] NetworkBytes()
        {
            byte[] networkbytes = null;
            foreach (KeyValuePair<long, ElectricNetwork> nets in networks)
            {
                nets.Value.NetworkID = nets.Key; // DOUBLEY MAKING SURE THIS IS SET
            }
            networkbytes = SerializerUtil.Serialize(networks.Values.ToList<ElectricNetwork>());
            return networkbytes;
        }
    }
}

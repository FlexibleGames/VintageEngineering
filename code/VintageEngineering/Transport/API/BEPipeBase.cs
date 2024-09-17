using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VintageEngineering.Transport.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace VintageEngineering.Transport.API
{
    public abstract class BEPipeBase : BlockEntity
    {
        protected long _networkID;
        protected MeshData _meshData;
        //protected MeshRef _meshRef;
        protected bool _shapeDirty;               

        protected List<PipeConnection> _pushConnections;
        protected PipeExtractionNode[] extractionNodes; // uses BlockFacing index, N, E, S, W, U, D
        protected GUIPipeExtraction[] extractionGUIs; // uses BlockFacing index, N, E, S, W, U, D        

        protected int numExtractionConnections;
        protected int numInsertionConnections;

        private int numPushConsDebug = 0;
        private int numTickHandlerDebug = 0;

        protected bool[] connectionSides;   // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] extractionSides;   // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] disconnectedSides; // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] insertionSides;    // uses BlockFacing index, N, E, S, W, U, D

        public static string[] Faceletter = {"N", "E", "S", "W", "U", "D"};
        public virtual string ExtractDialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-pipeextract");
            }
        }

        /// <summary>
        /// Used by Extraction nodes to sort and push into based on settings.<br/>
        /// PipeConnection object contains a Distance variable set when this list is built.
        /// </summary>
        public List<PipeConnection> PushConnections
        { get { return _pushConnections; } }

        /// <summary>
        /// Number of extraction nodes for this pipe block<br/>
        /// If 0, this block doesn't need to tick.
        /// </summary>
        public int NumExtractionConnections
        { get { return numExtractionConnections; } }

        /// <summary>
        /// Number of insertion nodes for this pipe block.
        /// </summary>
        public int NumInsertionConnections
        { get { return numInsertionConnections; } }

        /// <summary>
        /// NetworkID assigned to this pipe block. Should not be 0.
        /// </summary>
        public long NetworkID
        {
            get { return _networkID; }
            set { _networkID = value; }
        }

        /// <summary>
        /// Sides which have a valid pipe->pipe connection available, uses BlockFacing index, N, E, S, W, U, D<br/>
        /// Pipe to pipe connections only, not insertion or extraction connections.
        /// </summary>
        public bool[] ConnectionSides
        {
            get { return connectionSides; }
        }
        /// <summary>
        /// Sides which are set to Extraction Mode, uses BlockFacing index, N, E, S, W, U, D
        /// </summary>
        public bool[] ExtractionSides
        { get { return extractionSides; } }

        /// <summary>
        /// Sides which have valid connections but the player disconnected them manually, uses BlockFacing index, N, E, S, W, U, D
        /// </summary>
        public bool[] DisconnectedSides
        { get { return disconnectedSides; } }
        /// <summary>
        /// Sides which have a valid block to insert into, does not include pipe->pipe connections.
        /// </summary>
        public bool[] InsertionSides
        { get { return insertionSides; } }

        public GUIPipeExtraction[] PipeExtractionGUIs => extractionGUIs;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);            
            extractionNodes ??= new PipeExtractionNode[6];
            connectionSides ??= new bool[6];
            extractionSides ??= new bool[6]; 
            disconnectedSides ??= new bool[6]; 
            insertionSides ??= new bool[6];

            MarkPipeDirty(api.World, true); // mark the pipe dirty to rebuild shape if needed

            for (int f = 0; f< 6; f++)
            {
                if (extractionNodes[f] != null)
                {
                    extractionNodes[f].Initialize(Api, Pos, ConvertIndexToFace(f).Code);
                    //if (api.Side == EnumAppSide.Server) extractionNodes[f].SetHandler(GetHandler());
                }
            }

            PipeNetworkManager pnm = api.ModLoader.GetModSystem<PipeNetworkManager>(true); // this only exists on the server
            if (pnm == null) return;

            if (numExtractionConnections > 0) 
            { 
                RebuildPushConnections(api.World, pnm.GetNetwork(NetworkID).PipeBlockPositions.ToArray());
                api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
            }
            if (api.Side == EnumAppSide.Server) MarkDirty(true);
        }        

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            //base.GetBlockInfo(forPlayer, dsc);
            string output = string.Empty;
            output += $"NetID: {NetworkID}" + System.Environment.NewLine;
            //if (!Api.World.EntityDebugMode)
            //{
            //    // TODO Uncomment when this feature is 'done'
            //    dsc.Append(output);
            //    return;
            //}
            string inserts = string.Empty;
            string extracts = string.Empty;
            string overrides = string.Empty;
            string pipecons = string.Empty;

            for (int f = 0; f < 6; f++)
            {
                if (insertionSides[f]) inserts += Faceletter[f] + (f != 5 ? ", " : "");
                if (extractionSides[f]) extracts += Faceletter[f] + (f != 5 ? ", " : "");
                if (disconnectedSides[f]) overrides += Faceletter[f] + (f != 5 ? ", " : "");
                if (connectionSides[f]) pipecons += Faceletter[f] + (f != 5 ? ", " : "");
            }
            output += $"Insert Sides: {inserts}" + System.Environment.NewLine;
            output += $"Extract Sides: {extracts}" + System.Environment.NewLine;
            output += $"Overrides: {overrides}" + System.Environment.NewLine;
            output += $"Pipe Cons: {pipecons}" + System.Environment.NewLine;
            output += $"# Ins/Extr: {numInsertionConnections}/{numExtractionConnections}";            
            if (numPushConsDebug != 0) output += Environment.NewLine + $"#Pushes: {numPushConsDebug}";
            if (numTickHandlerDebug != 0) output += Environment.NewLine + $"#Tickers: {numTickHandlerDebug}";
            dsc.Append(output);
        }
        /// <summary>
        /// Returns a BlockPos array of all Pipe positions that connect to this one.      
        /// </summary>
        /// <param name="skippos">BlockPos to ignore all connections to/from.</param>
        /// <returns>BlockPos array.</returns>
        public virtual BlockPos[] GetPipeConnections(BlockPos skippos = null)
        {
            List<BlockPos> connections = new List<BlockPos>();
            for (int f=0; f < 6; f++)
            {
                if (connectionSides[f])
                {
                    if (skippos != null && Pos.AddCopy(ConvertIndexToFace(f)) == skippos)
                    {
                        continue; 
                    }
                    else
                    {
                        connections.Add(Pos.AddCopy(ConvertIndexToFace(f)));
                    }
                }
            }
            return connections.ToArray();
        }
        /// <summary>
        /// Called when a player right clicks a pipe block.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="player">Player who interacted</param>
        /// <param name="selection">BlockSelection data</param>
        /// <returns>True if handled without issue.</returns>
        public virtual bool OnPlayerRightClick(IWorldAccessor world, IPlayer player, BlockSelection selection)
        {
            int faceindex = selection.SelectionBoxIndex;
            if (faceindex == 6)
            {
                // right clicked the center main pipe object.
                return true;
            }
            //if (Api.Side != EnumAppSide.Server) return true;

            // grab the network manager
            PipeNetworkManager pnm = Api.ModLoader.GetModSystem<PipeNetworkManager>(true);

            if (player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Item?.Tool == EnumTool.Wrench)
            {
                // player right clicked WITH a wrench
                // detect sneak
                bool sidevalid = false;
                if (player.Entity.Controls.Sneak)
                {
                    // if sneaking, remove/add the connection
                    // these if's are mutually exclusive
                    if (insertionSides[faceindex])
                    {
                        sidevalid = true;
                        // removing an insert node
                        insertionSides[faceindex] = false;
                        numInsertionConnections--;

                        PipeConnection contoremove = new PipeConnection(
                            Pos.AddCopy(ConvertIndexToFace(faceindex)),
                            ConvertIndexToFace(faceindex),
                            0);
                        // Update Network
                        if (pnm != null && NetworkID != 0)
                        {
                            // update network and remove the insert node from the lists on the network.
                            pnm.GetNetwork(NetworkID).QuickUpdateNetwork(world, contoremove, true);
                        }
                    }
                    if (extractionSides[faceindex] && extractionNodes[faceindex] != null)
                    {
                        sidevalid = true;
                        // remove extract node
                        if (extractionGUIs != null && extractionGUIs[faceindex] != null && extractionGUIs[faceindex].IsOpened())
                        {
                            extractionGUIs[faceindex].TryClose();
                            extractionGUIs[faceindex].Dispose();
                        }
                        extractionNodes[faceindex].OnNodeRemoved();
                        RemoveExtractionListener(faceindex);
                        extractionNodes[faceindex] = null;
                        extractionSides[faceindex] = false;
                        numExtractionConnections--;
                        // network need not be updated as we did not remove an insert
                    }
                    if (connectionSides[faceindex])
                    {
                        sidevalid = true;
                        // we're forcefully removing pipe-pipe connection
                        // we need to inform neighboring blocks
                        connectionSides[faceindex] = false;
                        int oppface = ConvertIndexToFace(faceindex).Opposite.Index;
                        BEPipeBase bepb = world.BlockAccessor.GetBlockEntity(Pos.AddCopy(ConvertIndexToFace(faceindex))) as BEPipeBase;
                        if (bepb != null)
                        {
                            bepb.OverridePipeConnectionFace(oppface, true);
                            if (pnm != null)
                            {
                                pnm.OnPipeConnectionOverride(world, Pos, selection, true);
                            }
                        }
                    }
                    if (disconnectedSides[faceindex])
                    {
                        // the side was manually overriden, we need to restore it gracefully
                        disconnectedSides[faceindex] = false;                        
                        if (pnm != null)
                        {
                            int oppface = ConvertIndexToFace(faceindex).Opposite.Index;
                            BEPipeBase bepb = world.BlockAccessor.GetBlockEntity(Pos.AddCopy(ConvertIndexToFace(faceindex))) as BEPipeBase;

                            if (bepb != null)
                            {                            
                                if (pnm.GetNetwork(NetworkID).NetworkPipeType == pnm.GetNetwork(bepb.NetworkID).NetworkPipeType)
                                {
                                    bepb.OverridePipeConnectionFace(oppface, false);
                                    pnm.OnPipeConnectionOverride(world, Pos, selection, false);
                                    bepb.MarkPipeDirty(world, true);
                                }
                            }
                            else
                            {
                                if (CanConnectTo(world, Pos.AddCopy(ConvertIndexToFace(faceindex)), ConvertIndexToFace(faceindex).Opposite))
                                {
                                    insertionSides[faceindex] = true;
                                    numInsertionConnections++;
                                    PipeConnection restored = new PipeConnection(
                                        Pos.AddCopy(ConvertIndexToFace(faceindex)),
                                        ConvertIndexToFace(faceindex), 0);
                                    pnm.GetNetwork(NetworkID).QuickUpdateNetwork(world, restored, false);
                                }
                            }                            
                        }                        
                    }
                    else
                    {
                        // side wasn't overridden, but we're doing so now!
                        // only set to true if the side connection was something valid as
                        // we don't want to override a side that was empty already.
                        if (sidevalid) 
                        { 
                            disconnectedSides[faceindex] = true; 
                        }
                    }                    
                    //world.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
                    MarkPipeDirty(world, true);
                }            
                else
                {
                    // otherwise swap connection type
                    if (insertionSides[faceindex])
                    {
                        // swap from insert -> extract
                        insertionSides[faceindex] = false;
                        extractionSides[faceindex] = true;
                        extractionNodes[faceindex] = new PipeExtractionNode();
                        //extractionNodes[faceindex].SetHandler(GetHandler());
                        extractionNodes[faceindex].Initialize(Api, Pos, ConvertIndexToFace(faceindex).Code);                         
                        numExtractionConnections++;
                        numInsertionConnections--;
                        // CHECK PushConnection list, build if empty
                        // This will be empty on world/chunk load as it is not saved to disk
                        PipeConnection contoremove = new PipeConnection(
                            Pos.AddCopy(ConvertIndexToFace(faceindex)),
                            ConvertIndexToFace(faceindex),
                            0);
                        if (pnm != null && NetworkID != 0)
                        {
                            pnm.GetNetwork(NetworkID).QuickUpdateNetwork(world, contoremove, true);
                        }

                        if (_pushConnections == null) // if this is null we're freshly loaded or a new extract node
                        {
                            if (pnm != null)
                            {
                                // a fresh node with a new list, need to build it
                                RebuildPushConnections(world, pnm.GetNetwork(_networkID)?.PipeBlockPositions.ToArray());
                            }
                        }
                    }
                    else // can't do an elseif here as it would ALWAYS be true after the first if above. 
                    {                        
                        if (extractionSides[faceindex])
                        {
                            // swap extract -> insert
                            extractionNodes[faceindex].OnNodeRemoved();
                            RemoveExtractionListener(faceindex);
                            extractionNodes[faceindex] = null;
                            if (extractionGUIs != null && extractionGUIs[faceindex] != null) 
                            {
                                if (extractionGUIs[faceindex].IsOpened()) extractionGUIs[faceindex].TryClose();
                                extractionGUIs[faceindex].Dispose();
                            }
                            numExtractionConnections--;
                            numInsertionConnections++;
                            extractionSides[faceindex] = false;
                            insertionSides[faceindex] = true;
                            // Switching it back doesn't do anything to the PushConnection list
                            // There could be other extraction nodes at this position
                            // it isn't saved to disk, so it will be discarded eventually.
                            // HOWEVER, since this node is now an insertion node, the other extraction
                            // nodes on the network need to be updated efficiently
                            PipeConnection contoadd = new PipeConnection(
                                Pos.AddCopy(ConvertIndexToFace(faceindex)),
                                ConvertIndexToFace(faceindex),
                                0);
                            // Update Network
                            if (pnm != null && NetworkID != 0)
                            {
                                // update network and add the insert node to the lists on the network.
                                pnm.GetNetwork(NetworkID).QuickUpdateNetwork(world, contoadd, false);
                            }
                        }
                    }
                }
                _shapeDirty = true;
                //MarkDirty(true);
            }
            if (player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible is ItemPipeUpgrade
                ||
                player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible is ItemPipeFilter)
            {
                // AutoSwap hand item into extraction node
                if (extractionSides[faceindex])
                {
                    extractionNodes[faceindex].OnRightClick(world, player);
                }
            }
            else if (player.InventoryManager.ActiveHotbarSlot.Empty)
            {
                // player right clicked with an empty hand                
                // Open GUI if it is an extraction node
                if (ExtractionSides[faceindex] && extractionNodes[faceindex] != null)
                {
                    if (Api.Side == EnumAppSide.Client)
                    {
                        if (extractionGUIs == null) extractionGUIs = new GUIPipeExtraction[6];

                        ToggleExtractionNodeDialog(player, faceindex, delegate
                        {
                            extractionGUIs[faceindex] = new GUIPipeExtraction($"{ExtractDialogTitle} {ConvertIndexToFace(faceindex).Code}", 
                                (PipeInventory)extractionNodes[faceindex].Inventory,
                                Pos, Api as ICoreClientAPI, this, extractionNodes[faceindex], faceindex);
                            extractionGUIs[faceindex].Update();
                            return extractionGUIs[faceindex];
                        });
                    }
                }
            }
            MarkDirty(true);
            return true;
        }
        /// <summary>
        /// Override to return the proper handler for this pipe type.
        /// </summary>
        /// <returns>ITransportHandler object.</returns>
        public virtual ITransportHandler GetHandler()
        {
            return null;
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            for (int f = 0; f < 6; f++)
            {
                if (extractionSides[f])
                {
                    if (extractionNodes[f] != null)
                    {
                        RemoveExtractionListener(f);
                        extractionNodes[f].OnNodeRemoved();                        
                    }
                }
            }
            if (_meshData != null) _meshData.Dispose();
            base.OnBlockBroken(byPlayer);
        }

        /// <summary>
        /// Rebuild the connection directions; for example, when a Neighbor block changes.
        /// </summary>
        /// <param name="world">WorldAccessor object</param>
        public virtual void MarkPipeDirty(IWorldAccessor world, bool dirtyshape = false)
        {
            _shapeDirty = dirtyshape;
            BlockPipeBase us = world.BlockAccessor.GetBlock(Pos) as BlockPipeBase;
            PipeNetworkManager pnm = Api.ModLoader.GetModSystem<PipeNetworkManager>(true);
            
            // Check all 6 sides
            // the order is N, E, S, W, U, D
            for (int f = 0; f < BlockFacing.ALLFACES.Length; f++)
            {
                Block dblock = world.BlockAccessor.GetBlock((Pos.AddCopy(BlockFacing.ALLFACES[f])), BlockLayersAccess.Default);
                BlockEntity dbe = world.BlockAccessor.GetBlockEntity(Pos.AddCopy(BlockFacing.ALLFACES[f]));
                BlockFacing fromface = BlockFacing.ALLFACES[f];

                // NEED to track NetworkID's of all faces, merge networks, join networks as needed.

                if (dblock.Id == 0) // face direction is air block, neither solid nor fluid
                {
                    // block is air, not a valid block to connect to.
                    if (extractionSides[f])
                    {
                        // while the block is air, we have an extraction node trying to connect to it                        
                        PipeExtractionNode penode = extractionNodes[f];
                        // Call OnNodeRemoved, drops the contents and removes tick listener.
                        if (penode != null)
                        {
                            RemoveExtractionListener(f);
                            penode.OnNodeRemoved();
                            extractionNodes[f] = null;
                            if (extractionGUIs != null && extractionGUIs[f] != null)
                            {
                                if (extractionGUIs[f].IsOpened()) extractionGUIs[f].TryClose();
                                extractionGUIs[f].Dispose();
                            }
                        }

                        numExtractionConnections--;
                        _shapeDirty = true;
                        extractionSides[f] = false;
                    }
                    if (disconnectedSides[f])
                    {
                        // connection was previously manually overridden, remove that flag
                        disconnectedSides[f] = false;
                    }
                    if (insertionSides[f])
                    {
                        PipeConnection removeinsert = new PipeConnection(Pos.AddCopy(fromface), fromface, 0);
                        if (pnm != null) pnm.GetNetwork(NetworkID).QuickUpdateNetwork(world, removeinsert, true);

                        numInsertionConnections--; // block is now air, nothing to insert into
                        insertionSides[f] = false;
                        _shapeDirty = true;
                    }
                    if (connectionSides[f])
                    {
                        connectionSides[f] = false;
                        _shapeDirty = true;
                    }
                }
                else
                {
                    // block is NOT air, meaning a valid block, could be fluid
                    // need to check the entity now
                    if (dblock is BlockPipeBase pipeb)
                    {
                        if (pipeb.PipeUse == us.PipeUse) // pipe use is the same as us?
                        {
                            BEPipeBase bepb = dbe as BEPipeBase;
                            if (bepb == null) continue;
                            if (!bepb.disconnectedSides[ConvertIndexToFace(f).Opposite.Index])
                            {
                                disconnectedSides[f] = false;
                            }
                            if (!disconnectedSides[f])
                            {
                                if (!connectionSides[f])
                                {
                                    connectionSides[f] = true;
                                    _shapeDirty = true;
                                }
                            }
                            if (connectionSides[f] && !bepb.ConnectionSides[BlockFacing.ALLFACES[f].Opposite.Index])
                            {
                                bepb.MarkPipeDirty(world, true);
                            }
                            continue;
                        }
                    }
                    else if (CanConnectTo(world, Pos.AddCopy(BlockFacing.ALLFACES[f]), BlockFacing.ALLFACES[f].Opposite))
                    {
                        if (!disconnectedSides[f] && !insertionSides[f] && !extractionSides[f])
                        {
                            insertionSides[f] = true;
                            PipeConnection newinsert = new PipeConnection(Pos.AddCopy(fromface), fromface, 0);
                            if (pnm != null && NetworkID != 0) pnm.GetNetwork(NetworkID).QuickUpdateNetwork(world, newinsert, false);
                            numInsertionConnections++;
                            _shapeDirty = true;
                        }
                    }
                }
            }
            if (_shapeDirty) MarkDirty(true);
        }

        /// <summary>
        /// Removes a tick listener from this pipe.
        /// </summary>
        /// <param name="faceIndex">0-5 BlockFacing index (N,E,S,W,U,D)</param>
        public virtual void RemoveExtractionListener(int faceIndex)
        {
            if (extractionNodes[faceIndex] == null) return; // faceindex is invalid.

            if (extractionNodes[faceIndex].ListenerID != 0)
            {
                RemoveExtractionTickEvent(extractionNodes[faceIndex].ListenerID);
                extractionNodes[faceIndex].ListenerID = 0;
            }
        }

        /// <summary>
        /// Rebuild this pipe blocks push connection list based on the given BlockPos array.<br/>
        /// BlockPos array should be all the block positions of the pipes in the network, not the connected machines.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pipenetwork">BlockPos array of all pipes in this network that have an insert connection.</param>
        public virtual void RebuildPushConnections(IWorldAccessor world, BlockPos[] pipenetwork)
        {
            if (pipenetwork != null && pipenetwork.Length > 0)
            {
                if (_pushConnections != null) _pushConnections.Clear();
                else _pushConnections = new List<PipeConnection>();

                foreach (BlockPos p in pipenetwork)
                {
                    BEPipeBase bep = world.BlockAccessor.GetBlockEntity(p) as BEPipeBase;
                    if (bep == null) continue;
                    for (int f = 0; f < 6; f++)
                    {
                        if (bep.insertionSides[f])
                        {
                            BlockFacing facing = ConvertIndexToFace(f);
                            int dist = Pos.ManhattenDistance(p.AddCopy(facing));
                            _pushConnections.Add(new PipeConnection(
                                p.AddCopy(facing), facing, dist));
                        }
                    }
                }
                if (_pushConnections != null && _pushConnections.Count > 1)
                {
                    _pushConnections.Sort((x, y) => x.Distance.CompareTo(y.Distance)); 
                }
                MarkDirty(true);
            }
        }
        /// <summary>
        /// Alter the pushConnection list strictly based on the given block position.<br/>
        /// Mainly used when a new pipe is placed or removed.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="altered">Pipe BlockPos either added or removed.</param>
        /// <param name="isRemove">True if removing pushConnections, otherwise false.</param>
        public virtual void AlterPushConnections(IWorldAccessor world, BlockPos altered, bool isRemove = false)
        {
            BEPipeBase alteredpipe = world.BlockAccessor.GetBlockEntity(altered) as BEPipeBase;
            if (alteredpipe == null) return;
            for (int f = 0; f < 6; f++)
            {
                if (extractionNodes[f] != null) extractionNodes[f].IsSleeping = true;

                if (alteredpipe.insertionSides[f])
                {
                    PipeConnection con = new PipeConnection(
                        altered.AddCopy(ConvertIndexToFace(f)),
                        ConvertIndexToFace(f),
                        Pos.ManhattenDistance(altered));
                    if (isRemove)
                    {
                        if (_pushConnections != null && _pushConnections.Count > 0)
                        { 
                            _pushConnections.Remove(con); 
                        }
                    }
                    else
                    {
                        if (_pushConnections == null)
                        {
                            _pushConnections = new List<PipeConnection>();
                        }
                        if (!_pushConnections.Contains(con)) _pushConnections.Add(con);
                    }
                }
            }
            for (int f = 0; f < 6; f++)
            {
                if (extractionNodes[f] != null)
                {
                    // in the case of RoundRobin extraction, altering the list FUBARs the enumerator
                    extractionNodes[f].ResetEnumerator(_pushConnections);
                    extractionNodes[f].IsSleeping = false;
                }
            }
            MarkDirty(true);
        }
        /// <summary>
        /// Add or remove a set of push connections for this pipe entity.<br/>
        /// Will recalculate the distance value before altering the list.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="cons">Connections to add or remove</param>
        /// <param name="isRemove">True to remove the given connections</param>
        public virtual void AlterPushConnections(IWorldAccessor world, PipeConnection[] cons, bool isRemove = false)
        {
            if (cons == null || cons.Length == 0 || _pushConnections == null) return; // sanity check
            for (int f = 0; f < 6; f++)
            {
                if (extractionNodes[f] != null)
                {
                    // in the case of RoundRobin extraction, altering the list FUBARs the enumerator
                    extractionNodes[f].IsSleeping = true;
                }
            }
            for (int x = 0; x < cons.Length; x++)
            {
                PipeConnection newcon = cons[x].Copy(Pos.ManhattenDistance(cons[x].Position));
                if (isRemove)
                {
                    _pushConnections.Remove(newcon);
                }
                else
                {
                    if (!_pushConnections.Contains(newcon))
                    {
                        _pushConnections.Add(newcon.Copy());                        
                    }
                }
            }
            for (int f = 0; f < 6; f++)
            {
                if (extractionNodes[f] != null)
                {
                    // in the case of RoundRobin extraction, altering the list FUBARs the enumerator
                    extractionNodes[f].ResetEnumerator(_pushConnections);
                    extractionNodes[f].IsSleeping = false;
                }
            }
            MarkDirty(true);
        }
            
        /// <summary>
        /// Called when a player overrides a pipe connection on a neighboring pipe.<br/>
        /// Bool value sets the disconnectedSides value for the given faceindex.
        /// </summary>
        /// <param name="faceindex">Face index to change.</param>
        public virtual void OverridePipeConnectionFace(int faceindex, bool newvalue)
        {            
            disconnectedSides[faceindex] = newvalue;
            if (connectionSides[faceindex] && newvalue) connectionSides[faceindex] = false;
            _shapeDirty = true;
            MarkDirty(true);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (_shapeDirty || _meshData == null) RebuildShape();

            if (_meshData != null) 
            { 
                mesher.AddMeshData(_meshData, 1);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Rebuilds the shape based on the connection flags, should ONLY be called when a neighbor block changes
        /// or the player changes a valid connection.<br/>
        /// Does NOT need to be called when adding extraction node upgrades or filters!
        /// </summary>
        public virtual void RebuildShape()
        {
            // reset the mesh if not null
            if (_meshData != null)
            {
                _meshData.Clear();
                _meshData.Dispose();
                (Api as ICoreClientAPI).Tesselator.TesselateBlock(this.Block, out _meshData);
            }
            else 
            {
                //_meshData = new MeshData(true); 
                //_meshData = (Api as ICoreClientAPI).TesselatorManager.GetDefaultBlockMesh(this.Block);
                (Api as ICoreClientAPI).Tesselator.TesselateBlock(this.Block, out _meshData);
            }

            for (int f = 0; f < BlockFacing.ALLFACES.Length; f++)
            {
                if (!disconnectedSides[f])
                {
                    if (connectionSides[f] || insertionSides[f])
                    {
                        // "vinteng:pipeconnections-connection-" + BlockFacing.ALLFACES[f].Code
                        Block conb = Api.World.BlockAccessor.GetBlock(new AssetLocation("vinteng:pipeconnections-connection-" + BlockFacing.ALLFACES[f].Code));
                        //MeshData testing = (Api as ICoreClientAPI).TesselatorManager.GetDefaultBlockMesh(conb);
                        if (conb != null)
                        {
                            MeshData _data = ConnectionMesh(conb.Shape);                            
                            if (_data != null)
                            { 
                                if (_meshData != null)
                                { 
                                    _meshData.AddMeshData(_data); 
                                }
                            }
                        }
                    }
                    if (extractionSides[f])
                    {
                        Block conb = Api.World.BlockAccessor.GetBlock(new AssetLocation("vinteng:pipeconnections-extraction-" + BlockFacing.ALLFACES[f].Code));
                        if (conb != null) _meshData.AddMeshData(ConnectionMesh(conb.Shape));
                    }
                }
            }
            _shapeDirty = false;
        }

        private MeshData ConnectionMesh(CompositeShape _shape)
        {
            MeshData output;
            //Shape shape = Api.Assets.TryGet(_shape.Base, true).ToObject<Shape>(null);                       

            if (_shape != null)
            {                
                (Api as ICoreClientAPI).Tesselator.TesselateShape(
                    Block,
                    (Api as ICoreClientAPI).TesselatorManager.GetCachedShape(_shape.Base),
                    out output,
                    _shape.RotateXYZCopy, null, null);
                return output;
            }
            return new MeshData(true);
        }

        /// <summary>
        /// Override to check the given block position to determine whether this pipe type can interface with it.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pos">Position to check</param>
        /// <param name="onFace">Which face are we looking to connect to.</param>
        /// <returns>True if pipe connection is supported.</returns>
        public virtual bool CanConnectTo(IWorldAccessor world, BlockPos pos, BlockFacing onFace = null)
        {
            return false;
        }

        /// <summary>
        /// Adds an extraction tick event for a single extraction node for this block entity.
        /// </summary>
        /// <param name="delayms">Required Tick Delay</param>
        /// <param name="tickEvent">Tick Handler Method</param>
        /// <returns>listenerID</returns>
        public long AddExtractionTickEvent(int delayms, Action<float> tickEvent)
        {
            if (Api.Side == EnumAppSide.Server)
            { return RegisterGameTickListener(tickEvent, delayms); }
            return 0;
        }
        /// <summary>
        /// Removes a ExtractionNode tick event from the pool.
        /// </summary>
        /// <param name="lid">ListenerID to remove.</param>
        public void RemoveExtractionTickEvent(long lid)
        {
            if (Api.Side == EnumAppSide.Server) 
            { 
                UnregisterGameTickListener(lid);
                MarkDirty(true); // need to push updated data to client
            }
        }

        public void ToggleExtractionNodeDialog(IPlayer player, int faceindex, CreateDialogDelegate onCreateDialog)
        {
            if (extractionGUIs == null)
            {
                extractionGUIs = new GUIPipeExtraction[6];
            }
            if (extractionGUIs[faceindex] == null)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                byte[] facebytes = SerializerUtil.Serialize(faceindex);
                extractionGUIs[faceindex] = (GUIPipeExtraction)onCreateDialog();
                extractionGUIs[faceindex].OnClosed += delegate ()
                {
                    extractionGUIs[faceindex].Dispose();
                    extractionGUIs[faceindex] = null;
                    capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, 1001, facebytes);
                    capi.Network.SendPacketClient(extractionNodes[faceindex].Inventory.Close(player));
                };
                extractionGUIs[faceindex].TryOpen();
                capi.Network.SendPacketClient(extractionNodes[faceindex].Inventory.Open(player));
                capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, 1000, facebytes);
                return;
            }
            extractionGUIs[faceindex].TryClose();
        }

        public override void OnBlockUnloaded()
        {
            for (int f = 0; f < 6; f++)
            {
                if (extractionNodes[f] != null)
                {
                    if (extractionNodes[f].ListenerID != 0)
                    {
                        RemoveExtractionListener(f); // removes the listener and sets ID to 0                        
                    }
                    if (extractionGUIs != null && extractionGUIs[f] != null)
                    {
                        if (extractionGUIs[f].IsOpened()) extractionGUIs[f].TryClose();
                        extractionGUIs[f].Dispose();
                    }
                    extractionNodes[f].OnBlockUnloaded(Api.World);
                }
            }
            // free up memory, just in case.
            if (numExtractionConnections > 0 && _pushConnections != null) _pushConnections.Clear();
            if (_meshData != null) _meshData.Dispose();
            base.OnBlockUnloaded(); // base call can also remove tick listeners
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data); // this just informs behaviors
            if (packetid == 1005)
            {
                TreeAttribute packet = TreeAttribute.CreateFromBytes(data); //SerializerUtil.Deserialize<TreeAttribute>(data);
                int facei = packet.GetInt("faceindex");
                //Packet_Client pc = new Packet_Client(); // SerializerUtil.Deserialize<Packet_Client>(packet.GetBytes("packet"));
                byte[] p = packet.GetBytes("packet");
                int pid = packet.GetInt("pid");
                //Packet_ClientSerializer.DeserializeBuffer(p, p.Length, pc);

                if (extractionNodes[facei] != null)
                {
                    ((PipeInventory)extractionNodes[facei].Inventory).InvNetworkUtil.HandleClientPacket(fromPlayer, pid, p);
                }

                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
                return;
            }
            if (packetid == 1000)
            {
                IPlayerInventoryManager ivm = fromPlayer.InventoryManager;
                if (ivm == null || data == null) return;
                int faceindex = SerializerUtil.Deserialize<int>(data);
                ivm.OpenInventory(extractionNodes[faceindex].Inventory);
                return;
            }
            if (packetid == 1001)
            {
                IPlayerInventoryManager ivm = fromPlayer.InventoryManager;
                if (ivm == null || data == null) return;
                int faceindex = SerializerUtil.Deserialize<int>(data);
                ivm.CloseInventory(extractionNodes[faceindex].Inventory);
                return;
            }
            if (packetid == 1003)
            {
                // drop down selection changed
                TreeAttribute tree = new TreeAttribute();
                tree.FromBytes(data);

                BlockPos testpos = tree.GetBlockPos("position");
                if (testpos != null && testpos == Pos)
                {
                    // just a check for debugging purposes to make sure the right block is updated
                    string face = tree.GetString("face", "error");
                    string distro = tree.GetString("distro", "error");
                    if (face == "error" || distro == "error")
                    {
                        throw new Exception("Error in PacketID 1003 for Pipe Distribution settings. Face and/or Distro mode is invalid.");
                    }
                    int faceindex = BlockFacing.FromCode(face).Index;
                    if (extractionNodes[faceindex] == null) return;
                    extractionNodes[faceindex].SetDistroMode(distro);
                    if (distro == "robin")
                    { 
                        extractionNodes[faceindex].PushEnumerator = _pushConnections.GetEnumerator(); 
                    }
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("networkid", _networkID);
            tree.SetBytes("extractsides", SerializerUtil.Serialize(extractionSides));
            for (int f = 0; f < 6; f++)
            {
                if (extractionSides[f])
                {
                    PipeExtractionNode node = extractionNodes[f];
                    TreeAttribute nodetree = new TreeAttribute();
                    node.ToTreeAttributes(nodetree);
                    tree.SetBytes("extract-" + f.ToString(), nodetree.ToBytes());
                }
            }
            tree.SetBytes("connectsides", SerializerUtil.Serialize(connectionSides));
            tree.SetBytes("disconnectsides", SerializerUtil.Serialize(disconnectedSides));
            tree.SetBytes("insertsides", SerializerUtil.Serialize(insertionSides));

            tree.SetInt("numextract", numExtractionConnections);
            tree.SetInt("numinsert", numInsertionConnections);

            // DEBUG STUFF HERE, remove before release!!
            if (TickHandlers != null) tree.SetInt("tickhandlers", this.TickHandlers.Count);
            if (_pushConnections != null) tree.SetInt("pushcons", this._pushConnections.Count);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            // this code is run:
            // a) by the server when a chunk/world loads one of these
            // b) by the client from data received from the server
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _networkID = tree.GetLong("networkid");
            extractionSides = SerializerUtil.Deserialize(tree.GetBytes("extractsides"), new bool[6]);
            extractionNodes ??= new PipeExtractionNode[6];
            for (int f = 0; f < 6; f++)
            {
                if (extractionSides[f])
                {
                    if (extractionNodes[f] != null)
                    {
                        extractionNodes[f].FromTreeAttributes(
                            TreeAttribute.CreateFromBytes(tree.GetBytes("extract-" + f.ToString())),
                            worldAccessForResolve);
                    }
                    else
                    {
                        extractionNodes[f] = new PipeExtractionNode();
                        if (Api != null) 
                        { 
                            extractionNodes[f].Initialize(Api, Pos, BlockFacing.ALLFACES[f].Code);                            
                        }
                        extractionNodes[f].FromTreeAttributes(
                            TreeAttribute.CreateFromBytes(tree.GetBytes("extract-" + f.ToString())),
                            worldAccessForResolve);
                    }                    
                }
            }
            connectionSides = SerializerUtil.Deserialize(tree.GetBytes("connectsides"), new bool[6]);
            disconnectedSides = SerializerUtil.Deserialize(tree.GetBytes("disconnectsides"), new bool[6]);
            insertionSides = SerializerUtil.Deserialize(tree.GetBytes("insertsides"), new bool[6]);

            numExtractionConnections = tree.GetInt("numextract");
            numInsertionConnections = tree.GetInt("numinsert");

            numPushConsDebug = tree.GetInt("pushcons", 0);
            numTickHandlerDebug = tree.GetInt("tickhandlers", 0);

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                MarkPipeDirty(worldAccessForResolve, true); 
            }
        }

        

        /// <summary>
        /// Converts a Face Index int into a BlockFacing direction.<br/>
        /// Returns NULL if the index is not 0 - 5<br/>
        /// In Order: N, E, S, W, U, D
        /// </summary>
        /// <param name="faceindex">Index of the face.</param>
        /// <returns>BlockFacing object or NULL if not valid.</returns>
        public static BlockFacing ConvertIndexToFace(int faceindex)
        {
            switch (faceindex)
            {
                case 0: return BlockFacing.NORTH;
                case 1: return BlockFacing.EAST;
                case 2: return BlockFacing.SOUTH;
                case 3: return BlockFacing.WEST;
                case 4: return BlockFacing.UP;
                case 5: return BlockFacing.DOWN;                
                default: return null;
            }
        }

        /// <summary>
        /// A quick check to determine if a chunk at a given position is loaded.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="atpos">BlockPos to check.</param>
        /// <returns>True if chuck is loaded.</returns>
        public static bool IsChunkLoaded(IWorldAccessor world, BlockPos atpos)
        {
            if (world.BlockAccessor.GetChunk(atpos.X / GlobalConstants.ChunkSize,
                atpos.InternalY / GlobalConstants.ChunkSize,
                atpos.Z /  GlobalConstants.ChunkSize) == null)
            {
                return false;
            }
            return true;
        }
    }
}

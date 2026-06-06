using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VintageEngineering.API;
using VintageEngineering.Transport.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace VintageEngineering.Transport.API
{
    public delegate GuiDialogGeneric CreateGenericDialogDelegate();

    public abstract class BEPipeBaseNew : BlockEntity
    {        
        protected MeshData _meshData;        
        protected bool _shapeDirty;

        /// <summary>
        /// Used when determining whether a pipe system is independant.
        /// </summary>
        public bool _graphDirty;

        /// <summary>
        /// Insert Nodes for just this Pipe, indexed by face index
        /// </summary>
        protected Dictionary<int, PipeInsertNode> _insertNodes;
        //protected GUIPipeInsertion[] _insertionGUIs;

        /// <summary>
        /// Extract Nodes for just this Pipe, indexed by face index
        /// </summary>
        protected Dictionary<int, PipeExtractionNode> _extractionNodes;
        protected GUIPipeExtractionNew[] _extractionGUIs;   // uses BlockFacing index, N, E, S, W, U, D
        protected GUIPipeInsertNode[] _insertGUIs;

        protected int numExtractionConnections;
        protected int numInsertionConnections;

        private int numPushConsDebug = 0;
        private int numTickHandlerDebug = 0;

        protected bool[] connectionSides;   // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] extractionSides;   // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] overriddenSides; // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] insertionSides;    // uses BlockFacing index, N, E, S, W, U, D

        public static string[] Faceletter = { "N", "E", "S", "W", "U", "D" };
        public virtual string ExtractDialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-pipeextract");
            }
        }
        public virtual string InsertDialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-pipeinsert");
            }
        }
        

        /// <summary>
        /// Used by Extraction nodes to sort and push into based on settings.<br/>
        /// PipeInsertNode object contains a Distance variable set when this list is built.
        /// </summary>
        public Dictionary<int, PipeInsertNode> InsertNodes
        { get { return _insertNodes; } }

        /// <summary>
        /// Pipe Extraction nodes contained in this Pipes BlockEntity
        /// </summary>
        public Dictionary<int, PipeExtractionNode> ExtractNodes => _extractionNodes;

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
        /// Returns the amount of pipe-pipe connections this block contains.<br/>
        /// Iterates through the connectionSides bool array.
        /// </summary>
        public int NumPipeConnections
        {
            get
            {
                if (connectionSides == null) return 0;
                int num = 0;
                for (int f = 0; f < 6; f++)
                {
                    if (connectionSides[f]) num++;
                }
                return num;
            }
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
        public bool[] OverriddenSides
        { get { return overriddenSides; } }
        /// <summary>
        /// Sides which have a valid block to insert into, does not include pipe->pipe connections.
        /// </summary>
        public bool[] InsertionSides
        { get { return insertionSides; } }

        public GUIPipeExtractionNew[] PipeExtractionGUIs => _extractionGUIs;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            //_insertNodes ??= new PipeInsertNode[6];
            //extractionNodes ??= new PipeExtractionNode[6];
            connectionSides ??= new bool[6];
            extractionSides ??= new bool[6];
            overriddenSides ??= new bool[6];
            insertionSides  ??= new bool[6];

            if (_extractionNodes != null && _extractionNodes.Count > 0)
            {
                foreach (KeyValuePair<int, PipeExtractionNode> pair in  _extractionNodes) 
                {
                    pair.Value.Initialize(Api, Pos, pair.Value.FaceCode);
                    if (api.Side == EnumAppSide.Server) pair.Value.MarkNodeDirty(false);
                }
            }
            if (api.Side == EnumAppSide.Server && _graphDirty)
            {
                // we are on the server, and just loaded from disk so graph is dirty...
                // propagate dirtiness
                ProcessDirtyGraph(api.World, Pos.Copy());
            }
        }

        public static void ProcessDirtyGraph(IWorldAccessor world, BlockPos start)
        {
            List<BlockPos> poked = new List<BlockPos>();
            List<BlockPos> topoke = new List<BlockPos>();

            topoke.Add(start.Copy());
            float delay = 0f;

            List<BlockPos> toadd = new List<BlockPos>();

            while (topoke.Count > 0)
            {
                foreach (BlockPos pos in topoke)
                {
                    if (!VEHelpers.IsChunkLoaded(world, pos)) continue;

                    BEPipeBaseNew bepipe = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pos);
                    if (bepipe == null) continue;

                    for (int f = 0; f < 6; f++)
                    {
                        if (bepipe.overriddenSides[f]) continue;
                        else
                        {
                            BlockPos conndir = pos.AddCopy(BlockFacing.ALLFACES[f]);

                            if (poked.Contains(conndir)) continue; // If we've already checked this, skip it

                            if (bepipe.connectionSides[f])  //world.BlockAccessor.GetBlock(conndir) is BlockPipeBaseNew)
                            {
                                if (!toadd.Contains(conndir)) toadd.Add(conndir);
                            }
                        }

                        if (bepipe._graphDirty) bepipe._graphDirty = false;

                        if (bepipe.extractionSides[f])
                        {
                            bepipe.ExtractNodes[f].MarkGraphDirty(delay);
                            delay += 1f;
                        }
                    }
                    poked.Add(pos.Copy());
                }
                topoke.Clear();
                if (toadd.Count > 0) topoke.AddRange(toadd);
                toadd.Clear();
            }
            topoke.Clear();
            toadd.Clear();
            poked.Clear();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {            
            string output = string.Empty;            
            if (ClientSettings.ExtendedDebugInfo || Api.World.EntityDebugMode)
            { 
                string inserts = string.Empty;
                string extracts = string.Empty;
                string overrides = string.Empty;
                string pipecons = string.Empty;

                for (int f = 0; f < 6; f++)
                {
                    if (insertionSides[f]) inserts += Faceletter[f] + (f != 5 ? ", " : "");
                    if (extractionSides[f]) extracts += Faceletter[f] + (f != 5 ? ", " : "");
                    if (overriddenSides[f]) overrides += Faceletter[f] + (f != 5 ? ", " : "");
                    if (connectionSides[f]) pipecons += Faceletter[f] + (f != 5 ? ", " : "");
                }
                output += $"Insert Sides: {inserts}" + System.Environment.NewLine;
                output += $"Extract Sides: {extracts}" + System.Environment.NewLine;
                output += $"Overrides: {overrides}" + System.Environment.NewLine;
                output += $"Pipe Cons: {pipecons}" + System.Environment.NewLine;
                output += $"# Ins/Extr: {numInsertionConnections}/{numExtractionConnections}";
                if (numPushConsDebug != 0) output += Environment.NewLine + $"#Pushes: {numPushConsDebug}";
                if (numTickHandlerDebug != 0) output += Environment.NewLine + $"#Tickers: {numTickHandlerDebug}";
            }
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
            for (int f = 0; f < 6; f++)
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
            //if (world.Api.Side == EnumAppSide.Client) return true;
            int faceindex = selection.SelectionBoxIndex;
            if (faceindex == 6)
            {
                // right clicked the _center main pipe object.
                return true;
            }

            if (player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Item?.Tool == EnumTool.Wrench)
            {
                // player right clicked WITH a wrench
                // detect sneak
                bool sidedisabled = false;
                if (player.Entity.Controls.Sneak)
                {
                    // if sneaking, remove/add the connection
                    // these if's are mutually exclusive
                    if (insertionSides[faceindex] && _insertNodes[faceindex] != null)
                    {
                        sidedisabled = true;
                        // removing an insert node
                        insertionSides[faceindex] = false;
                        numInsertionConnections--;
                        QuickUpdatePipeGraph(Api.World, Pos, [_insertNodes[faceindex]], true);
                        _insertNodes.Remove(faceindex);                        
                    }
                    else if (extractionSides[faceindex] && _extractionNodes[faceindex] != null)
                    {
                        sidedisabled = true;
                        // remove extract node
                        if (_extractionGUIs != null && _extractionGUIs[faceindex] != null && _extractionGUIs[faceindex].IsOpened())
                        {
                            _extractionGUIs[faceindex].TryClose();
                            _extractionGUIs[faceindex].Dispose();
                        }
                        RemoveExtractionListener(faceindex);
                        _extractionNodes[faceindex].OnNodeRemoved();                        
                        _extractionNodes.Remove(faceindex);
                        extractionSides[faceindex] = false;
                        numExtractionConnections--;                        
                    }
                    else if (connectionSides[faceindex])
                    {
                        sidedisabled = true;
                        // we're forcefully removing pipe-pipe connection
                        // we need to inform neighboring blocks
                        connectionSides[faceindex] = false;                        
                        int oppface = ConvertIndexToFace(faceindex).Opposite.Index;
                        BEPipeBaseNew bepb = world.BlockAccessor.GetBlockEntity(Pos.AddCopy(ConvertIndexToFace(faceindex))) as BEPipeBaseNew;
                        if (bepb != null)
                        {
                            bepb.OverridePipeConnectionFace(oppface, true);
                        }
                        // spamming this feature is NOT advised... which means someone will do it
                        RebuildPipeGraph(Api.World, Pos.Copy());
                        RebuildPipeGraph(Api.World, Pos.AddCopy(ConvertIndexToFace(faceindex)));
                        // I think a pipe system would have to be many hundreds of pipes to really push this into a laggy mess
                    }
                    else if (overriddenSides[faceindex])
                    {
                        // the side was manually overriden, we need to restore it gracefully                        
                        sidedisabled = false;
                        int oppface = ConvertIndexToFace(faceindex).Opposite.Index;
                        BEPipeBaseNew bepb = world.BlockAccessor.GetBlockEntity(Pos.AddCopy(ConvertIndexToFace(faceindex))) as BEPipeBaseNew;
                        if (bepb != null)
                        {
                            connectionSides[faceindex] = true;
                            bepb.OverridePipeConnectionFace(oppface, false);
                        }
                        else if (CanConnectTo(world, Pos.AddCopy(ConvertIndexToFace(faceindex)), ConvertIndexToFace(faceindex).Opposite))
                        {
                            insertionSides[faceindex] = true;
                            numInsertionConnections++;
                            _insertNodes.Add(faceindex, new PipeInsertNode(Pos.AddCopy(ConvertIndexToFace(faceindex)), ConvertIndexToFace(faceindex), string.Empty, 0));
                        }
                        overriddenSides[faceindex] = sidedisabled;
                        RebuildPipeGraph(Api.World, Pos.Copy());
                    }
                    overriddenSides[faceindex] = sidedisabled;
                    _shapeDirty = true;
                }
                else
                {
                    // otherwise swap connection type
                    if (insertionSides[faceindex])
                    {
                        _extractionNodes ??= new();
                        // swap from insert -> extract
                        insertionSides[faceindex] = false;
                        extractionSides[faceindex] = true;
                        _extractionNodes.Add(faceindex, new PipeExtractionNode());
                        //extractionNodes[faceindex].SetHandler(GetHandler());
                        _extractionNodes[faceindex].Initialize(Api, Pos, ConvertIndexToFace(faceindex).Code);
                        _extractionNodes[faceindex].RebuildConnectionsNow();
                        numExtractionConnections++;
                        numInsertionConnections--;
                        QuickUpdatePipeGraph(world, Pos, [_insertNodes[faceindex]], true);
                        _insertNodes.Remove(faceindex);
                        if (_insertGUIs != null && _insertGUIs[faceindex] != null)
                        {
                            if (_insertGUIs[faceindex].IsOpened()) _insertGUIs[faceindex].TryClose();
                            _insertGUIs[faceindex].Dispose();
                        }
                    }
                    else // can't do an elseif here as it would ALWAYS be true after the first if above. 
                    {
                        if (extractionSides[faceindex])
                        {
                            // swap extract -> insert
                            _extractionNodes[faceindex].OnNodeRemoved();
                            RemoveExtractionListener(faceindex);
                            _extractionNodes.Remove(faceindex);
                            if (_extractionGUIs != null && _extractionGUIs[faceindex] != null)
                            {
                                if (_extractionGUIs[faceindex].IsOpened()) _extractionGUIs[faceindex].TryClose();
                                _extractionGUIs[faceindex].Dispose();
                            }
                            numExtractionConnections--;
                            numInsertionConnections++;
                            extractionSides[faceindex] = false;
                            insertionSides[faceindex] = true;
                            _insertNodes ??= new();
                            _insertNodes.Add(faceindex, new PipeInsertNode(Pos.AddCopy(ConvertIndexToFace(faceindex)), ConvertIndexToFace(faceindex), string.Empty, 0));
                            QuickUpdatePipeGraph(world, Pos, [_insertNodes[faceindex]], false);
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
                    _extractionNodes[faceindex].OnRightClick(world, player);
                }
            }
            else if (player.InventoryManager.ActiveHotbarSlot.Empty)
            {
                // player right clicked with an empty hand
                // Open extraction GUI if it is an extraction node
                if (ExtractionSides[faceindex] && _extractionNodes[faceindex] != null)
                {
                    if (Api.Side == EnumAppSide.Client)
                    {
                        if (_extractionGUIs == null) _extractionGUIs = new GUIPipeExtractionNew[6];

                        ToggleExtractionNodeDialog(player, faceindex, delegate
                        {
                            _extractionGUIs[faceindex] = new GUIPipeExtractionNew($"{ExtractDialogTitle} {ConvertIndexToFace(faceindex).Code}",
                                (PipeInventory)_extractionNodes[faceindex].Inventory,
                                Pos, Api as ICoreClientAPI, this, _extractionNodes[faceindex], faceindex);
                            _extractionGUIs[faceindex].Update();
                            return _extractionGUIs[faceindex];
                        });
                    }
                }
                if (InsertionSides[faceindex] && _insertNodes[faceindex] != null)
                {
                    if (Api.Side == EnumAppSide.Client)
                    {
                        ICoreClientAPI capi = Api as ICoreClientAPI;
                        if (_insertGUIs == null) _insertGUIs = new GUIPipeInsertNode[6];
                        if (_insertGUIs[faceindex] == null)
                        {
                            ToggleInsertNodeDialog(player, faceindex, delegate
                            {
                                _insertGUIs[faceindex] = new GUIPipeInsertNode($"{InsertDialogTitle} {ConvertIndexToFace(faceindex).Code}",
                                    Pos, capi, this, _insertNodes[faceindex], faceindex);
                                _insertGUIs[faceindex].Update();
                                return _insertGUIs[faceindex];
                            });
                        }
                        else if (_insertGUIs[faceindex].IsOpened())
                        {
                            if (_insertGUIs[faceindex]._byPlayer == capi.World.Player) _insertGUIs[faceindex].TryClose();
                        }
                    }
                }
            }
            // keeping this one, players need to be updated on any interaction with a pipe
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
            int numPipeCons = NumPipeConnections;
            List<BlockPos> pipesConnected = new List<BlockPos>();

            for (int f = 0; f < 6; f++) // Clean up any connections on this block first
            {
                if (extractionSides[f])
                {
                    if (_extractionNodes[f] != null)
                    {
                        RemoveExtractionListener(f);
                        _extractionNodes[f].OnNodeRemoved();
                        _extractionNodes.Remove(f);
                        numExtractionConnections--;
                        extractionSides[f] = false;
                        if (_extractionGUIs != null && _extractionGUIs[f] != null && _extractionGUIs[f].IsOpened())
                        {
                            _extractionGUIs[f].TryClose();
                            _extractionGUIs[f].Dispose();
                            _extractionGUIs[f] = null;
                        }
                    }
                }
                if (connectionSides[f]) // keep a copy of all the pipes this is connected to
                {
                    pipesConnected.Add(Pos.AddCopy(BlockFacing.ALLFACES[f]));
                    connectionSides[f] = false;
                    //overriddenSides[f] = true;
                    // disconnect the other pipe to ensure this block isn't included in the graph search.
                    BEPipeBaseNew bepipe = Api.World.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(Pos.AddCopy(BlockFacing.ALLFACES[f]));
                    if (bepipe == null)
                    {
                        throw new Exception($"VintEng: When breaking pipe at {Pos.ToLocalPosition(Api)} pipe connection on face {BlockFacing.ALLFACES[f].Code} found a null Pipe BE in that direction.");
                    }
                    bepipe?.connectionSides[BlockFacing.ALLFACES[f].Opposite.Index] = false;
                    bepipe?._shapeDirty = true;
                }
                if (insertionSides[f])
                {
                    if (InsertNodes[f] != null)
                    {
                        insertionSides[f] = false;
                        InsertNodes.Remove(f);
                        numInsertionConnections--;
                        if (_insertGUIs != null && _insertGUIs[f] != null && _insertGUIs[f].IsOpened())
                        {
                            _insertGUIs[f].TryClose();
                            _insertGUIs[f].Dispose();
                            _insertGUIs[f] = null;
                        }
                    }
                }
            }
            if (pipesConnected.Count > 1) // this pipe was connected to more than one other pipe, possible split, this is going to be rough
            {
                if (pipesConnected.Count != numPipeCons)
                {
                    throw new Exception($"Exception when breaking block {base.Block.GetType().Name} at {Pos.ToLocalPosition(Api)}, Pipe connection count mis-match. Expected {numPipeCons} and found {pipesConnected.Count}");
                }
                foreach (BlockPos pipe in pipesConnected)
                {
                    // set all the connected pipes to dirty for the search
                    BEPipeBaseNew pipebe = Api.World.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pipe);
                    if (pipebe != null) pipebe._graphDirty = true;
                }
                IWorldAccessor world = Api.World;

                // while this is a heavy memory footprint, it only triggers when there is more than 1 pipe connection
                List<BlockPos> poked = new List<BlockPos>();
                List<BlockPos> topoke = new List<BlockPos>();
                List<PipeInsertNode> t_insertnodes = new List<PipeInsertNode>();
                List<BlockPos> t_extractnodes = new List<BlockPos>();
                List<BlockPos> toadd = new List<BlockPos>();

                while (pipesConnected.Count > 0)
                {
                    topoke.Add(pipesConnected[0].Copy()); // add the first pipe to check
                    pipesConnected.RemoveAt(0); // remove it to prevent infinite loops
                    BEPipeBaseNew pipebe = Api.World.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(topoke[0]);
                    if (pipebe != null) pipebe._graphDirty = false; // reset the dirty flag in that first element

                    while (topoke.Count > 0)
                    {
                        foreach (BlockPos pos in topoke)
                        {
                            if (!VEHelpers.IsChunkLoaded(world, pos)) continue;

                            BEPipeBaseNew bepipe = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pos);
                            if (bepipe == null) continue; // should this crash?
                            if (bepipe._graphDirty)
                            {
                                // we found one end-point is connected to another somewhere else in the graph.
                                // remove it from the list to search.
                                pipesConnected.Remove(pos);
                                bepipe._graphDirty = false; // we already poked this, now reset it
                            }
                            for (int f = 0; f < 6; f++)
                            {
                                if (bepipe.overriddenSides[f]) continue;
                                else
                                {
                                    BlockPos conndir = pos.AddCopy(BlockFacing.ALLFACES[f]);

                                    if (poked.Contains(conndir)) continue; // If we've already checked this, skip it

                                    if (bepipe.connectionSides[f])  //world.BlockAccessor.GetBlock(conndir) is BlockPipeBaseNew)
                                    {
                                        if (!toadd.Contains(conndir)) toadd.Add(conndir);
                                    }
                                }
                                if (bepipe.insertionSides[f])
                                {
                                    t_insertnodes.Add(new PipeInsertNode(pos.AddCopy(BlockFacing.ALLFACES[f]), BlockFacing.ALLFACES[f], string.Empty, 0));
                                }
                                if (bepipe.extractionSides[f] && !t_extractnodes.Contains(pos))
                                {
                                    t_extractnodes.Add(pos.Copy());
                                }
                            }
                            poked.Add(pos.Copy());
                        }
                        topoke.Clear();
                        if (toadd.Count > 0) topoke.AddRange(toadd);
                        toadd.Clear();
                    }
                    topoke.Clear();
                    toadd.Clear();
                    poked.Clear();

                    foreach (BlockPos pos in t_extractnodes)
                    {
                        BEPipeBaseNew bepn = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pos);
                        foreach (KeyValuePair<int, PipeExtractionNode> pair in bepn._extractionNodes)
                        {
                            pair.Value.MarkNodeDirty(t_insertnodes);
                        }
                    }
                    t_extractnodes.Clear();
                    t_insertnodes.Clear();
                }
            }
            else
            {
                // Pipe was connected to 1 or 0 other pipes
                if (pipesConnected.Count == 1)
                {
                    RemovePositionFromGraph(Api.World, pipesConnected[0], Pos);
                }
                // if it's 0, we don't care, nothing to do, the cleanup was done first-thing.
            }
            if (_meshData != null) _meshData.Dispose();
            base.OnBlockBroken(byPlayer);
        }

        /// <summary>
        /// Called when this pipe has a neighbor block change.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="us"></param>
        /// <param name="neighbor"></param>
        public virtual void PipeNeighborChanged(IWorldAccessor world, BlockPos us, BlockPos neighbor)
        {
            BlockFacing face = us.FacingFrom(neighbor);
            if (!VEHelpers.IsChunkLoaded(world, neighbor)) return;

            if (CanConnectTo(world, neighbor, face))
            {
                // someone placed something that has insert/extract potential
                _insertNodes ??= new();
                PipeInsertNode newinsert = new PipeInsertNode(neighbor, face.Opposite, string.Empty, 0);
                if (!_insertNodes.ContainsKey(face.Opposite.Index))
                {
                    _insertNodes.Add(face.Opposite.Index, newinsert);
                }
                insertionSides[face.Opposite.Index] = true;
                numInsertionConnections++;
                List<PipeInsertNode> newnodelist = [newinsert];
                QuickUpdatePipeGraph(world, us, newnodelist, false);
                _shapeDirty = true;
            }
            else
            {
                Block neighborblock = world.BlockAccessor.GetBlock(neighbor);
                if (neighborblock is BlockPipeBaseNew bpbn)
                {
                    if (bpbn.PipeUse == (base.Block as BlockPipeBaseNew).PipeUse)
                    {
                        BEPipeBaseNew neighBE = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(neighbor);
                        if (neighBE != null && !overriddenSides[face.Opposite.Index])
                        {
                            if (neighBE.overriddenSides[face.Index])
                            {
                                overriddenSides[face.Opposite.Index] = true;
                            }
                            else
                            {
                                connectionSides[face.Opposite.Index] = true;
                            }
                            _shapeDirty = true;
                        }
                    }
                }
                else if (neighborblock.Id == 0)
                {
                    // Someone broke a block that we may or may not have been connected to.
                    int faceto = face.Opposite.Index; // face index that was TOWARD the neighbor
                    if (connectionSides[faceto])
                    {
                        // we had a pipe connection in this direction
                        connectionSides[faceto] = false;
                        _shapeDirty = true;
                    }
                    else if (extractionSides[faceto])
                    {
                        // we had an extraction node in this direction
                        // clear and drop node contents                        
                        RemoveExtractionListener(faceto);
                        ExtractNodes[faceto].OnNodeRemoved();
                        ExtractNodes.Remove(faceto);
                        numExtractionConnections--;
                        extractionSides[faceto] = false;
                        _shapeDirty = true;
                    }
                    else if (insertionSides[faceto])
                    {
                        // we had an insertion node in this direction that is now air
                        PipeInsertNode toremove = new PipeInsertNode(neighbor.Copy(), face.Opposite, string.Empty, 0);
                        InsertNodes.Remove(faceto);
                        insertionSides[faceto] = false;
                        numInsertionConnections--;
                        //RemovePositionFromGraph(world, Pos, neighbor);
                        RemoveSingleInsertNode(world, us, toremove);
                        _shapeDirty = true;
                    }
                }
            }
            if (_shapeDirty)
            {
                MarkDirty(true);
            }
        }

        /// <summary>
        /// Rebuild the connection directions; for example, when a Neighbor block changes.
        /// </summary>
        /// <param name="world">WorldAccessor object</param>
        public virtual void NewPipePlaced(IWorldAccessor world, bool dirtyshape = false)
        {
            _shapeDirty = dirtyshape;
            BlockPipeBaseNew us = world.BlockAccessor.GetBlock(Pos) as BlockPipeBaseNew;
            int _numNewInserts = 0;
            int _numNeighborPipes = 0;
            // Check all 6 sides
            // the order is N, E, S, W, U, D
            for (int f = 0; f < BlockFacing.ALLFACES.Length; f++)
            {
                bool isLoaded = VEHelpers.IsChunkLoaded(Api.World, Pos.AddCopy(BlockFacing.ALLFACES[f]));
                Block dblock = world.BlockAccessor.GetBlock((Pos.AddCopy(BlockFacing.ALLFACES[f])), BlockLayersAccess.Default);
                BlockEntity dbe = world.BlockAccessor.GetBlockEntity(Pos.AddCopy(BlockFacing.ALLFACES[f]));
                BlockFacing fromface = BlockFacing.ALLFACES[f];

                if (!isLoaded) continue;

                if (dblock.Id != 0) // face direction is not air block, neither solid nor fluid
                {
                    // block is NOT air, meaning a valid block, could be fluid
                    // need to check the entity now
                    if (dblock is BlockPipeBaseNew pipeb)
                    {
                        if (pipeb.PipeUse == us.PipeUse) // pipe use is the same as us?
                        {
                            BEPipeBaseNew bepb = dbe as BEPipeBaseNew;
                            if (bepb == null) continue;
                            if (bepb.overriddenSides[ConvertIndexToFace(f).Opposite.Index])
                            {
                                overriddenSides[f] = true;
                            }
                            if (!overriddenSides[f])
                            {
                                if (!connectionSides[f])
                                {
                                    connectionSides[f] = true;
                                    _shapeDirty = true;
                                    _numNeighborPipes++;
                                }
                            }
                            continue;
                        }
                    }
                    else if (CanConnectTo(world, Pos.AddCopy(fromface), fromface.Opposite))
                    {                        
                        if (!overriddenSides[f])
                        {
                            _insertNodes ??= new Dictionary<int, PipeInsertNode>();

                            insertionSides[f] = true;
                            _numNewInserts++;
                            PipeInsertNode newinsert = new PipeInsertNode(Pos.AddCopy(fromface), fromface, string.Empty, 0);
                            _insertNodes.Add(f, newinsert);
                            
                            numInsertionConnections++;
                            _shapeDirty = true;
                        }
                    }
                }
            }
            if (_shapeDirty)
            {
                if (_numNeighborPipes > 1 || _numNewInserts > 0)
                {
                    if (_numNeighborPipes > 1)
                    {
                        // we are connecting two or more different pipe systems, rebuild all node lists
                        // Possibily Multithread this call...somehow
                        RebuildPipeGraph(world, Pos.Copy());
                    }
                    else
                    {
                        // we are simply adding new insert nodes
                        if (_numNeighborPipes > 0)
                        {
                            // new pipe has at least 1 pipe neighbor, update any extraction nodes with new inserts
                            QuickUpdatePipeGraph(world, Pos.Copy(), _insertNodes.Values.ToList(), false);
                        }
                    }
                }
                MarkDirty(true);
            }
        }

        /// <summary>
        /// Removes a tick listener from this pipe.
        /// </summary>
        /// <param name="faceIndex">0-5 BlockFacing index (N,E,S,W,U,D)</param>
        public virtual void RemoveExtractionListener(int faceIndex)
        {
            if (_extractionNodes == null || _extractionNodes.Count == 0) return;

            if (_extractionNodes.ContainsKey(faceIndex))
            {
                RemoveExtractionTickEvent(_extractionNodes[faceIndex].ListenerID);
                _extractionNodes[faceIndex].ListenerID = 0;
            }
        }

        /// <summary>
        /// Remove a single Insert Node toremove from a Pipe System start at fromPos.<br/>
        /// Removes any insert node that targets the provided nodes Position.<br/>
        /// Used when a block is broken that was insertable by this pipe system.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="fromPos">Postion to start search</param>
        /// <param name="toremove">Insert node to remove</param>
        public static void RemoveSingleInsertNode(IWorldAccessor world, BlockPos fromPos, PipeInsertNode toremove)
        {
            if (world.Api.Side == EnumAppSide.Client) return;
            List<BlockPos> poked = new List<BlockPos>();
            List<BlockPos> topoke = new List<BlockPos>();

            topoke.Add(fromPos.Copy());

            List<BlockPos> toadd = new List<BlockPos>();

            while (topoke.Count > 0)
            {
                foreach (BlockPos pos in topoke)
                {
                    if (!VEHelpers.IsChunkLoaded(world, pos)) continue;

                    BEPipeBaseNew bepipe = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pos);
                    if (bepipe == null) continue;

                    for (int f = 0; f < 6; f++)
                    {
                        if (bepipe.overriddenSides[f]) continue;
                        else
                        {
                            BlockPos conndir = pos.AddCopy(BlockFacing.ALLFACES[f]);

                            if (poked.Contains(conndir)) continue; // If we've already checked this, skip it

                            if (bepipe.connectionSides[f])  //world.BlockAccessor.GetBlock(conndir) is BlockPipeBaseNew)
                            {
                                if (!toadd.Contains(conndir)) toadd.Add(conndir);
                            }
                        }
                        if (bepipe.extractionSides[f])
                        {
                            bepipe.ExtractNodes[f].RemoveInsertNode(toremove);
                            bepipe.MarkDirty(true);
                        }
                    }
                    poked.Add(pos.Copy());
                }
                topoke.Clear();
                if (toadd.Count > 0) topoke.AddRange(toadd);
                toadd.Clear();
            }
            topoke.Clear();
            toadd.Clear();
            poked.Clear();
        }
        /// <summary>
        /// Remove a single BlockPos from a graph, is only used when a pipe on an end-point is broken, not in the middle.<br/>
        /// </summary>
        /// <param name="world"></param>
        /// <param name="fromPos"></param>
        public static void RemovePositionFromGraph(IWorldAccessor world, BlockPos fromPos, BlockPos toremove)
        {
            if (world.Api.Side == EnumAppSide.Client) return;

            List<BlockPos> poked = new List<BlockPos>();
            List<BlockPos> topoke = new List<BlockPos>();

            topoke.Add(fromPos.Copy());

            List<BlockPos> toadd = new List<BlockPos>();

            List<PipeExtractionNode> extractionNodes = new List<PipeExtractionNode>();

            while (topoke.Count > 0)
            {
                foreach (BlockPos pos in topoke)
                {
                    if (!VEHelpers.IsChunkLoaded(world, pos)) continue;

                    BEPipeBaseNew bepipe = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pos);
                    if (bepipe == null) continue;

                    for (int f = 0; f < 6; f++)
                    {
                        if (bepipe.overriddenSides[f]) continue;
                        else
                        {
                            BlockPos conndir = pos.AddCopy(BlockFacing.ALLFACES[f]);

                            if (poked.Contains(conndir)) continue; // If we've already checked this, skip it

                            if (bepipe.connectionSides[f])  //world.BlockAccessor.GetBlock(conndir) is BlockPipeBaseNew)
                            {
                                if (!toadd.Contains(conndir)) toadd.Add(conndir);
                            }
                        }
                        if (bepipe.extractionSides[f])
                        {
                            bepipe.ExtractNodes[f]?.RemoveInsertPosition(toremove);
                        }
                    }
                    poked.Add(pos.Copy());
                }
                topoke.Clear();
                if (toadd.Count > 0) topoke.AddRange(toadd);
                toadd.Clear();
            }
            topoke.Clear();
            toadd.Clear();
            poked.Clear();
        }

        /// <summary>
        /// Updates the Extraction Nodes from the given Position with the list of given PipeInsertNodes<br/>
        /// Called when a pipe is placed and it has new insert nodes but not connecting multiple systems.<br/>
        /// When using Node removal should only be given a SINGLE node in the list.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="fromPos">Starting Position of search</param>
        /// <param name="withNodes">New Nodes</param>
        /// <param name="isremove">Remove a node?</param>
        public static void QuickUpdatePipeGraph(IWorldAccessor world, BlockPos fromPos, List<PipeInsertNode> withNodes, bool isremove)
        {
            if (withNodes == null || withNodes.Count == 0) return; // sanity bounce
            if (world.Api.Side == EnumAppSide.Client) return;
            List<BlockPos> poked = new List<BlockPos>();
            List<BlockPos> topoke = new List<BlockPos>();

            topoke.Add(fromPos.Copy());

            List<BlockPos> toadd = new List<BlockPos>();

            while (topoke.Count > 0)
            {
                foreach (BlockPos pos in topoke)
                {
                    if (!VEHelpers.IsChunkLoaded(world, pos)) continue;

                    BEPipeBaseNew bepipe = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pos);
                    if (bepipe == null) continue;

                    for (int f = 0; f < 6; f++)
                    {
                        if (bepipe.overriddenSides[f]) continue;
                        else
                        {
                            BlockPos conndir = pos.AddCopy(BlockFacing.ALLFACES[f]);

                            if (poked.Contains(conndir)) continue; // If we've already checked this, skip it

                            if (bepipe.connectionSides[f])  //world.BlockAccessor.GetBlock(conndir) is BlockPipeBaseNew)
                            {
                                if (!toadd.Contains(conndir)) toadd.Add(conndir);
                            }
                        }                        
                        if (bepipe.extractionSides[f])
                        {
                            if (!isremove) bepipe.ExtractNodes[f].MarkNodeDirty(withNodes, true);
                            else
                            {
                                if (withNodes.Count == 0) continue;
                                PipeInsertNode thenode = withNodes.First();
                                bepipe.ExtractNodes[f].RemoveExactInsertNode(thenode.NodePosition, thenode.Facing.Index);
                                bepipe.MarkDirty(true);
                            }
                        }
                    }
                    poked.Add(pos.Copy());
                }
                topoke.Clear();
                if (toadd.Count > 0) topoke.AddRange(toadd);
                toadd.Clear();
            }
            topoke.Clear();
            toadd.Clear();
            poked.Clear();

        }

        /// <summary>
        /// Completely rebuilds all Extraction Nodes on this system with a list of Insert Nodes it finds.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="startpos"></param>
        public static void RebuildPipeGraph(IWorldAccessor world, BlockPos startpos)
        {
            if (world.Api.Side == EnumAppSide.Client) return;
            List<PipeInsertNode> t_insertnodes = new List<PipeInsertNode>();
            List<BlockPos> t_extractnodes = new List<BlockPos>();

            List<BlockPos> poked = new List<BlockPos>();
            List<BlockPos> topoke = new List<BlockPos>();

            topoke.Add(startpos.Copy());

            List<BlockPos> toadd = new List<BlockPos>();

            while (topoke.Count > 0)
            {
                foreach (BlockPos pos in topoke)
                {
                    if (!VEHelpers.IsChunkLoaded(world, pos)) continue;

                    BEPipeBaseNew bepipe = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pos);
                    if (bepipe == null) continue;

                    for (int f = 0; f < 6; f++)
                    {
                        if (bepipe.overriddenSides[f]) continue;
                        else
                        {
                            BlockPos conndir = pos.AddCopy(BlockFacing.ALLFACES[f]);

                            if (poked.Contains(conndir)) continue; // If we've already checked this, skip it

                            if (bepipe.connectionSides[f])  //world.BlockAccessor.GetBlock(conndir) is BlockPipeBaseNew)
                            {
                                if (!toadd.Contains(conndir)) toadd.Add(conndir);
                            }
                        }
                        if (bepipe.insertionSides[f])
                        {
                            t_insertnodes.Add(new PipeInsertNode(pos.AddCopy(BlockFacing.ALLFACES[f]), BlockFacing.ALLFACES[f], string.Empty, 0));
                        }
                        if (bepipe.extractionSides[f] && !t_extractnodes.Contains(pos))
                        {
                            t_extractnodes.Add(pos.Copy());
                        }
                    }
                    poked.Add(pos.Copy());
                }
                topoke.Clear();
                if (toadd.Count > 0) topoke.AddRange(toadd);
                toadd.Clear();
            }
            topoke.Clear();
            toadd.Clear();
            poked.Clear();

            foreach (BlockPos pos in t_extractnodes)
            {
                BEPipeBaseNew bepn = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pos);
                foreach (KeyValuePair<int, PipeExtractionNode> pair in bepn._extractionNodes)
                {
                    pair.Value.MarkNodeDirty(t_insertnodes);
                    pair.Value._graphDirty = false;
                }
            }
            t_extractnodes.Clear();
            t_insertnodes.Clear();
        }

        /// <summary>
        /// Builds a fresh List&lt;PipeInsertNode&gt; given a starting BlockPos of All Insert Nodes in this pipe system.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="fromPos">Starting Position</param>
        /// <returns></returns>
        public static List<PipeInsertNode> BuildInsertNodeList(IWorldAccessor world, BlockPos fromPos)
        {
            if (world.Api.Side == EnumAppSide.Client) return null;
            List<PipeInsertNode> output = new List<PipeInsertNode>();

            List<BlockPos> poked = new List<BlockPos>();
            List<BlockPos> topoke = new List<BlockPos>();

            topoke.Add(fromPos.Copy());

            List<BlockPos> toadd = new List<BlockPos>();

            while (topoke.Count > 0)
            {
                foreach (BlockPos pos in topoke)
                {
                    if (!VEHelpers.IsChunkLoaded(world, pos)) continue;

                    BEPipeBaseNew bepipe = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(pos);
                    if (bepipe == null) continue;

                    for (int f = 0; f < 6; f++)
                    {
                        if (bepipe.overriddenSides[f]) continue;
                        else
                        {                            
                            if (bepipe.connectionSides[f])
                            {
                                BlockPos conndir = pos.AddCopy(BlockFacing.ALLFACES[f]);
                                if (poked.Contains(conndir)) continue; // If we've already checked this, skip it
                                toadd.Add(conndir);
                            }
                        }
                        if (bepipe.insertionSides[f])
                        {
                            output.Add(new PipeInsertNode(pos.AddCopy(BlockFacing.ALLFACES[f]), BlockFacing.ALLFACES[f], string.Empty, fromPos.ManhattanDistance(pos.AddCopy(BlockFacing.ALLFACES[f]))));
                        }
                    }
                    poked.Add(pos.Copy());
                }
                topoke.Clear();
                if (toadd.Count > 0) topoke.AddRange(toadd);
                toadd.Clear();
            }
            topoke.Clear();
            toadd.Clear();
            poked.Clear();

            return output;
        }

        /// <summary>
        /// Called when a player overrides a pipe connection on a neighboring pipe.<br/>
        /// Bool value sets the overriddenSides value for the given faceindex.
        /// </summary>
        /// <param name="faceindex">Face index to change.</param>
        public virtual void OverridePipeConnectionFace(int faceindex, bool newvalue)
        {
            overriddenSides[faceindex] = newvalue;

            if (connectionSides[faceindex] == newvalue) connectionSides[faceindex] = !newvalue;
            _shapeDirty = true;
            // need this one for clients as it's important they see the change asap
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
                _meshData.Dispose();
                _meshData.Clear();
                (Api as ICoreClientAPI).Tesselator.TesselateBlock(this.Block, out _meshData); // creates the default shape mesh for the pipe.
            }
            else
            {
                //_heatableMesh = new MeshData(true); 
                //_heatableMesh = (Api as ICoreClientAPI).TesselatorManager.GetDefaultBlockMesh(this.Block);
                (Api as ICoreClientAPI).Tesselator.TesselateBlock(this.Block, out _meshData);
            }

            for (int f = 0; f < BlockFacing.ALLFACES.Length; f++)
            {
                if (!overriddenSides[f])
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
                        if (conb != null)
                        {
                            MeshData extnode = ConnectionMesh(conb.Shape);
                            if (extnode != null) _meshData.AddMeshData(extnode);
                        }
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
            return null;
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
                // not needed...
                //MarkDirty(true); // need to push updated data to client
            }
        }

        public void ToggleExtractionNodeDialog(IPlayer player, int faceindex, CreateDialogDelegate onCreateDialog)
        {
            _extractionGUIs ??= new GUIPipeExtractionNew[6];

            if (_extractionGUIs[faceindex] == null)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                TreeAttribute packet = new TreeAttribute();
                packet.SetInt("faceindex", faceindex);
                packet.SetString("node", "extract");
                byte[] sendbytes = packet.ToBytes();
                _extractionGUIs[faceindex] = (GUIPipeExtractionNew)onCreateDialog();
                _extractionGUIs[faceindex].OnClosed += delegate ()
                {
                    _extractionGUIs[faceindex].Dispose();
                    _extractionGUIs[faceindex] = null;
                    capi.Network.SendBlockEntityPacket(Pos.Copy(), 1001, sendbytes);
                    capi.Network.SendPacketClient(_extractionNodes[faceindex].Inventory.Close(player));
                };
                _extractionGUIs[faceindex].TryOpen();
                capi.Network.SendPacketClient(_extractionNodes[faceindex].Inventory.Open(player));
                capi.Network.SendBlockEntityPacket(Pos.Copy(), 1000, sendbytes);
                return;
            }
            _extractionGUIs[faceindex].TryClose();
        }

        public void ToggleInsertNodeDialog(IPlayer player, int faceindex, CreateGenericDialogDelegate onCreateDialog)
        {
            _insertGUIs ??= new GUIPipeInsertNode[6];
            
            if (_insertGUIs[faceindex] == null)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                TreeAttribute packet = new TreeAttribute();
                packet.SetInt("faceindex", faceindex);
                packet.SetString("node", "insert");
                byte[] sendbytes = packet.ToBytes();
                _insertGUIs[faceindex] = (GUIPipeInsertNode)onCreateDialog();
                _insertGUIs[faceindex].OnClosed += delegate ()
                {
                    _insertGUIs[faceindex].Dispose();
                    _insertGUIs[faceindex] = null;
                    capi.Network.SendBlockEntityPacket(Pos.Copy(), 1001, sendbytes);
                    //_capi.Network.SendPacketClient(_insertGUIs[faceindex]._subnetItem.Close(player));
                };
                bool opened = _insertGUIs[faceindex].TryOpen();
                //_capi.Network.SendPacketClient(_insertGUIs[faceindex]._subnetItem.Open(player));
                capi.Network.SendBlockEntityPacket(Pos.Copy(), 1000, sendbytes);
                return;
            }
            _insertGUIs[faceindex].TryClose();
        }

        public override void OnBlockUnloaded()
        {
            for (int f = 0; f < 6; f++)
            {
                if (_extractionNodes != null && _extractionNodes.ContainsKey(f) && _extractionNodes[f] != null)
                {
                    if (_extractionNodes[f].ListenerID != 0)
                    {
                        RemoveExtractionListener(f); // removes the listener and sets ID to 0 
                    }
                    if (_extractionGUIs != null && _extractionGUIs[f] != null)
                    {
                        if (_extractionGUIs[f].IsOpened()) _extractionGUIs[f].TryClose();
                        _extractionGUIs[f].Dispose();
                    }
                    _extractionNodes[f].OnBlockUnloaded(Api.World);
                }
            }
            // free up memory, just in case.
            if (numExtractionConnections > 0) 
            { 
                foreach (PipeExtractionNode node in _extractionNodes.Values)
                {
                    node.OnBlockUnloaded(Api.World);
                }
            }
            if (_meshData != null) _meshData.Dispose();
            base.OnBlockUnloaded(); // base call can also remove tick listeners
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data); // this just informs behaviors
            if (packetid == 1005) // PipeExtractionNode GUI inventory packet
            {
                TreeAttribute packet = TreeAttribute.CreateFromBytes(data); //SerializerUtil.Deserialize<TreeAttribute>(data);
                int facei = packet.GetInt("faceindex");
                //Packet_Client pc = new Packet_Client(); // SerializerUtil.Deserialize<Packet_Client>(packet.GetBytes("packet"));
                byte[] p = packet.GetBytes("packet");
                int pid = packet.GetInt("pid");
                //Packet_ClientSerializer.DeserializeBuffer(p, p.Length, pc);

                if (_extractionNodes[facei] != null)
                {
                    ((PipeInventory)_extractionNodes[facei].Inventory).InvNetworkUtil.HandleClientPacket(fromPlayer, pid, p);
                }

                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
                return;
            }
            else if (packetid == 5005)
            {
                // GUI custom subnet text update
                TreeAttribute packet = TreeAttribute.CreateFromBytes(data);
                int facei = packet.GetInt("faceindex");
                string type = packet.GetString("nodetype", string.Empty);
                string newsubnet = packet.GetString("customsubnet", string.Empty);
                if (type == "extract")
                {
                    ExtractNodes[facei].SubNet = newsubnet;
                }
            }
            else if (packetid == 5006)
            {
                // GUI custom subnet text update
                TreeAttribute packet = TreeAttribute.CreateFromBytes(data);
                int facei = packet.GetInt("faceindex");
                string type = packet.GetString("nodetype", string.Empty);
                string newsubnet = packet.GetString("customsubnet", string.Empty);
                if (type == "insert")
                {
                    InsertNodes[facei].SubNet = newsubnet;
                }
            }
            else if (packetid == 1006)
            {
                // GUI Custom ItemSlot change
                TreeAttribute packet = TreeAttribute.CreateFromBytes(data);
                int facei = packet.GetInt("faceindex");
                string type = packet.GetString("nodetype", string.Empty);
                string code = packet.GetString("code", string.Empty);
                if (code == "empty") code = string.Empty;
                if (type == "insert")
                {
                    InsertNodes[facei].SubNet = code;
                }
                else
                {
                    ExtractNodes[facei].SubNet = code;
                }
            }
            else if (packetid == 1000)
            {
                IPlayerInventoryManager ivm = fromPlayer.InventoryManager;
                if (ivm == null || data == null) return;
                TreeAttribute packet = TreeAttribute.CreateFromBytes(data);
                int faceindex = packet.GetInt("faceindex", 0);
                string type = packet.GetString("node", string.Empty);
                if (type != string.Empty)
                {
                    if (type == "insert")// && _insertGUIs[faceindex] != null)
                    {
                        //ivm.OpenInventory(_insertGUIs[faceindex]._subnetItem);
                    }
                    else
                    {
                        ivm.OpenInventory(_extractionNodes[faceindex].Inventory);
                    }
                }                
                return;
            }
            else if (packetid == 1001)
            {
                IPlayerInventoryManager ivm = fromPlayer.InventoryManager;
                if (ivm == null || data == null) return;
                TreeAttribute packet = TreeAttribute.CreateFromBytes(data);
                int faceindex = packet.GetInt("faceindex", 0);
                string type = packet.GetString("node", string.Empty);
                if (type != string.Empty)
                {
                    if (type == "insert")// && _insertGUIs[faceindex] != null)
                    {
                        //ivm.CloseInventory(_insertGUIs[faceindex]._subnetItem);
                    }
                    else
                    {
                        ivm.CloseInventory(_extractionNodes[faceindex].Inventory);
                    }
                }                
                return;
            }
            else if (packetid == 1003)
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
                    if (_extractionNodes[faceindex] == null) return;
                    _extractionNodes[faceindex].SetDistroMode(distro);
                    if (distro == "robin")
                    {
                        _extractionNodes[faceindex].ResetEnumerator();
                    }
                }
            }
            MarkDirty(true);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            int pushcons = 0;
            tree.SetBytes("extractsides", SerializerUtil.Serialize(extractionSides));
            if (NumExtractionConnections > 0)
            {
                foreach (PipeExtractionNode enode in _extractionNodes.Values)
                {
                    TreeAttribute nodetree = new TreeAttribute();
                    enode.ToTreeAttributes(nodetree);
                    tree.SetBytes("extract-" + enode.FaceCode, nodetree.ToBytes());
                    pushcons = enode.InsertNodes?.Count ?? 0;
                }
            }

            tree.SetBytes("connectsides", SerializerUtil.Serialize(connectionSides));
            tree.SetBytes("disconnectsides", SerializerUtil.Serialize(overriddenSides));
            tree.SetBytes("insertsides", SerializerUtil.Serialize(insertionSides));
            if (NumInsertionConnections > 0)
            {
                foreach (PipeInsertNode inode in _insertNodes.Values)
                {
                    TreeAttribute nodetree = new TreeAttribute();
                    inode.ToTreeAttributes(nodetree);
                    tree.SetBytes("insert-" + inode.Facing.Code, nodetree.ToBytes());
                }
            }

            tree.SetInt("numextract", numExtractionConnections);
            tree.SetInt("numinsert", numInsertionConnections);

            // DEBUG STUFF HERE, remove before release!!
            if (TickHandlers != null) tree.SetInt("tickhandlers", this.TickHandlers.Count);
            tree.SetInt("pushcons", pushcons);            
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            // this code is run:
            // a) by the server when a chunk/world loads one of these
            // b) by the client from data received from the server
            base.FromTreeAttributes(tree, worldAccessForResolve);
            extractionSides = SerializerUtil.Deserialize(tree.GetBytes("extractsides"), new bool[6]);

            int numExtractionBackup = NumExtractionConnections;
            int numInsertionBackup = NumInsertionConnections;
            int numPipeConsBackup = NumPipeConnections;

            numExtractionConnections = tree.GetInt("numextract", 0);
            numInsertionConnections = tree.GetInt("numinsert", 0);            

            if (numExtractionConnections > 0)
            {
                if (_extractionNodes != null)
                {
                    if (_extractionNodes.Count > 0) _extractionNodes.Clear();
                }
                else _extractionNodes = new Dictionary<int, PipeExtractionNode>();
                for (int f = 0; f < 6; f++)
                {
                    if (extractionSides[f])
                    {
                        PipeExtractionNode enode = new PipeExtractionNode();
                        if (Api != null) enode.Initialize(Api, Pos, BlockFacing.ALLFACES[f].Code);
                        enode.FromTreeAttributes(TreeAttribute.CreateFromBytes(tree.GetBytes("extract-" + ConvertIndexToFace(f).Code)), worldAccessForResolve);
                        _extractionNodes.Add(f, enode);
                    }
                }
            }

            connectionSides = SerializerUtil.Deserialize(tree.GetBytes("connectsides"), new bool[6]);
            overriddenSides = SerializerUtil.Deserialize(tree.GetBytes("disconnectsides"), new bool[6]);
            insertionSides = SerializerUtil.Deserialize(tree.GetBytes("insertsides"), new bool[6]);

            if (NumInsertionConnections > 0)
            {
                if (_insertNodes != null)
                {
                    if (_insertNodes.Count > 0) _insertNodes.Clear();
                }
                else _insertNodes = new Dictionary<int, PipeInsertNode>();
                for (int f = 0; f < 6; f++)
                {
                    if (insertionSides[f])
                    {
                        PipeInsertNode enode = new PipeInsertNode();
                        enode.FromTreeAttributes(TreeAttribute.CreateFromBytes(tree.GetBytes("insert-" + ConvertIndexToFace(f).Code)), worldAccessForResolve);
                        _insertNodes.Add(f, enode);
                    }
                }
            }

            numPushConsDebug = tree.GetInt("pushcons", 0);
            numTickHandlerDebug = tree.GetInt("tickhandlers", 0);

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                _shapeDirty = true;
            }
            else
            {
                // API is null OR we're on the server, either way, we are on the server.
                _graphDirty = true;
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
            if (faceindex < 6) return BlockFacing.ALLFACES[faceindex];
            else return null;
        }

    }
}

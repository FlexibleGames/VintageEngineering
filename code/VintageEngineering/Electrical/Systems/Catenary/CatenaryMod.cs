using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// Called when a wire is connected between two points.
    /// <br>If your mod needs to manage the network created by this event, subscribe to it.</br>
    /// </summary>
    /// <param name="start">Start WireNode</param>
    /// <param name="end">End WireNode</param>
    /// <param name="block">WireBlock used</param>
    /// <param name="consumed">Set to true to stop further calls.</param>    
    public delegate void OnWireConnectedDelegate(WireNode start, WireNode end, Block block, BoolRef consumed);
    /// <summary>
    /// Called when a wire connection is removed.
    /// <br>If your mod needs to manage the network associated with the Block type, subscribe to this.</br>
    /// </summary>
    /// <param name="start">Start WireNode</param>
    /// <param name="end">End WireNode</param>
    /// <param name="block">Block type of connection</param>
    /// <param name="consumed">Set to true to stop further calls.</param>
    public delegate void OnWireRemovedDelegate(WireNode start, WireNode end, Block block, BoolRef consumed);


    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CatenaryData
    {
        public HashSet<WireConnection> allConnections = new HashSet<WireConnection>();
    }

    /// <summary>
    /// System that tracks, saves/loads, and renders wires of any purpose in the game.
    /// <br>It's up to other mods to implement ModSystems to create and simulate the wires stored/created in this system.</br>    
    /// <br>Includes two events, one for adding wire connections and one for removing. Subscribe to these events in your Network mod to build & manage the connections.</br>
    /// </summary>
    public class CatenaryMod : ModSystem, IRenderer, IDisposable
    {        
        /// <summary>
        /// Workspaces by player for pending wire connections
        /// </summary>
        private Dictionary<string, WirePlacerWorkSpace> workspaceByPlayer = new Dictionary<string, WirePlacerWorkSpace>();
        public Matrixf ModelMat = new Matrixf();

        /// <summary>
        /// While rebuilt every run, this holds the default wire meshes for all wire types.
        /// </summary>
        private Dictionary<Block, MeshData> origWireMeshes = new Dictionary<Block, MeshData>();

        /// <summary>
        /// Renderer for all the placed wires in the world.
        /// </summary>
        public CatenaryRenderer WireRenderer;

        private CatenaryData data = new CatenaryData();

        private IServerNetworkChannel serverChannel;
        public IClientNetworkChannel clientChannel;

        /// <summary>
        /// Called when a wire is connected between two points.
        /// <br>If your mod needs to manage the network created by this event, subscribe to it.</br>
        /// </summary>
        /// <param name="start">Start Node</param>
        /// <param name="end">End Node</param>
        /// <param name="startIndex">Anchor Start Index</param>
        /// <param name="endIndex">Anchor End Index</param>
        /// <param name="block">WireBlock used</param>
        /// <param name="consumed">Set to true to stop further calls.</param>
        /// <param name="networkid">NetworkID assigned to this connection.</param>
        public event OnWireConnectedDelegate OnWireConnected;

        /// <summary>
        /// Called when a wire connection is removed.
        /// <br>If your mod needs to manage the network associated with the Block type, subscribe to this.</br>
        /// </summary>
        /// <param name="start">Start Node</param>
        /// <param name="end">End Node</param>
        /// <param name="startIndex">Anchor Start Index</param>
        /// <param name="endIndex">Anchor End Index</param>
        /// <param name="block">Block type of connection</param>
        /// <param name="consumed">Set to true to stop further calls.</param>
        public event OnWireRemovedDelegate OnWireRemoved;
              
        ICoreAPI api;
        ICoreServerAPI sapi;
        ICoreClientAPI capi;

        #region ModSystem

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public double RenderOrder { get { return 0.5; } } // IRenderer
        public int RenderRange { get { return 20; } }   // IRenderer

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            if (api.Side is EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
                clientChannel = capi.Network.RegisterChannel("catenarymod")
                    .RegisterMessageType(typeof(WireConnectionData))
                    .SetMessageHandler<WireConnectionData>(OnDataFromServer)
                    .RegisterMessageType(typeof(CatenaryData))
                    .SetMessageHandler<CatenaryData>(OnWireDataFromServer);
            }
            else
            {
                sapi = api as ICoreServerAPI;
                serverChannel = sapi.Network.RegisterChannel("catenarymod")
                    .RegisterMessageType(typeof(WireConnectionData))
                    .SetMessageHandler<WireConnectionData>(OnDataFromClient)
                    .RegisterMessageType(typeof(CatenaryData));
            }
            api.RegisterBlockClass("CatenaryBlockWire", typeof(BlockWire));            
            api.RegisterCollectibleBehaviorClass("CatenaryWireToolBehavior", typeof(BehaviorWireTool));
        }

        /// <summary>
        /// Update client with fresh catenary data, this will not contain meshes or meshrefs.
        /// </summary>
        /// <param name="packet">CatenaryData</param>
        private void OnWireDataFromServer(CatenaryData packet)
        {
            // the wire renderer holds a copy of this data that contains the mesh and VAO meshrefs
            // as the server doesn't care about those.
            this.data = packet;
            WireRenderer.UpdateWireMeshes(data); // this generates the meshes and uploads them for rendering.                        
        }

        /// <summary>
        /// Called ONLY on the server when a packet of type WireConnectionData is sent.
        /// </summary>
        /// <param name="fromPlayer">Player who sent the packet</param>
        /// <param name="packet">The packet of type WireConnectionData</param>
        private void OnDataFromClient(IServerPlayer fromPlayer, WireConnectionData packet)
        {
            if (packet != null)
            {
                if (packet.opcode == WireConnectionOpCode.Add)
                {
                    if (packet.connection != null)
                    {
                        AddConnection(packet.connection);
                    }
                }
                if (packet.opcode == WireConnectionOpCode.Remove || packet.opcode == WireConnectionOpCode.RemoveAll)
                {
                    RemoveConnection(packet);
                }
                if (packet.opcode == WireConnectionOpCode.Cancel)
                {
                    if (packet.playerUID != string.Empty)
                    {
                        EntityAgent agent = api.World.PlayerByUid(packet.playerUID).Entity;
                        CancelPlace(null, agent);
                    }
                }
            }
        }

        private void OnDataFromServer(WireConnectionData packet)
        {
            throw new NotImplementedException();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "wireplacer");
            
            capi.Event.ChunkDirty += OnChunkDirty;
            api.Event.BlockTexturesLoaded += onLoaded;
            api.Event.LeaveWorld += () =>
            {
                WireRenderer?.Dispose();
            };
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            sapi.Event.PlayerNowPlaying += Event_PlayerNowPlaying;            
        }        

        private void Event_PlayerNowPlaying(IServerPlayer byPlayer)
        {
            serverChannel.SendPacket<CatenaryData>(data, byPlayer);
        }

        private void Event_SaveGameLoaded()
        {
            byte[] bdata = sapi.WorldManager.SaveGame.GetData("catenarydata");
            try
            {
                if (bdata != null) data = SerializerUtil.Deserialize<CatenaryData>(bdata);
                else data = new CatenaryData();
            }
            catch (Exception e)
            {
                sapi.Logger.Error($"CatenaryMod: Error loading SaveGame data, its possible data does not exist yet.{System.Environment.NewLine} {e}");
                data = new CatenaryData();
            }
        }

        private void Event_GameWorldSave()
        {
            byte[] databytes = SerializerUtil.Serialize(data);
            sapi.WorldManager.SaveGame.StoreData("catenarydata", databytes);
            sapi.Logger.Debug($"CatenaryMod: Saved {databytes.Length} bytes of CatenaryData to savegame.");
        }

        private void OnChunkDirty(Vec3i chunkCoord, IWorldChunk chunk, EnumChunkDirtyReason reason)
        {
            if (reason == EnumChunkDirtyReason.NewlyLoaded)
            {
                WireRenderer.UpdateWireMeshes(data, chunkCoord); // only when loading chunks, not generating them
            }
        }
        private void onLoaded()
        {
            WireRenderer = new CatenaryRenderer(capi, this);
        }
        #endregion

        /// <summary>
        /// Returns a List of WireConnections from a specific WireNode at BlockSelection.Position
        /// </summary>
        /// <param name="node">BlockSelection to check</param>
        /// <returns>List of WireConnections</returns>
        public List<WireConnection> GetWireConnectionsAt(BlockSelection node) 
        {
            if (node == null) return null;
            List<WireConnection> toProcess = data.allConnections.Where(x => (x.NodeStart.blockPos == node.Position && x.NodeStart.index == node.SelectionBoxIndex) 
                                                                         || (x.NodeEnd.blockPos == node.Position && x.NodeEnd.index == node.SelectionBoxIndex) ).ToList();
            List<WireConnection> output = new List<WireConnection>();
            if (toProcess.Count == 0) return output; // an empty list
            foreach (WireConnection wire in toProcess)
            {
                if (wire.NodeStart.blockPos == node.Position || wire.NodeEnd.blockPos == node.Position)
                {
                    output.Add(new WireConnection(wire.VecStart, wire.VecEnd, wire.BlockId, wire.WireThickness, wire.NodeStart, wire.NodeEnd, wire.Block));
                } 
            }
            return output;
        }

        /// <summary>
        /// Returns the number of current connections at a specific WireNode
        /// </summary>
        /// <param name="node">WireNode to check</param>
        /// <returns>Number of connections, -1 if given node is null.</returns>
        public int GetNumberConnectionsAt(BlockSelection node)
        {
            if (node == null) return -1;
            return data.allConnections.Count(x => (x.NodeStart.blockPos == node.Position && x.NodeStart.index == node.SelectionBoxIndex)
                                               || (x.NodeEnd.blockPos == node.Position && x.NodeEnd.index == node.SelectionBoxIndex) );
        }

        /// <summary>
        /// Cancels the pending wire placement for the given player.
        /// </summary>
        /// <param name="blockwire">Block type player is holding</param>
        /// <param name="byEntity">Player</param>
        /// <returns></returns>
        public bool CancelPlace(BlockWire blockwire, EntityAgent byEntity)
        {
            WirePlacerWorkSpace ws = this.getWorkSpace(byEntity);
            if (ws.nowBuilding)
            {
                ws.nowBuilding = false;
                ws.startPos = null;
                ws.startOffset = null;
                ws.endOffset = null;                
//                ws.currentMesh = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called from wire block object to manage connections.
        /// <br>Will fire event OnWireConnected with start, end, and item slot information.</br>
        /// </summary>
        /// <param name="block">Block Player is Holding (Wire Type)</param>
        /// <param name="slot">Active ItemSlot</param>
        /// <param name="byEntity">Player</param>
        /// <param name="blocksel">Block Player is Looking At</param>
        public void OnInteract(Block block, ItemSlot slot, EntityAgent byEntity, BlockSelection blocksel)
        {
            if (blocksel == null) return; // not actually looking at a block
            WirePlacerWorkSpace ws = this.getWorkSpace(byEntity);

            IWireAnchor wiredBlock = api.World.BlockAccessor.GetBlock(blocksel.Position) as IWireAnchor;            

            if (wiredBlock == null)
            {
                // check this early on to save time                
                IServerPlayer splayer = api.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer;
                if (api.Side == EnumAppSide.Server) sapi.SendIngameError(splayer as IServerPlayer, "invalid", Lang.Get("gui-catenary-invalidblock"));
                return; // don't even try to connect if these are not wire enabled blocks
            }

            // Check permissions of the player
            if (!api.World.Claims.TryAccess( ((EntityPlayer)byEntity).Player, blocksel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                ((EntityPlayer)byEntity).Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!ws.nowBuilding) 
            {
                // we're NOT currenly connected to a start pos.
                ws.currentMesh = GetOrCreateWireMesh(block); // Gets the default mesh for this wire

                // we are looking at a wire connectable block

                // CanAttachWire is a vital call, all logic falls to network implimentations
                // Checks for valid connections based on wire type and WireAnchor types
                if (wiredBlock.CanAttachWire(api.World, block, blocksel))
                {
                    // wire connection is valid, lets connect it
                    ws.startPos = blocksel.Position.Copy();
                    ws.startOffset = wiredBlock.GetAnchorPosInBlock(blocksel.SelectionBoxIndex);
                    ws.startIndex = blocksel.SelectionBoxIndex;
                    ws.startNetID = -1;
                    ws.thickness = block.Attributes["thickness"].AsFloat(0.125f);
                    ws.maxlength = block.Attributes["maxlength"].AsInt(1);
                    ws.wireFunction = Enum.Parse<EnumWireFunction>(block.Attributes["wirefunction"].AsString());
                    ws.block = block;
                    ws.nowBuilding = true; // setting this triggers the renderer 
                    ws.endOffset = null;
                }                
                return;
            }
            // We are currently connected to a start position
            if (wiredBlock == null)
            {
                if (capi != null) capi.TriggerIngameError(this, "error", Lang.Get("gui-catenary-badstart"));
                CancelPlace(block as BlockWire, byEntity);
                return;
            }
            ws.nowBuilding = false;

            // start and end block pos are the same, bounce
            if (ws.startPos == blocksel.Position) 
            {
                if (capi != null) capi.TriggerIngameError(this, "cannotattach", Lang.Get("gui-catenary-connecttoself"));
                return; 
            }

            // can we attach the wire to the target selected?
            if (!wiredBlock.CanAttachWire(api.World, block, blocksel))
            {
                // wire connection is NOT valid...
                if (capi != null) capi.TriggerIngameError(this, "cannotattach", Lang.Get("gui-catenary-wireinvalid"));
                //CancelPlace(block as BlockWire, byEntity);
                return;
            }
            EntityPlayer eplr = byEntity as EntityPlayer;
            Vec3f nowEndOffset = GetEndOffset(eplr.Player, ws);

            // what length is the current pending wire?
            //int length = (int)Math.Ceiling((double)nowEndOffset.DistanceTo(ws.startOffset)); DOH!
            double length = Math.Ceiling(ws.startPos.DistanceTo(blocksel.Position));
            int maxlen = block.Attributes["maxlength"].AsInt(0);
            if (length < 0.05)
            {
                if (capi != null) capi.TriggerIngameError(this, "tooshort", Lang.Get("gui-catenary-wiretooshort"));
                return;
            }
            // bounce if our length exceeds the max for the wire
            if (length > maxlen)
            {
                if (capi != null) capi.TriggerIngameError(this, "lengthexceeded", Lang.Get("gui-catenary-wiretoolong"));
               // CancelPlace(block as BlockWire, byEntity);
                return;
            }

            IWireAnchor startBlock = api.World.BlockAccessor.GetBlock(ws.startPos) as IWireAnchor;
            IWireAnchor endBlock = api.World.BlockAccessor.GetBlock(blocksel.Position) as IWireAnchor;

            //new WireNode(BlockPos pos, int ind, int maxcon, EnumWireFunction funct, Vec3f conpoint)
            WireNode startnode = new WireNode(ws.startPos.Copy(), ws.startIndex,
                startBlock.GetMaxConnections(ws.startIndex), startBlock.GetWireFunction(ws.startIndex),
                startBlock.GetAnchorPosInBlock(ws.startIndex));

            WireNode endnode = new WireNode(blocksel.Position.Copy(), blocksel.SelectionBoxIndex,
                endBlock.GetMaxConnections(blocksel.SelectionBoxIndex), endBlock.GetWireFunction(blocksel.SelectionBoxIndex),
                endBlock.GetAnchorPosInBlock(blocksel.SelectionBoxIndex));

            // TODO : Check max connection values, length was already checked above

            EntityPlayer entityPlayer = byEntity as EntityPlayer;
            if (slot.StackSize < length && entityPlayer.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                if (capi != null) capi.TriggerIngameError(this, "notenoughitems", Lang.Get("gui-catenary-needwire", new object[] { length }));
                CancelPlace(block as BlockWire, byEntity);
                return;
            }
            else
            {
                slot.TakeOut((int)Math.Ceiling(length));
                slot.MarkDirty();
            }
            

            // If we're here, we can connect wire
            // public WireConnection(Vec3f vstart, Vec3f vend, int blockid, float wirethickness, WireNode start, WireNode end, Block wireBlock, MeshData mesh = null)

            WireConnection newconnection = new WireConnection(
                startnode.anchorPos, endnode.anchorPos,
                block.Id, block.Attributes["thickness"].AsFloat(),
                startnode, endnode, block);

            // send a packet to the server notifying it of a new connection
            if (api.Side == EnumAppSide.Client)
            {
                clientChannel.SendPacket<WireConnectionData>(new WireConnectionData()
                {
                    connection = newconnection,
                    playerUID = (byEntity as EntityPlayer).PlayerUID,
                    opcode = WireConnectionOpCode.Add
                });
            }
        }
        
        public void OnRenderFrame(float deltatime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque) return;

            WirePlacerWorkSpace ws = this.getWorkSpace(capi.World.Player.PlayerUID);
            
            // if we are not currently running a wire, we don't need to render anything
            if (!ws.nowBuilding) return;

            Vec3f nowEndOffset = this.GetEndOffset(capi.World.Player, ws);

            // Bounce if distance is very small
            if ((double)ws.startOffset.DistanceTo(nowEndOffset) < 0.1) return;

            // Bounce if distance is >= maxlength of wire
            int maxlength = ws.block.Attributes["maxlength"].AsInt(1);
            if ((double)ws.startOffset.DistanceTo(nowEndOffset) > maxlength) return;

            if (ws.endOffset != nowEndOffset)
            {
                ws.endOffset = nowEndOffset.Clone();
                reloadMeshRef(); // disposes of and recreates a new mesh
            }

            // Something bad happened with uploading mesh to GPU
            if (ws.currentMeshRef == null) return;

            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            AssetLocation wireTexture = new AssetLocation(ws.block.Attributes["texture"].AsString());
            int textureid = capi.Render.GetOrLoadTexture(wireTexture);

            rpi.BindTexture2d(textureid);
            IStandardShaderProgram shader = rpi.PreparedStandardShader(camPos.XInt, camPos.YInt, camPos.ZInt);
            shader.Use();
            shader.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            shader.ViewMatrix = rpi.CameraMatrixOriginf;
            shader.ModelMatrix = ModelMat.Values;

            Vec3d offset = ws.startPos.ToVec3d();
            ModelMat = ModelMat.Identity().Translate(offset.X - camPos.X,
                offset.Y - camPos.Y, offset.Z - camPos.Z);
            shader.ModelMatrix = ModelMat.Values;
            rpi.RenderMesh(ws.currentMeshRef);
            shader.Stop();

        }

        private void reloadMeshRef()
        {
            WirePlacerWorkSpace ws = this.getWorkSpace(this.capi.World.Player.PlayerUID);
            MeshRef currentMeshRef = ws.currentMeshRef;
            if (currentMeshRef != null)
            {
                currentMeshRef.Dispose();
            }
            Vec3f startOffset = ws.startOffset;
            Vec3f endOffset = ws.endOffset;            
            //MeshData currentMesh = ws.currentMesh;
            
            JsonObject attributes = ws.block.Attributes;
            float thickness = attributes["thickness"].AsFloat(0.015f);
            
            MeshData curmesh = ws.currentMesh;
            /*MeshData newmesh = ModSystemSupportBeamPlacer.generateMesh(startOffset, endOffset, null, curmesh, 
                (attributes != null) ? attributes["slumpPerMeter"].AsFloat(0.125f) : 0.125f);
            */
            MeshData newmesh = WireMesh.MakeWireMesh(startOffset, endOffset, thickness);
            newmesh.SetMode(EnumDrawMode.Triangles);
            //ws.currentMesh.Clear();
            //ws.currentMesh = newmesh;
            ws.currentMeshRef = this.capi.Render.UploadMesh(newmesh);
        }

        private WirePlacerWorkSpace getWorkSpace(EntityAgent forEntity)
        {
            EntityPlayer entityPlayer = forEntity as EntityPlayer;
            return this.getWorkSpace((entityPlayer != null) ? entityPlayer.PlayerUID : null);
        }
        
        private WirePlacerWorkSpace getWorkSpace(string playerUID)
        {
            WirePlacerWorkSpace ws;
            if (this.workspaceByPlayer.TryGetValue(playerUID, out ws))
            {
                return ws;
            }
            return this.workspaceByPlayer[playerUID] = new WirePlacerWorkSpace();
        }
        
        /// <summary>
        /// Returns default MeshData of given Wire Block.
        /// </summary>
        /// <param name="block">Block Mesh to pull.</param>
        /// <returns>MeshData</returns>
        public MeshData GetOrCreateWireMesh(Block block)
        {
            if (this.capi == null)
            {
                return null;
            }
            MeshData meshData;
            if (!this.origWireMeshes.TryGetValue(block, out meshData))
            {
                // this allows the use of the texture of the shape
                this.capi.Tesselator.TesselateShape(block, this.capi.TesselatorManager.GetCachedShape(block.Shape.Base), out meshData, null, null, null);
                this.origWireMeshes[block] = meshData;
                return meshData;
            }
            return meshData;
        }
        
        /// <summary>
        /// Returns the EndPosition Vec3f of a pending wire depending on what the player is looking at.
        /// <br>If player isn't looking at a wire anchor node, the wire should hover in front of them.</br>
        /// </summary>
        /// <param name="player">Player</param>
        /// <param name="ws">WirePlacerWorkSpace</param>
        /// <returns></returns>
        public Vec3f GetEndOffset(IPlayer player, WirePlacerWorkSpace ws)
        {
            Vec3d vec = player.Entity.SidedPos.AheadCopy(2.0).XYZ.Add(player.Entity.LocalEyePos).Sub(ws.startPos);
            if (player.CurrentBlockSelection != null)
            {                
                BlockSelection blockSel = player.CurrentBlockSelection;                

                // Is this a valid IWireAnchor derived Block?
                if (blockSel.Block is IWireAnchor wiredBlock)
                {
                    // Number of Anchors vs. Selection Box Index
                    if (blockSel.SelectionBoxIndex < wiredBlock.NumAnchorsInBlock(ws.wireFunction))
                    {
                        // Can the block (wire) type attach to selection index anchor?
                        if (player.InventoryManager.ActiveHotbarSlot.Empty) return vec.ToVec3f(); // sanity check

                        if (wiredBlock.CanAttachWire(player.Entity.World, player.InventoryManager.ActiveHotbarSlot.Itemstack.Block, player.CurrentBlockSelection))
                        {
                            vec = blockSel.Position.ToVec3d().Sub(ws.startPos).Add(wiredBlock.GetAnchorPosInBlock(blockSel.SelectionBoxIndex));
                        }
                    }
                }
            }

            return vec.ToVec3f();
        }

        public void RemoveAllConnectionsAtPos(BlockPos pos)
        {
            if (api.Side == EnumAppSide.Client) return;

            List<WireConnection> toRemove = data.allConnections.Where(
                (WireConnection con) => con.NodeStart.blockPos == pos || con.NodeEnd.blockPos == pos).ToList();

            if (toRemove.Count > 0)
            {
                foreach (WireConnection connection in toRemove)
                {
                    RemoveConnection(connection);                    
                }
                serverChannel.BroadcastPacket(data);
            }
        }

        /// <summary>
        /// Specfic function run only serverside by a ServerNetwork packet. 
        /// <br>Should ensure data integrity.</br>
        /// </summary>
        /// <param name="wirecondata">WireConnectionData packet</param>
        public void RemoveConnection(WireConnectionData wirecondata)
        {
            if (wirecondata.opcode == WireConnectionOpCode.Remove && wirecondata.connection != null)
            {
                RemoveConnection(wirecondata.connection, null, null, null);
            }
            if (wirecondata.opcode == WireConnectionOpCode.RemoveAll)
            {
                RemoveAllConnectionsAtPos(wirecondata._pos);
            }
        }

        /// <summary>
        /// Removes a wire connection notifying both the start and end nodes to remove the connection. Also drops wire on the ground.
        /// <br>Also triggers the OnWireRemoved event for any subscribing network mods.</br>
        /// </summary>
        /// <param name="con">WireConnection to remove</param>
        /// <param name="byEntity">[Can be Null] Entity removing the wire.</param>
        /// <param name="blockSel">[Can be Null] BlockSelection location</param>
        /// <param name="slot">[Can be Null] Slot used.</param>
        public void RemoveConnection(WireConnection con, EntityAgent byEntity = null, BlockSelection blockSel = null, ItemSlot slot = null)
        {
            // get what the drops will be
            List<ItemStack> drops = new List<ItemStack>();
            if (con.Block == null) con.Block = sapi.World.BlockAccessor.GetBlock(con.BlockId);
            drops.Add(new ItemStack(con.Block, (int)Math.Floor((double)con.NodeEnd.blockPos.DistanceTo(con.NodeStart.blockPos))));

            // Remove the connection
            // the WireConnection pos object tells us what block has the start and end
            int removed = data.allConnections.RemoveWhere(x =>
                (x.NodeStart == con.NodeStart && x.NodeEnd == con.NodeEnd));
            if (removed > 0)
            {
                serverChannel.BroadcastPacket(data);
                if (removed > 1)
                {
                    sapi.Logger.Error($"CatenaryMod: Error removing connection {con}, {removed} matches found!");
                }
            }
            else
            {
                sapi.Logger.Error($"CatenaryMod: Error Removing connection {con}, no match found!");
            }

            // while we remove 2 connections, only one is 'true' as the endpoints get a copy of the connection
            if (drops.Count > 0)
            {
                // should only ever be one stack of items, but lets iterate anyway
                foreach (ItemStack stack in drops)
                {
                    api.World.SpawnItemEntity(stack, con.NodeStart.blockPos.ToVec3d());
                }
            }

            // Triggers the Event and any other mod subscribed will be notified of the connection removal.
            BoolRef consumed = new BoolRef();
            TriggerRemoveConnection(con.NodeStart, con.NodeEnd,
                                    con.Block, consumed);
        }

        /// <summary>
        /// Adds a WireConnection to CatenaryData object
        /// <br>Triggers OnWireConnected event for any subscribed network mods.</br>
        /// </summary>
        /// <param name="placedWire">WireConnection added</param>
        public void AddConnection(WireConnection placedWire)
        {
            if (placedWire.Block == null)
            {
                // the only reason we're in here is when the action is triggered Block isn't saved via ProtoBuf
                // But BlockID IS saved, so we just look it up.
                placedWire.Block = this.api.World.BlockAccessor.GetBlock(placedWire.BlockId);
            }

            bool isAdded = data.allConnections.Add(placedWire);
            if (isAdded)
            {
                serverChannel.BroadcastPacket(data);
            }
            else
            {
                sapi.Logger.Error($"CatenaryMod: Error Adding connection: {placedWire}, already exists!");
            }

            BoolRef consumed = new BoolRef();            
            // event trigger to notify listeners.
            TriggerAddConnection(placedWire.NodeStart, placedWire.NodeEnd,
                                 placedWire.Block, consumed);
        }

        public virtual void TriggerRemoveConnection(WireNode start, WireNode end, Block block, BoolRef consumed)
        {
            if (this.OnWireRemoved == null) return;

            foreach (OnWireRemovedDelegate dele in OnWireRemoved.GetInvocationList())
            {
                try
                {
                    dele(start, end, block, consumed);
                }
                catch(Exception ex)
                {
                    api.Logger.Error($"CatenaryMod: Exception in TriggerRemoveConnection | {ex}");                    
                }
                if (consumed.value) break;
            }                        
        }

        public virtual void TriggerAddConnection(WireNode start, WireNode end, Block block, BoolRef consumed)
        {            
            if (this.OnWireConnected == null) return;

            foreach (OnWireConnectedDelegate dele in OnWireConnected.GetInvocationList())
            {
                try
                {
                    dele(start, end, block, consumed);
                }
                catch (Exception ex)
                {
                    api.Logger.Error($"CatenaryMod: Exception in TriggerAddConnection | {ex}");
                }
                if (consumed.value) break;
            }
        }
    }
}

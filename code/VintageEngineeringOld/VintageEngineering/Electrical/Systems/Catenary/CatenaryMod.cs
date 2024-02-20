using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// Called when a wire is connected between two points.
    /// <br>If your mod needs to manage the network created by this event, subscribe to it.</br>
    /// </summary>
    /// <param name="start">Start Node</param>
    /// <param name="end">End Node</param>
    /// <param name="startIndex">Anchor Start Index</param>
    /// <param name="endIndex">Anchor End Index</param>    
    /// <param name="slot">ItemSlot used</param>
    /// <param name="consumed">Set to true to stop further calls.</param>
    /// <param name="networkid">NetworkID assigned to this connection.</param>
    public delegate void OnWireConnectedDelegate(BlockPos start, BlockPos end, int startIndex, int endIndex, ItemSlot slot, BoolRef consumed, out long networkid);
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
    public delegate void OnWireRemovedDelegate(BlockPos start, BlockPos end, int startIndex, int endIndex, Block block, BoolRef consumed);

    /// <summary>
    /// System that tracks, saves/loads, and renders wires of any purpose in the game.
    /// <br>It's up to other mods to implement ModSystems to create and simulate the wire networks stored/created in this system.</br>    
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
        /// Event fired when a wire connection is created.
        /// </summary>
        public event OnWireConnectedDelegate OnWireConnected;
        /// <summary>
        /// Event fired when a wire connection is removed.
        /// </summary>
        public event OnWireRemovedDelegate OnWireRemoved;
              
        ICoreAPI api;
        ICoreServerAPI sapi;
        ICoreClientAPI capi;

        // Simple system to limit pending wire rendering to protect FPS integrity.
        private float renderbouncer = 0f;

        #region ModSystem

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public double RenderOrder { get { return 0.5; } } // IRenderer
        public int RenderRange { get { return 100; } }   // IRenderer

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            if (api.Side is EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }
            else
            {
                sapi = api as ICoreServerAPI;
            }
            api.RegisterBlockClass("BlockWire", typeof(BlockWire));
            api.RegisterBlockEntityBehaviorClass("WiredBlock", typeof(BEBehaviorWire));
            api.RegisterCollectibleBehaviorClass("WireTool", typeof(BehaviorWireTool));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "wireplacer");
        }
        #endregion

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
                ws.currentMesh = null;
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
            BEBehaviorWire beh = api.World.BlockAccessor.GetBlockEntity(blocksel.Position)?.GetBehavior<BEBehaviorWire>();

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

                if (beh == null || wiredBlock == null)
                {
                    // bounce, as we're not looking at a Wire-connectable block
                    return;
                }
                else
                {
                    // we are looking at a wire connectable block

                    // CanAttachWire is a vital call, all logic falls to network implimentations
                    // Checks for valid connections based on wire type and WireAnchor types
                    if (wiredBlock.CanAttachWire(api.World, block, blocksel.SelectionBoxIndex))
                    {
                        // wire connection is valid, lets connect it
                        ws.startPos = blocksel.Position.Copy();
                        ws.startOffset = wiredBlock.GetAnchorPosInBlock(blocksel.SelectionBoxIndex);
                        ws.startIndex = blocksel.SelectionBoxIndex;
                        ws.startNetID = -1;
                        ws.thickness = block.Attributes["thickness"].AsFloat(0.125f);
                        ws.wireFunction = block.Attributes["wirefunction"].AsObject<EnumWireFunction>();
                        ws.block = block;
                        ws.nowBuilding = true; // setting this triggers the renderer 
                        ws.endOffset = null;
                    }
                }
                return;
            }
            // We are currently connected to a start position
            if (beh == null || wiredBlock == null)
            {
                // bounce, as we're not looking at a Wire-connectable block
                return;
            }
            // Check validity of start position one last time before connecting to next spot.
            BEBehaviorWire bestart = api.World.BlockAccessor.GetBlockEntity(ws.startPos)?.GetBehavior<BEBehaviorWire>();
            if (bestart == null)
            {
                CancelPlace(block as BlockWire, byEntity);
                return;
            }

            // start and end block pos are the same, bounce
            if (ws.startPos == blocksel.Position) return;

            if (!wiredBlock.CanAttachWire(api.World, block, blocksel.SelectionBoxIndex))
            {
                // wire connection is NOT valid...
                return;
            }
            int length = (int)Math.Ceiling((double)ws.endOffset.DistanceTo(ws.startOffset));
            if (slot.StackSize < length)
            {
                if (capi != null) capi.TriggerIngameError(this, "notenoughitems", $"You need {length} wires to connect these points.");
                return;
            }
            else
            {
                slot.TakeOut(length);
                slot.MarkDirty();
            }

            // If we're here, we can connect wire            
            PlacedWire newconnection = new PlacedWire(
                ws.startOffset.Clone(), ws.endOffset.Clone(), ws.wireFunction, block.Id, block.Attributes["thickness"].AsFloat(),
                ws.startPos.Copy(), blocksel.Position.Copy(), ws.startIndex, blocksel.SelectionBoxIndex, 0, ws.currentMesh.Clone());

            long netid = -1;
            BoolRef consumed = new BoolRef();
            TriggerAddConnection(newconnection.MasterStart, newconnection.MasterEnd,
                                newconnection.StartSelectionIndex, newconnection.EndSelectionIndex,
                                slot, consumed, out netid);

            newconnection.NetworkID = netid;
            AddConnection(newconnection);
        }

        public void OnRenderFrame(float deltatime, EnumRenderStage stage)
        {
            /// TODO Add a DEBOUNCE to save FPS
            renderbouncer += deltatime;
            if (renderbouncer < 0.5) return;
            renderbouncer = 0f;

            WirePlacerWorkSpace ws = this.getWorkSpace(capi.World.Player.PlayerUID);
            
            // if we are not currently running a wire, we don't need to render anything
            if (!ws.nowBuilding) return;

            Vec3f nowEndOffset = this.GetEndOffset(capi.World.Player, ws);

            // Bounce if distance is very small
            if ((double)ws.startOffset.DistanceTo(nowEndOffset) < 0.1) return;

            // Bounce if distance is >= maxlength of wire
            int maxlength = ws.block.Attributes["maxlength"].AsInt(1);
            if ((double)ws.startOffset.DistanceTo(nowEndOffset) >= maxlength) return;

            if (ws.endOffset != nowEndOffset)
            {
                ws.endOffset = nowEndOffset;
                reloadMeshRef();
            }
            // Something bad happened with uploading mesh to GPU
            if (ws.currentMeshRef == null) return;

            IShaderProgram currentActiveShader = this.capi.Render.CurrentActiveShader;
            if (currentActiveShader != null) currentActiveShader.Stop();

            IStandardShaderProgram standardShaderProgram = this.capi.Render.PreparedStandardShader(
                ws.startPos.X, ws.startPos.Y, ws.startPos.Z, null);

            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            // no idea what the code below actually does
            standardShaderProgram.Use();
            standardShaderProgram.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId; // ??                        
            standardShaderProgram.ModelMatrix = this.ModelMat.Identity().Translate(
                (double)ws.startPos.X - camPos.X,
                (double)ws.startPos.Y - camPos.Y,
                (double)ws.startPos.Z - camPos.Z).Values;
            standardShaderProgram.ViewMatrix = capi.Render.CameraMatrixOriginf;
            standardShaderProgram.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;
            capi.Render.RenderMesh(ws.currentMeshRef);
            standardShaderProgram.Stop();
            if (currentActiveShader == null) return;
            currentActiveShader.Use();
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
            //MeshData mesh = WireMesh.MakeWireMesh(startOffset, endOffset, thickness);
            MeshData curmesh = ws.currentMesh;
            MeshData newmesh = ModSystemSupportBeamPlacer.generateMesh(startOffset, endOffset, null, curmesh, 0.25f);

            ws.currentMesh.Clear();
            ws.currentMesh = newmesh;
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
                    if (blockSel.SelectionBoxIndex < wiredBlock.NumAnchorsInBlock(EnumWireFunction.Any))
                    {
                        // Can the block (wire) type attach to selection index anchor?
                        if (wiredBlock.CanAttachWire(player.Entity.World, player.InventoryManager.ActiveHotbarSlot.Itemstack.Block, player.CurrentBlockSelection.SelectionBoxIndex))
                        {
                            return wiredBlock.GetAnchorPosInBlock(blockSel.SelectionBoxIndex);
                        }
                    }
                }
            }
            // if the block check above is invalid, return the vec in front of the player.
            return vec.ToVec3f();
        }

        internal void RemoveConnection(PlacedWire pos, EntityAgent byEntity, BlockSelection blockSel, ItemSlot slot)
        {
            // get what the drops will be
            List<ItemStack> drops = new List<ItemStack>();
            drops.Add(new ItemStack(pos.Block, (int)Math.Ceiling((double)pos.End.DistanceTo(pos.Start))));

            // Remove the connection
            // the PlacedWire pos object tells us what block has the start and end
            BEBehaviorWire behstart = api.World.BlockAccessor.GetBlockEntity(pos.MasterStart).GetBehavior<BEBehaviorWire>();
            BEBehaviorWire behend = api.World.BlockAccessor.GetBlockEntity(pos.MasterEnd).GetBehavior<BEBehaviorWire>();
            if (behstart == null || behend == null) return; // the positions are not valid?

            // remove from start and end nodes
            behstart.RemoveWire(pos);
            behend.RemoveWire(pos, true);

            // while we remove 2 connections, only one is 'true' as the endpoints get a copy of the connection
            if (drops.Count > 0)
            {
                // should only ever be one stack of items, but lets iterate anyway
                foreach (ItemStack stack in drops)
                {
                    api.World.SpawnItemEntity(stack, pos.MasterStart.ToVec3d());
                }
            }
            //behstart.Blockentity.MarkDirty(true);
            //behend.Blockentity.MarkDirty(true);

            // Triggers the Event and any other mod subscribed will be notified of the connection removal.
            BoolRef consumed = new BoolRef();
            TriggerRemoveConnection(pos.MasterStart, pos.MasterEnd, 
                                    pos.StartSelectionIndex, pos.EndSelectionIndex, pos.Block, consumed);
        }

        internal void AddConnection(PlacedWire placedWire)
        {
            BEBehaviorWire behstart = api.World.BlockAccessor.GetBlockEntity(placedWire.MasterStart).GetBehavior<BEBehaviorWire>();
            BEBehaviorWire behend = api.World.BlockAccessor.GetBlockEntity(placedWire.MasterEnd).GetBehavior<BEBehaviorWire>();

            behstart.AddWire(placedWire);
            behend.AddWire(placedWire, true);
        }

        public virtual void TriggerRemoveConnection(BlockPos start, BlockPos end, int startIndex, int endIndex, Block block, BoolRef consumed)
        {
            if (this.OnWireRemoved == null) return;

            foreach (OnWireRemovedDelegate dele in OnWireRemoved.GetInvocationList())
            {
                try
                {
                    dele(start, end, startIndex, endIndex, block, consumed);
                }
                catch(Exception ex)
                {
                    api.Logger.Error($"ModSystem Exception: Catenary:TriggerRemoveConnection | {ex}");                    
                }
                if (consumed.value) break;
            }                        
        }

        public virtual void TriggerAddConnection(BlockPos start, BlockPos end, int startIndex, int endIndex, ItemSlot slot, BoolRef consumed, out long networkid)
        {
            networkid = -1;
            if (this.OnWireConnected == null) return;

            foreach (OnWireConnectedDelegate dele in OnWireConnected.GetInvocationList())
            {
                try
                {
                    dele(start, end, startIndex, endIndex, slot, consumed, out networkid);
                }
                catch (Exception ex)
                {
                    api.Logger.Error($"ModSystem Exception: Catenary:TriggerAddConnection | {ex}");
                }
                if (consumed.value) break;
            }
        }
    }
}

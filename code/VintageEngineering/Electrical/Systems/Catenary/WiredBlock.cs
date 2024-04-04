using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// Base object for a generic wire connectable block
    /// <br>Impliments IWireAnchor</br>
    /// </summary>
    public abstract class WiredBlock : Block, IWireAnchor
    {
        /// <summary>
        /// The Wire Anchors this block has.
        /// </summary>
        protected WireNode[] wireAnchors;

        public WireNode[] WireAnchors { get { return wireAnchors; } }        

        protected CatenaryMod cm;

        public WiredBlock(): base()
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            JsonObject[] wirenodes = Attributes?["wireNodes"]?.AsArray();
            cm = api.ModLoader.GetModSystem<CatenaryMod>(true);
            if (wirenodes != null)
            {
                try
                {
                    wireAnchors = new WireNode[wirenodes.Length];                    
                    
                    for (int i = 0; i < wirenodes.Length; i++)
                    {
                        wireAnchors[i] = new WireNode(wirenodes[i]);
                    }
                    return;
                }
                catch (Exception e)
                {
                    api.World.Logger.Error($"Failed loading WireAnchors for item/block {Code}. Will Ignore. Exception: {e}");
                }
            }
            wireAnchors = new WireNode[0];
        }        

        /// <summary>
        /// Overloaded for the addition of WireNode selection areas. 
        /// <br>If holding a wrench only the WireConnection selection boxes are shown.</br>
        /// </summary>
        /// <param name="blockAccessor">BlockAccessor</param>
        /// <param name="pos">Position</param>
        /// <returns>Cuboidf Array</returns>
        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            List<Cuboidf> boxes = new List<Cuboidf>();
            if (this.api.Side == EnumAppSide.Client)
            {
                if ((api as ICoreClientAPI).World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Item?.Tool == EnumTool.Wrench)
                {
                    // player is holding a wrench
                    if (wireAnchors.Length > 0)
                    {
                        return GetWireSelectionBoxes(blockAccessor, pos);
                    }
                }
                if ((api as ICoreClientAPI).World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Block?.FirstCodePart() == "catenary")
                {
                    // player is holding wire
                    if (wireAnchors.Length > 0)
                    {
                        return GetWireSelectionBoxes(blockAccessor, pos);
                    }
                }
            }
            boxes.AddRange(base.GetSelectionBoxes(blockAccessor, pos));
            return boxes.ToArray();
        }

        /// <summary>
        /// Get ONLY WireNode selection areas. Ensures wire anchor index value isn't corrupted by base selection box.
        /// </summary>
        /// <param name="blockAccessor"></param>
        /// <param name="pos"></param>
        /// <returns>Wire Anchor Cuboidf array</returns>
        public virtual Cuboidf[] GetWireSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            List<Cuboidf> boxes = new List<Cuboidf>(wireAnchors.Length);
            for (int i = 0; i < wireAnchors.Length; i++)
            {
                boxes.Add(wireAnchors[i].RotatedCopy()); // this ensures nodes don't wiggle around
            }
            return boxes.ToArray();
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // needs to announce breaking wire connections
            CatenaryMod cm = api.ModLoader.GetModSystem<CatenaryMod>(true);

            if (cm != null && this.api.Side == EnumAppSide.Client)
            {
                string playerid = byPlayer != null ? byPlayer.PlayerUID : string.Empty;
                WireConnectionData wcd = new WireConnectionData()
                {
                    _pos = pos,
                    playerUID = playerid,
                    opcode = WireConnectionOpCode.RemoveAll
                };
                // tell the server to process connections at this position, will push data back to client.
                cm.clientChannel.SendPacket(wcd);
                //cm.RemoveAllConnectionsAtPos(pos);
            }
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        /// <summary>
        /// Returns the CENTER POINT of the selection box by a given index.
        /// <br>If selectionIndex is larger than the number of wire anchors, returns Vec3f.Zero</br>
        /// </summary>
        /// <param name="selectionIndex">Selection Box Index</param>
        /// <returns>Vec3f location.</returns>
        public virtual Vec3f GetAnchorPosInBlock(int selectionIndex)
        {
            if (selectionIndex >= wireAnchors.Length)
            {
                return Vec3f.Zero;
            }
            Cuboidf anchor = wireAnchors[selectionIndex].RotatedCopy();
            Vec3f centerpoint = new Vec3f(anchor.MidX, anchor.MidY, anchor.MidZ);
            return centerpoint;
        }

        public virtual Vec3f GetAnchorPosInBlock(WireNode node)
        {
            return GetAnchorPosInBlock(node.index);
        }

        public virtual WireNode GetWireNodeInBlock(int selectionIndex)
        {
            if (selectionIndex >= wireAnchors.Length) return null;            
            return wireAnchors[selectionIndex];
        }

        /// <summary>
        /// [Overridable] For getting number of connections for a given selectionindex anchor.        
        /// </summary>
        /// <param name="selectionIndex">SelectionBox highlighted</param>
        /// <returns>Max Connections</returns>        
        public virtual int GetMaxConnections(int selectionIndex)
        {
            if (selectionIndex >= wireAnchors.Length) return 0;
            return wireAnchors[selectionIndex].maxconnections;
        }

        /// <summary>
        /// Override this to report whether a wire can be attached or not.
        /// <br>Do NOT call base as it throws an exception.</br>
        /// </summary>
        /// <param name="world">WorldAccessor</param>
        /// <param name="wireitem">WireBlock</param>
        /// <param name="selectionIndex">BlockSelection information to check.</param>
        /// <returns>True if wire connection is valid.</returns>
        /// <exception cref="NotImplementedException">Exception thrown if not overridden.</exception>
        public virtual bool CanAttachWire(IWorldAccessor world, Block wireitem, BlockSelection selection)
        {
            // needs block item
            if (wireitem == null) return false;

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns what the wire function is according to the selectionIndex
        /// <br>Value of EnumWireFunction.None is returned if no valid anchor is defined.</br>
        /// </summary>
        /// <param name="selectionIndex">Which Selection Box to return</param>
        /// <returns>Wire function of this anchor.</returns>
        public virtual EnumWireFunction GetWireFunction(int selectionIndex = 0)
        {
            if (selectionIndex > wireAnchors.Length)
            {
                return EnumWireFunction.None;
            }
            return wireAnchors[selectionIndex].wirefunction;
        }

        /// <summary>
        /// Returns the number of valid anchors in this block according to their function.
        /// </summary>
        /// <param name="wfunction">EnumWireFunction to filter by.</param>
        /// <returns>Int Num Anchors</returns>
        public virtual int NumAnchorsInBlock(EnumWireFunction wfunction)
        {
            int num = 0;
            foreach (WireNode anchor in wireAnchors)
            {
                if (anchor.wirefunction == wfunction) num++;
            }
            return num;
        }

        /// <summary>
        /// Returns WireConnections array corrisponding to the given WireNode Index.
        /// </summary>
        /// <param name="selectionIndex">WireAnchor Index</param>
        /// <param name="byEntity">Player Entity</param>
        /// <param name="blockSelection">BlockSelection object</param>
        /// <returns>WireConnection array, null if no connections exist</returns>        
        public virtual WireConnection[] GetWireConnectionsInBlock(int selectionIndex, EntityAgent byEntity, BlockSelection blockSelection)
        {
            // sanity check on the selection index
            if (selectionIndex >= wireAnchors.Length) { return Array.Empty<WireConnection>(); }
            // grab any connections at this WireNode
            List<WireConnection> conshere = cm.GetWireConnectionsAt(blockSelection);

            // no connections, return null
            if (conshere.Count == 0) return null;
            // finally, return the connections we have
            return conshere.ToArray();
        }

        public bool OnWireInteractionStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {            
            // ALL the sanity checks
            if (world == null) return false; // the sky is falling
            if (byPlayer == null || byPlayer.InventoryManager.ActiveHotbarSlot.Empty) return false; // we're not actually holding anything
            if (blockSel == null) return false; // we're not looking at a block
            if (blockSel.Block is not IWireAnchor) return false; // we're not looking at a wire-connectable block

            Block checkit = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Block;
            if (checkit == null || checkit is not BlockWire) return false; // we're not even holding wire?!

            // after all that, finally let the CatenaryMod know we want to connect a wire.
            cm.OnInteract(checkit, byPlayer.InventoryManager.ActiveHotbarSlot, byPlayer.Entity, blockSel);
            return true;
        }
    }
}

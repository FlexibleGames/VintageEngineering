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
        /// Wire Anchors this block has.
        /// </summary>
        protected WireAnchor[] wireAnchors;

        public WiredBlock(): base()
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);            
            JsonObject[] wirenodes = Attributes?["wireNodes"]?.AsArray();

            if (wirenodes != null)
            {
                try
                {
                    wireAnchors = new WireAnchor[wirenodes.Length];

                    for (int i = 0; i < wirenodes.Length; i++)
                    {
                        wireAnchors[i] = new WireAnchor(wirenodes[i]); 
                    }
                    return;
                }
                catch (Exception e)
                {
                    api.World.Logger.Error($"Failed loading WireAnchors for item/block {Code}. Will Ignore. Exception: {e}");
                }
            }
            wireAnchors = new WireAnchor[0];
        }

        /// <summary>
        /// Overloaded for the addition of WireAnchor selection areas. 
        /// <br>If holding a wrench only the PlacedWire selection boxes are shown.</br>
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
                    BlockEntity blockEntity = api.World.BlockAccessor.GetBlockEntity(pos);
                    BEBehaviorWire beh = blockEntity?.GetBehavior<BEBehaviorWire>();
                    if (beh != null)
                    {
                        // are there connections here?
                        if (beh.WiresStart.Length > 0 || beh.WiresEnd.Length > 0)
                        {
                            return beh.GetWireCollisionBoxes();
                        }
                    }
                }
                if ((api as ICoreClientAPI).World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Block?.FirstCodePart() == "catenary")
                {
                    foreach (WireAnchor wa in wireAnchors)
                    {
                        boxes.Add(wa.RotatedCopy());
                    }
                    return boxes.ToArray();
                }
            }
            boxes.AddRange(base.GetSelectionBoxes(blockAccessor, pos));
            return boxes.ToArray();
        }

        /// <summary>
        /// Get ONLY WireAnchor selection areas. Ensures wire anchor index value isn't corrupted by base selection box.
        /// </summary>
        /// <param name="blockAccessor"></param>
        /// <param name="pos"></param>
        /// <returns>Wire Anchor Cuboidf array</returns>
        public virtual Cuboidf[] GetWireSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            List<Cuboidf> boxes = new List<Cuboidf>();
            foreach (WireAnchor wa in wireAnchors)
            {
                boxes.Add(wa.RotatedCopy());
            }
            return boxes.ToArray();
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);
            /// TODO
            /// ALL the things
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

        public virtual WireAnchor GetWireAnchorInBlock(int selectionIndex)
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
            if (selectionIndex > wireAnchors.Length) return 0;
            return wireAnchors[selectionIndex]._maxconnections;
        }

        /// <summary>
        /// Override to report whether a wire can be attached or not.
        /// <br>Do NOT call base as it throws an exception.</br>
        /// </summary>
        /// <param name="world">WorldAccessor</param>
        /// <param name="wireitem">WireBlock</param>
        /// <param name="selectionIndex">Selectionbox index to check.</param>
        /// <returns>True if wire connection is valid.</returns>
        /// <exception cref="NotImplementedException">Exception thrown if not overridden.</exception>
        public virtual bool CanAttachWire(IWorldAccessor world, Block wireitem, int selectionIndex)
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
            return wireAnchors[selectionIndex]._wirefunction;
        }

        /// <summary>
        /// Returns the number of valid anchors in this block.
        /// </summary>
        /// <param name="wfunction">EnumWireFunction to filter by.</param>
        /// <returns>Int Num Anchors</returns>
        public virtual int NumAnchorsInBlock(EnumWireFunction wfunction)
        {
            int num = 0;
            foreach (WireAnchor anchor in wireAnchors)
            {
                if ((anchor._wirefunction & wfunction) == wfunction) num++;
            }
            return num;
        }

        /// <summary>
        /// Returns PlacedWire coorisponding to the given selectionIndex.
        /// </summary>
        /// <param name="selectionIndex"></param>
        /// <returns>WireNode</returns>        
        public virtual PlacedWire GetWireConnectionInBlock(int selectionIndex, EntityAgent byEntity, BlockSelection blockSelection)
        {            
            BlockEntity blockEntity = api.World.BlockAccessor.GetBlockEntity(blockSelection.Position);
            BEBehaviorWire beh = blockEntity?.GetBehavior<BEBehaviorWire>();
            if (beh == null) return null;

            if (beh.WiresStart == null || selectionIndex >= beh.WiresStart?.Length)
            {
                // return WiresEnd index
                selectionIndex -= beh.WiresStart == null ? 0 : beh.WiresStart.Length;
                return beh.WiresEnd[selectionIndex];
            }
            return beh.WiresStart[selectionIndex];
        }
    }
}

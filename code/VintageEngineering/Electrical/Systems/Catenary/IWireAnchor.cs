using System;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// Allows wire system to be used for many reasons.    
    /// </summary>
    public enum EnumWireFunction
    {
        /// <summary>
        /// Allows for when selection box does not have a wire anchor.
        /// <br>For example when the machine itself is selected and not the wire anchor.</br>
        /// </summary>
        None,
        /// <summary>
        /// For Signal based wires.
        /// </summary>
        Signal,
        /// <summary>
        /// For Power based wires.
        /// </summary>
        Power,
        /// <summary>
        /// For Communication based wires.
        /// </summary>
        Communication,
        /// <summary>
        /// Wires for all other uses.
        /// </summary>
        Other,
        /// <summary>
        /// Be sure to allow for this if adding your own wires.
        /// </summary>
        Any
    }

    /// <summary>
    /// Interface for Blocks which have Wire Anchor connection points. (aka WireNode)
    /// <br>Used by WiredBlock or any mod adding a wire connectable block.</br>
    /// </summary>
    public interface IWireAnchor
    {
        /// <summary>
        /// Returns number of anchors (WireNodes) this block supports by function.
        /// </summary>
        /// <param name="wfunction">Wire Function to filter by.</param>
        /// <returns>Number of Wire Anchors</returns>
        int NumAnchorsInBlock(EnumWireFunction wfunction);

        /// <summary>
        /// Returns Coordinates to wire connection point based on the selection box being interacted with.
        /// <br>Typically the midpoint of the selection box defined by WireNode in attributes.</br>
        /// </summary>
        /// <param name="selectionIndex">Selection Box index player is interacting with</param>
        /// <returns>Wire Connection point</returns>
        Vec3f GetAnchorPosInBlock(int selectionIndex);

        /// <summary>
        /// Returns the WireNode associated with the selection box index.
        /// <br>Returns Null if selectionIndex is out of bounds.</br>
        /// </summary>
        /// <param name="selectionIndex">Selection Index</param>
        /// <returns>WireAnchor</returns>
        WireNode GetWireNodeInBlock(int selectionIndex);

        /// <summary>
        /// Returns Coordinates to wire connection point based on WireNode object 
        /// <br>WireNode object has index value</br>
        /// </summary>
        /// <param name="node">WireNode</param>
        /// <returns>Vec3F coordinates</returns>
        Vec3f GetAnchorPosInBlock(WireNode node);

        /// <summary>
        /// Returns a WireConnection array coorisponding to the given selection index.
        /// </summary>
        /// <param name="selectionIndex">Selection Box Index</param>
        /// <returns>WireConnection</returns>
        WireConnection[] GetWireConnectionsInBlock(int selectionIndex, EntityAgent byEntity, BlockSelection blockSelection);

         /// <summary>
        /// A Block-level function to return the maximum number of connections this node can support.
        /// <br>Machines/Generators typically have 1 or 2</br>
        /// <br>Relays can have many</br>
        /// </summary>
        /// <param name="selectionIndex">Selection Box index player is interacting with</param>
        /// <returns>Maximum number of allowed connections.</returns>
        int GetMaxConnections(int selectionIndex);

        /// <summary>
        /// Is wire Item allowed to connect to selectionIndex node?
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="wireitem">Item to test.</param>
        /// <param name="selectionIndex">Index of connection attempt.</param>
        /// <returns>True if connection allowed.</returns>
        bool CanAttachWire(IWorldAccessor world, Block wireitem, BlockSelection selection);
        
        /// <summary>
        /// Returns the Wire Function of the given selectionIndex Anchor point.
        /// <br>Allows for a block to have power, signal, and communication connection points.</br>
        /// <br>Returns EnumWireFunction.None for invalid wire connections.</br>
        /// </summary>
        /// <param name="selectionIndex">SelectionIndex</param>
        /// <returns>EnumWireFunction value</returns>
        EnumWireFunction GetWireFunction(int selectionIndex = 0);

        /// <summary>
        /// Passed from the Block OnInteraction event if the interaction included a held wire item.
        /// <br>Informs the Catenary Mod a wire connection interaction is being made.</br>
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="block">Block (MUST be BlockWire!) Held</param>
        /// <param name="byPlayer">Player</param>
        /// <param name="blockSel">Block Selection</param>
        /// <returns>True if CatenaryMod was notified.</returns>
        bool OnWireInteractionStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel);
    }
}

using System;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// Allows wire system to be used for many reasons.
    /// <br>Uses Flags (for bitwise operations)</br>
    /// </summary>
    [Flags]
    public enum EnumWireFunction
    {
        /// <summary>
        /// Allows for when selection box does not have a wire anchor.
        /// <br>For example when the machine itself is selected and not the wire anchor.</br>
        /// </summary>
        None = 0,
        /// <summary>
        /// For Signal based wires.
        /// </summary>
        Signal = 1,
        /// <summary>
        /// For Power based wires.
        /// </summary>
        Power = 2,
        /// <summary>
        /// For Communication based wires.
        /// </summary>
        Communication = 4,
        /// <summary>
        /// Wires for all other uses.
        /// </summary>
        Other = 8,
        /// <summary>
        /// Be sure to allow for this if adding your own wires.
        /// </summary>
        Any = Signal | Power | Communication | Other,
    }

    /// <summary>
    /// Interface for Blocks which have Wire Anchor connection points.
    /// <br>Used by Wiring system to create Wire Networks</br>
    /// </summary>
    public interface IWireAnchor
    {
        /// <summary>
        /// Returns number of anchors this block supports.
        /// </summary>
        /// <param name="wfunction">Wire Function to filter by.</param>
        /// <returns>Number of Wire Anchors</returns>
        int NumAnchorsInBlock(EnumWireFunction wfunction);

        /// <summary>
        /// Returns Coordinates to wire connection point based on the selection box being interacted with.
        /// </summary>
        /// <param name="selectionIndex">Selection Box index player is interacting with</param>
        /// <returns>Wire Connection point</returns>
        Vec3f GetAnchorPosInBlock(int selectionIndex);

        /// <summary>
        /// Returns the WireAnchor associated with the selection box index.
        /// <br>Returns Null if selectionIndex is out of bounds.</br>
        /// </summary>
        /// <param name="selectionIndex">Selection Index</param>
        /// <returns>WireAnchor</returns>
        WireAnchor GetWireAnchorInBlock(int selectionIndex);

        /// <summary>
        /// Returns Coordinates to wire connection point based on WireNode object 
        /// <br>WireNode object has index value</br>
        /// </summary>
        /// <param name="node">WireNode</param>
        /// <returns>Vec3F coordinates</returns>
        Vec3f GetAnchorPosInBlock(WireNode node);

        /// <summary>
        /// Returns a PlacedWire object coorisponding to the given selection index.
        /// </summary>
        /// <param name="selectionIndex"></param>
        /// <returns>WireConnection</returns>
        PlacedWire GetWireConnectionInBlock(int selectionIndex, EntityAgent byEntity, BlockSelection blockSelection);

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
        bool CanAttachWire(IWorldAccessor world, Block wireitem, int selectionIndex);
        
        /// <summary>
        /// Returns the Wire Function of the given selectionIndex Anchor point.
        /// <br>Allows for a block to have power, signal, and communication connection points.</br>
        /// <br>Returns EnumWireFunction.None for invalid wire connections.</br>
        /// </summary>
        /// <param name="selectionIndex">SelectionIndex</param>
        /// <returns>EnumWireFunction value</returns>
        EnumWireFunction GetWireFunction(int selectionIndex = 0);
        
    }

    /// <summary>
    /// A generic object to represent a JSON defined wire anchor. Rotatable with RotatedCopy()
    /// <br>JSON: attributes/wireNodes[]</br>
    /// <br>Is not related to IWireAnchor</br>
    /// </summary>
    public class WireAnchor : RotatableCube
    {
        public int _index;
        public EnumWireFunction _wirefunction;
        public int _maxconnections;
        
        public WireAnchor(int index, EnumWireFunction wirefunction, int maxconnections, float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) : base(MinX, MinY, MinZ, MaxX, MaxY, MaxZ)
        {
            _index = index;
            _wirefunction = wirefunction;
            _maxconnections = maxconnections;
        }
        public WireAnchor(JsonObject anchor)
        {
            _index = anchor["index"].AsInt(0);
            _wirefunction = anchor["wirefunction"].AsObject<EnumWireFunction>(EnumWireFunction.None);
            _maxconnections = anchor["maxconnections"].AsInt(1);            
            base.Set(anchor["x1"].AsFloat(0.25f),
                    anchor["y1"].AsFloat(0.25f),
                    anchor["z1"].AsFloat(0.25f),
                    anchor["x2"].AsFloat(0.75f),
                    anchor["y2"].AsFloat(0.75f),
                    anchor["z2"].AsFloat(0.75f));
        }
    }
}

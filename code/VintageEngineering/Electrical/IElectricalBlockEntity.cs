using System;
using System.Collections.Generic;
using VintageEngineering.Electrical.Systems;
using VintageEngineering.Electrical.Systems.Catenary;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Power Interface for Electrical Block Entities
    /// </summary>
    public interface IElectricalBlockEntity
    {
        /// <summary>
        /// Connections this Block Entity has.
        /// </summary>
        /// <param name="wirenodeindex">Index of the WireNode of THIS block to pull from.</param>
        /// <returns>WireNode List of connections</returns>
        List<WireNode> GetConnections(int wirenodeindex);

        /// <summary>
        /// Add an ElectricConnection to this block entity at the wire node index.
        /// </summary>
        /// <param name="wirenodeindex">Index of the WireNode</param>
        /// <param name="newconnection">Connection to Add</param>
        void AddConnection(int wirenodeindex, WireNode newconnection);

        /// <summary>
        /// Removes an ElectricConnection from this block entity at the wire node index.
        /// </summary>
        /// <param name="wirenodeindex">Index of the WireNode</param>
        /// <param name="oldconnection">Connection to remove.</param>
        void RemoveConnection(int wirenodeindex, WireNode oldconnection);

        /// <summary>
        /// Returns the number of connections this node carries per given WireNode index.        
        /// </summary>
        /// <param name="wirenodeindex">Index of the WireNode</param>
        /// <returns>Total connections this node has.</returns>
        int NumConnections(int wirenodeindex);
        
        /// <summary>
        /// Max power this entity stores. Set in JSON.
        /// </summary>
        ulong MaxPower { get; }

        /// <summary>
        /// Max Power Per Second this Entity can handle, incoming and outgoing and generating. Set in JSON.
        /// </summary>
        ulong MaxPPS { get; }

        /// <summary>
        /// Current Power in Entity
        /// </summary>
        ulong CurrentPower { get; }

        /// <summary>
        /// What type of Electrical Entity is this? 
        /// <br>This is set in the JSON Attributes "entitytype" variable.</br>
        /// </summary>
        EnumElectricalEntityType ElectricalEntityType { get; }

        /// <summary>
        /// Can Receive Power? Override to set.
        /// </summary>
        bool CanReceivePower { get; }

        /// <summary>
        /// Can power be extracted? Override to set.
        /// </summary>
        bool CanExtractPower { get; }

        /// <summary>
        /// Is Power Full? i.e. Can simply be MaxPower == CurrentPower
        /// </summary>
        bool IsPowerFull { get; }

        /// <summary>
        /// Is this block sleeping? Saved in base ElectricBE.<br/>
        /// If a machine is ON but NOT Crafting it is sleeping.<br/>
        /// A sleeping machine ticks at a slower rate to preserve update time.
        /// </summary>
        bool IsSleeping { get; }

        /// <summary>
        /// Is this Machine Enabled? (On/Off) Saved in base ElectricBE.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// What is the Priority of this Entity? Saved in base ElectricBE.
        /// <br>1 = highest priority.</br>
        /// <br>If every entity is the same priority, then the priority system is negated.</br>
        /// <br>Higher Priority generators are first to empty, machines are first to fill, etc.</br>
        /// <br>Can be hard-coded or set via GUI.</br>
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Takes powerOffered and removes any power needed and returns power left over.<br/>
        /// It's up to the implementing block entity to track it's internal power and PPS limits.
        /// </summary>
        /// <param name="powerOffered">Power offered to this Entity</param>
        /// <param name="dt">Delta Time; Time elapsed since last update.</param>
        /// <param name="simulate">[Optional] Whether to simulate function and not actually give power.</param>
        /// <returns>Power left over (0 if all power was consumed)</returns>
        ulong ReceivePower(ulong powerOffered, float dt, bool simulate = false);

        /// <summary>
        /// Reduces powerWanted by power held in this entity.<br/>
        /// It's up to the implementing block entity to track it's internal power and PPS limits.
        /// </summary>
        /// <param name="powerWanted">How much total power is needed</param>
        /// <param name="dt">Delta Time; Time elapsed since last update.</param>
        /// <param name="simulate">[Optional] Whether to just simulate function and not actually take power.</param>
        /// <returns>Unfulfilled amount of powerWanted (0 if all wanted power was satisfied)</returns>
        ulong ExtractPower(ulong powerWanted, float dt, bool simulate = false);

        /// <summary>
        /// Completely fill (or drain) power buffer.<br/>
        /// A fast way for Electrical Networks to process power for this entity.<br/>        
        /// </summary>
        /// <param name="drain">[Optional] Drain power to 0 if true.</param>
        void CheatPower(bool drain = false);
        
    }
}

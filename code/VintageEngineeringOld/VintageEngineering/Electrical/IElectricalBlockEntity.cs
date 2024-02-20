using System;
using System.Collections.Generic;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Power Interface for Electrical Block Entities
    /// </summary>
    public interface IElectricalBlockEntity
    {
        /// <summary>
        /// Max power this entity stores.
        /// </summary>
        ulong MaxPower { get; }

        /// <summary>
        /// Current Power in Entity
        /// </summary>
        ulong CurrentPower { get; }

        /// <summary>
        /// What type of Electrical Entity is this?
        /// </summary>
        EnumElectricalEntityType ElectricalEntityType { get; }

        /// <summary>
        /// Can Receive Power?
        /// </summary>
        bool CanReceivePower { get; }

        /// <summary>
        /// Can power be extracted?
        /// </summary>
        bool CanExtractPower { get; }

        /// <summary>
        /// Is Power Full? i.e. MaxPower == CurrentPower
        /// </summary>
        bool IsPowerFull { get; }

        /// <summary>
        /// What is the Priority of this Entity?
        /// <br>1 = highest priority.</br>
        /// <br>If every entity is the same priority, then the priority system is negated.</br>
        /// <br>Higher Priority generators are first to empty, machines are first to fill, etc.</br>
        /// <br>Can be hard-coded or set via GUI.</br>
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Takes powerOffered and removes any power needed and returns power left over.
        /// </summary>
        /// <param name="powerOffered">Power offered to this Entity</param>
        /// <param name="simulate">Whether to simulate function and not actually give power.</param>
        /// <returns>Power left over (0 if all power was consumed)</returns>
        ulong ReceivePower(ulong powerOffered, bool simulate = false);

        /// <summary>
        /// Reduces powerWanted by power held in this entity
        /// </summary>
        /// <param name="powerWanted">How much total power is needed</param>
        /// <param name="simulate">Whether to just simulate function and not actually take power.</param>
        /// <returns>Unfulfilled amount of powerWanted (0 if all wanted power was satisfied)</returns>
        ulong ExtractPower(ulong powerWanted, bool simulate = false);
        
    }
}

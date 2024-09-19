using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Simple (and familiar) interface for chargeable ITEMS (Tools, gadgets, etc)<br/>
    /// Its up to the item to manage its internal values.<br/>
    /// For full compatibility if this interface does not exist, use Item Durabilty.
    /// </summary>
    public interface IChargeableItem
    {
        /// <summary>
        /// What is the max power this machine can hold?
        /// <br>Type : Unsigned Long (ulong)</br>
        /// <br>Max Value : 18,446,744,073,709,551,615</br>
        /// </summary>
        ulong MaxPower { get; }
        /// <summary>
        /// Current power held by this Item.
        /// </summary>
        ulong CurrentPower { get; }
        /// <summary>
        /// Max Power Per Second rating of this Item.
        /// </summary>
        ulong MaxPPS { get; }
        /// <summary>
        /// Can power be extracted FROM this Item?
        /// </summary>
        bool CanExtractPower { get; }
        /// <summary>
        /// Can Power be pushed INTO this Item?
        /// </summary>
        bool CanReceivePower { get; }

        /// <summary>
        /// Power Needed/Available Rated to a items MaxPPS given the deltaTime.<br/>
        /// Used by a charger to quickly determine power for a given tick time.<br/>
        /// Should not actually alter a machines power.
        /// </summary>
        /// <param name="dt">DeltaTime (time betwen ticks in decimal seconds)</param>
        /// <param name="isInsert">True for inserting power, false for extracting.</param>
        /// <returns>Power for this deltatime</returns>
        ulong RatedPower(float dt, bool isInsert = false);

        /// <summary>
        /// Takes powerOffered and removes any power needed and returns power left over.<br/>
        /// It's up to the implementing item to track it's internal power and PPS limits.
        /// </summary>
        /// <param name="powerOffered">Power offered to this Item</param>
        /// <param name="dt">Delta Time; Time elapsed since last update.</param>
        /// <param name="simulate">[Optional] Whether to simulate function and not actually give power.</param>
        /// <returns>Power left over (0 if all power was consumed)</returns>
        ulong ReceivePower(ulong powerOffered, float dt, bool simulate = false);

        /// <summary>
        /// Reduces powerWanted by power held in this item.<br/>
        /// It's up to the implementing item to track it's internal power and PPS limits.
        /// </summary>
        /// <param name="powerWanted">How much total power is needed</param>
        /// <param name="dt">Delta Time; Time elapsed since last update.</param>
        /// <param name="simulate">[Optional] Whether to just simulate function and not actually take power.</param>
        /// <returns>Unfulfilled amount of powerWanted (0 if all wanted power was satisfied)</returns>
        ulong ExtractPower(ulong powerWanted, float dt, bool simulate = false);

        /// <summary>
        /// Completely fill (or drain) power buffer.<br/>
        /// A fast way for chargers to process power for this item.<br/>
        /// </summary>
        /// <param name="drain">[Optional] Drain power to 0 if true.</param>
        void CheatPower(bool drain = false);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace VintageEngineering.API
{
    /// <summary>
    /// Defines values used by the CrudeOilWell Block Entity for the MV tier pumpjack interaction
    /// </summary>
    public interface IOilWell
    {
        /// <summary>
        /// How many fluid portions are still available, set when block is spawned.
        /// </summary>
        long RemainingPortions { get; }
        /// <summary>
        /// Maximum Portions Per Second this well can deliver if not depleted.
        /// </summary>
        long MaxPPS { get; }
        /// <summary>
        /// Oil Block code, set in JSON
        /// </summary>
        string OilBlockCode { get; }
        /// <summary>
        /// Oil Portion Code, set in JSON
        /// </summary>
        string OilPortionCode { get; }
        /// <summary>
        /// How many portions to give per second when deposit is depleted.
        /// </summary>
        int TricklePortions { get; }
        /// <summary>
        /// Allow pumping infinite trickle portions from this well, set in JSON
        /// </summary>
        bool CanBeInfinite { get; }
        /// <summary>
        /// Maximum number of 'blocks' of fluid this deposit can provide.<br/>
        /// Actual fluid portions is this value * 1000 * portion Items Per Liter<br/>
        /// Also note, when random "Large" deposits spawn, this value is *2
        /// </summary>
        long MaxDepositBlocks { get; }
        /// <summary>
        /// Minimum number of 'blocks' of fluid this deposit can provide<br/>
        /// Actual fluid portions is this value * 1000 * portion Items Per Liter
        /// </summary>
        long MinDepositBlocks { get; }

        /// <summary>
        /// Initialize this deposit with startsize amount of liquid.<br/>
        /// Called once when the block is spawned while the terrain is generated.
        /// </summary>
        /// <param name="isLarge">Is this a large deposit?</param>
        void InitDeposit(bool isLarge, ICoreAPI api);
        /// <summary>
        /// A pump tick, returns a non-negative value of amount of portions returned
        /// </summary>
        /// <param name="dt">DeltaTime (a fractional second) time since last update tick.</param>
        /// <returns>Amount pumped.</returns>
        long PumpTick(float dt);
    }
}

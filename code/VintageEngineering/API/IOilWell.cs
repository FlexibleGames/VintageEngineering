using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        ulong RemainingPortions { get; }
        /// <summary>
        /// Maximum Portions Per Second this well can deliver if not depleted.
        /// </summary>
        ulong MaxPPS { get; }
        /// <summary>
        /// Oil Block code, set in JSON
        /// </summary>
        string OilBlockCode { get; }
        /// <summary>
        /// Oil Portion Code, set in JSON
        /// </summary>
        string OilPortionCode { get; }
        /// <summary>
        /// How many portions to give per tick when deposit is depleted.
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
        ulong MaxDepositBlocks { get; }
        /// <summary>
        /// Minimum number of 'blocks' of fluid this deposit can provide<br/>
        /// /// Actual fluid portions is this value * 1000 * portion Items Per Liter
        /// </summary>
        ulong MinDepositBlocks { get; }

        /// <summary>
        /// Initialize this deposit with startsize amount of liquid.<br/>
        /// Called once when the block is spawned while the terrain is generated.
        /// </summary>
        /// <param name="startsize">Initial total size of the deposit in blocks</param>
        void InitDeposit(ulong startsize);
        /// <summary>
        /// A pump tick, returns a non-negative value of amount of portions returned
        /// </summary>
        /// <param name="dt">DeltaTime (a fractional second) time since last update tick.</param>
        /// <returns>Amount pumped.</returns>
        ulong PumpTick(float dt);
    }
}

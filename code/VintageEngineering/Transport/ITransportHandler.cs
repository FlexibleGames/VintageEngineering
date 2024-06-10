using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport
{
    public interface ITransportHandler
    {
        /// <summary>
        /// What Type of pipe is handled by this TransportHandler
        /// </summary>
        EnumPipeUse PipeType { get; }
        /// <summary>
        /// The Main Update Tick for this handler, handles all logic of transporting on tick events.
        /// </summary>
        /// <param name="deltatime">Time since last update, in seconds (0.5 means a half second)</param>
        /// <param name="pos">BlockPos of this pipe node</param>
        /// <param name="world">World Accessor</param>
        /// <param name="node">Node being ticked</param>
        void TransportTick(float deltatime, BlockPos pos, IWorldAccessor world, PipeExtractionNode node);
    }
}

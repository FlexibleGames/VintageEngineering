using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Transport.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport.Handlers
{
    public class ItemTransportHandler : ITransportHandler
    {
        public EnumPipeUse PipeType => EnumPipeUse.item;

        public void TransportTick(float deltatime, BlockPos pos, IWorldAccessor world, PipeExtractionNode node)
        {
            // TODO Do all the things
        }
    }
}

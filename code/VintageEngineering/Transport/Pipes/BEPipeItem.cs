using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Transport.API;
using VintageEngineering.Transport.Handlers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport.Pipes
{
    public class BEPipeItem : BEPipeBase
    {
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            TransportHandler = new ItemTransportHandler();
        }

        public override bool CanConnectTo(IWorldAccessor world, BlockPos pos)
        {
            return base.CanConnectTo(world, pos);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            if (Api != null && Api.Side == EnumAppSide.Server)
            {
                for (int f = 0; f < 6; f++)
                {
                    if (extractionNodes[f] != null)
                    {
                        extractionNodes[f].SetHandler(TransportHandler);
                    }
                }
            }
        }
    }
}

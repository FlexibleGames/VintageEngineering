using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using VintageEngineering.Transport.API;
using VintageEngineering.Transport.Handlers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport.Pipes
{
    public class BEPipeFluid : BEPipeBase
    {
        private static FluidTransportHandler fluidHandler = new();
        public override void Initialize(ICoreAPI api)
        {            
            base.Initialize(api);
        }

        public override ITransportHandler GetHandler()
        {
            if (Api == null || Api.Side == EnumAppSide.Client) return null;
            return fluidHandler;
        }

        public override bool CanConnectTo(IWorldAccessor world, BlockPos pos)
        {
            IBlockEntityContainer bec = world.BlockAccessor.GetBlock(pos).GetInterface<IBlockEntityContainer>(world, pos);
            if (bec != null)
            {
                // TODO check and load config blacklist of assemblies and types to ignore
                foreach (ItemSlot slot in bec.Inventory)
                {
                    if (slot is ItemSlotLiquidOnly) return true;
                }
            }
            IVELiquidInterface liq = world.BlockAccessor.GetBlock(pos).GetInterface<IVELiquidInterface>(world, pos);
            if (liq != null) return true;

            // for debugging, until we get a pump
            Block blockat = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.FluidOrSolid);
            if (blockat != null && blockat.IsLiquid()) return true;

            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }
    }
}

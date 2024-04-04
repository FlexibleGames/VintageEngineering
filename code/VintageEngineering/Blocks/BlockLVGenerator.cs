using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using VintageEngineering.Electrical;

namespace VintageEngineering
{
    public class BlockLVGenerator : ElectricBlock
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
            }
            else
            {
                capi = api as ICoreClientAPI;
            }
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);
            BELVGenerator bETestGen = world.BlockAccessor.GetBlockEntity(pos) as BELVGenerator;
            if (bETestGen != null)
            {
                bETestGen.NeighborUpdate(world);
            }
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            BELVGenerator bETestGen = world.BlockAccessor.GetBlockEntity(blockPos) as BELVGenerator;
            if (bETestGen != null)
            {
                bETestGen.NeighborUpdate(world);
            }
        }

    }
}

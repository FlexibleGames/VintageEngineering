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

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }
            if (base.OnWireInteractionStart(world, byPlayer, blockSel)) return true;

            //BELVGenerator genEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BELVGenerator;
            //if (genEntity != null)
            //{
            //    genEntity.OnPlayerRightClick(byPlayer, blockSel);
            //    return true;
            //}

            return true;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BELVGenerator bETestGen = world.BlockAccessor.GetBlockEntity(pos) as BELVGenerator;
            if (bETestGen != null)
            {
                return bETestGen.GetOutputText() + base.GetPlacedBlockInfo(world, pos, forPlayer);
            }
            else
            {
                return base.GetPlacedBlockInfo(world, pos, forPlayer);
            }
        }
    }
}

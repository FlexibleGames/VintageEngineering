using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using VintageEngineering.Electrical;

namespace VintageEngineering
{
    public class BlockTestGen : ElectricBlock
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
            BETestGen bETestGen = world.BlockAccessor.GetBlockEntity(pos) as BETestGen;
            if (bETestGen != null)
            {
                bETestGen.NeighborUpdate(world);
            }
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            BETestGen bETestGen = world.BlockAccessor.GetBlockEntity(blockPos) as BETestGen;
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

            BETestGen genEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BETestGen;
            if (genEntity != null)
            {
                genEntity.OnPlayerRightClick(byPlayer, blockSel);
                return true;
            }

            return true;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BETestGen bETestGen = world.BlockAccessor.GetBlockEntity(pos) as BETestGen;
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

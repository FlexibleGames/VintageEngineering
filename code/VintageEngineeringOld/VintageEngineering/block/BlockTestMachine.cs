using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace VintageEngineering
{
    public class BlockTestMachine : Block
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

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {            
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }
            BETestMachine machEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BETestMachine;
            if (machEntity != null)
            {
                machEntity.OnPlayerRightClick(byPlayer, blockSel);
                return true;
            }
            else return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BETestMachine bETestMach = world.BlockAccessor.GetBlockEntity(pos) as BETestMachine;
            if (bETestMach != null)
            {
                return bETestMach.GetOutputText();
            }
            else
            {
                return base.GetPlacedBlockInfo(world, pos, forPlayer);
            }
        }
    }
}

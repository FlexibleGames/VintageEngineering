using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using VintageEngineering.Electrical;

namespace VintageEngineering
{
    public class BlockMetalPress : ElectricBlock
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
            if (base.OnWireInteractionStart(world, byPlayer, blockSel)) return true;
            BEMetalPress machEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEMetalPress;
            if (machEntity != null)
            {
                machEntity.OnPlayerRightClick(byPlayer, blockSel);
                return true;
            }
            else return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BEMetalPress bETestMach = world.BlockAccessor.GetBlockEntity(pos) as BEMetalPress;
            if (bETestMach != null)
            {
                return bETestMach.GetOutputText() + base.GetPlacedBlockInfo(world, pos, forPlayer);
            }
            else
            {
                return base.GetPlacedBlockInfo(world, pos, forPlayer);
            }
        }
    }
}

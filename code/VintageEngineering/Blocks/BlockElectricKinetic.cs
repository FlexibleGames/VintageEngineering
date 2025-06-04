using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace VintageEngineering.Blocks
{
    internal class BlockElectricKinetic : ElectricBlock, IMechanicalPowerBlock
    {
        private BlockFacing axleFace;
        public override void OnLoaded(ICoreAPI api)
        {
            axleFace = BlockFacing.FromCode(Variant["side"]);
            base.OnLoaded(api);
        }
        public void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
        }

        public bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return face == axleFace;
        }

        public MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>() is IMechanicalPowerDevice device) { return device.Network; }
            return null;
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            BlockFacing facing = BlockFacing.NORTH;
            try
            {
                facing = blockSel.Face;
            }
            catch
            {
                return false;
            }

            if (base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack))
            {

                if (
                    world.BlockAccessor.GetBlock(blockSel.Position) is IMechanicalPowerBlock block &&
                    block.HasMechPowerConnectorAt(world, blockSel.Position, facing.Opposite)
                )
                {
                    block.DidConnectAt(world,blockSel.Position, facing.Opposite);

                    world.BlockAccessor.GetBlockEntity(blockSel.Position)?
                        .GetBehavior<BEBehaviorMPBase>()?.tryConnect(facing);
                }

                return true;
            }

            return false;
        }

    }
}

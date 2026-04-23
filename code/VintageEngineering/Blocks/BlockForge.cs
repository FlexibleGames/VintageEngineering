using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering
{
    public class BlockVEForge: ElectricBlock
    {
        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            IHeatable heatable = world.BlockAccessor.GetBlockEntity(pos.UpCopy(1)) as IHeatable;
            BEForge forge = world.BlockAccessor.GetBlockEntity(pos) as BEForge;
            if (heatable != null)
            {
                forge.SetHeatableBlock(true);
            }
            else
            {
                forge.SetHeatableBlock(false);
            }
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEForge bea = world.BlockAccessor.GetBlockEntity<BEForge>(blockSel.Position);
            if (bea != null && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty &&
                !byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Code.Path.Contains("catenary"))
            {
                return bea.OnPlayerRightClick(byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}

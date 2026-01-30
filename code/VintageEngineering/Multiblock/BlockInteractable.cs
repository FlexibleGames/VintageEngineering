using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Multiblock
{
    public class BlockInteractable : Block
    {
        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            // TODO: Detect if we're linked to Core, if not, try to find it.
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer != null && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible?.Tool == EnumTool.Wrench)
                {
                    MBBEInteractable be = world.BlockAccessor.GetBlockEntity<MBBEInteractable>(blockSel.Position);
                    if (be != null)
                    {
                        return be.TriggerValidation(world, byPlayer, blockSel);
                    }
                }
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}

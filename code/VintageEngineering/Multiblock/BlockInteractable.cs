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
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.Entity.Controls.Sneak && (!byPlayer.InventoryManager.ActiveHotbarSlot?.Empty ?? false))
            {
                MBBEInteractable be = world.BlockAccessor.GetBlockEntity<MBBEInteractable>(blockSel.Position);
                if (be != null)
                {
                    return be.TriggerValidation(world, byPlayer, blockSel);
                }
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}

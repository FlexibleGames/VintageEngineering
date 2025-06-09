using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using Vintagestory.API.Common;

namespace VintageEngineering.Blocks
{
    public class BlockLVBlower: ElectricBlock
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer != null && byPlayer.Entity != null && byPlayer.Entity.Controls.Sneak &&
                byPlayer.InventoryManager.ActiveHotbarSlot != null &&
                byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                // a very important call
                if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)) return false;

                BEBlower bentity = world.BlockAccessor.GetBlockEntity<BEBlower>(blockSel.Position);
                if (bentity != null)
                {
                    bentity.OnRightClick(byPlayer);
                }
                return true;
            }
            else return base.OnBlockInteractStart(world, byPlayer, blockSel);             
        }
    }
}

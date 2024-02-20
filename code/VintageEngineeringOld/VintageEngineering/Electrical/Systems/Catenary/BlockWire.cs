using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// The Block that is the wire. Has many wire variants.
    /// <br>Has to be a block for rendering texture variants.</br>
    /// <br>Mods can extend the variants of this block to add wire types if needed.</br>
    /// </summary>
    public class BlockWire : Block
    {
        private CatenaryMod CatenaryMod;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.CatenaryMod = api.ModLoader.GetModSystem<CatenaryMod>(true);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity blockEntity = api.World.BlockAccessor.GetBlockEntity(pos);
            BEBehaviorWire beh = (blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorWire>() : null;
            if (beh != null)
            {
                return beh.GetWireCollisionBoxes();
            }
            return base.GetSelectionBoxes(blockAccessor, pos); 
        }
        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity blockEntity = api.World.BlockAccessor.GetBlockEntity(pos);
            BEBehaviorWire beh = blockEntity?.GetBehavior<BEBehaviorWire>();
            if (beh != null)
            {
                return beh.GetWireCollisionBoxes();
            }
            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;
            // Pass interaction to the Catenary mod system for proper connection event firing and rendering.
            this.CatenaryMod.OnInteract(this, slot, byEntity, blockSel);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            // Left click cancels an in-progress wire placement
            if (this.CatenaryMod.CancelPlace(this, byEntity))
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // This will get the drops for the entire block, not just a single wire connection
            BlockEntity blockEntity = this.api.World.BlockAccessor.GetBlockEntity(pos);
            BEBehaviorWire beh = blockEntity?.GetBehavior<BEBehaviorWire>();
            if (beh != null)
            {
                return beh.GetDrops(byPlayer);
            }
            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            /// Do not allow simple placing of this block in the world like other blocks.
            return false;
        }
    }
}

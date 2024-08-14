using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.blockentity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Blocks
{
    public class BlockFluidTank: Block
    {
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            bool preventDefault = false;
            foreach (BlockBehavior blockBehavior in this.BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;
                blockBehavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.PreventDefault)
                {
                    preventDefault = true;
                }
                if (handled == EnumHandling.PreventSubsequent)
                {
                    return;
                }
            }
            if (preventDefault)
            {
                return;
            }
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[]
                {
                    new ItemStack(this, 1)
                };
                BEFluidTank beft = world.BlockAccessor.GetBlockEntity(pos) as BEFluidTank;
                if (beft != null && !beft.Inventory[0].Empty)
                {
                    drops[0].Attributes.SetItemstack("contents", beft.Inventory[0].Itemstack);
                }
                for (int i = 0; i < drops.Length; i++)
                {
                    world.SpawnItemEntity(drops[i], new Vec3d((double)pos.X + 0.5, (double)pos.Y + 0.5, (double)pos.Z + 0.5), null);
                }
                world.PlaySoundAt(this.Sounds.GetBreakSound(byPlayer), (double)pos.X, (double)pos.Y, (double)pos.Z, byPlayer, true, 32f, 1f);
            }
            if (this.EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken(null);
                }
            }
            world.BlockAccessor.SetBlock(0, pos);            
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            if (base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack))
            {
                if (byItemStack.Attributes.HasAttribute("contents"))
                {
                    BEFluidTank beft = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEFluidTank;
                    if (beft != null)
                    {
                        ItemStack fluid = byItemStack.Attributes.GetItemstack("contents");
                        if (fluid != null) fluid.ResolveBlockOrItem(world);
                        beft.SetFluidOnPlace(fluid);
                    }
                }
            }
            return true;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack drops =  new ItemStack(this, 1);
            BEFluidTank beft = world.BlockAccessor.GetBlockEntity(pos) as BEFluidTank;
            if (beft != null && !beft.Inventory[0].Empty)
            {
                drops.Attributes.SetItemstack("contents", beft.Inventory[0].Itemstack);
            }
            return drops;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using VintageEngineering.Electrical;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;


namespace VintageEngineering
{
    public class BlockMixer : ElectricBlock
    {        

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use) )
            {
                return false;
            }
            BEMixer bemix = null;
            if (blockSel.Position != null)
            {
                bemix = (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEMixer);
            }
            if (bemix == null) return false;
            
            if (byPlayer != null && byPlayer.InventoryManager.ActiveHotbarSlot != null && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                ILiquidSink bucket = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible as ILiquidSink;
                if (bucket != null)
                {
                    ItemStack contents = bucket.GetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack);
                    if (contents != null)
                    {
                        DummySlot topush = new DummySlot(contents);
                        IVELiquidInterface ivel = bemix as IVELiquidInterface;
                        ItemSlotLiquidOnly push = ivel.GetLiquidAutoPushIntoSlot(blockSel.Face, topush);
                        if (push == null) return true;
                        WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(contents);
                        int capacityavailable = (int)(push.CapacityLitres * props.ItemsPerLitre) - (int)(push.StackSize);
                        if (capacityavailable >= contents.StackSize)
                        {
                            topush.TryPutInto(api.World, push, topush.StackSize);
                            bucket.SetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, null);                            
                        }
                        else
                        {
                            int moved = topush.TryPutInto(api.World, push, topush.StackSize);
                            contents.StackSize -= moved;
                            bucket.SetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, contents);
                        }
                        bemix.MarkDirty(true);
                        return true;
                    }
                    else
                    {
                        IVELiquidInterface ivel = bemix as IVELiquidInterface;
                        ItemSlotLiquidOnly pull = ivel.GetLiquidAutoPullFromSlot(blockSel.Face);
                        if (pull == null) return true;
                        WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(pull.Itemstack);
                        int cancontain = (int)(bucket.CapacityLitres * props.ItemsPerLitre);
                        if (cancontain >= pull.Itemstack.StackSize)
                        {
                            bucket.SetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, pull.Itemstack.Clone());
                            pull.TakeOutWhole();
                        }
                        else
                        {
                            ItemStack pulled = pull.Itemstack.Clone();
                            pulled.StackSize = cancontain;
                            bucket.SetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, pulled.Clone());
                            pull.TakeOut(cancontain);
                        }
                        bemix.MarkDirty(true);
                        return true;
                    }
                }
            }

            bool handled = base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (!handled && !byPlayer.WorldData.EntityControls.ShiftKey && blockSel.Position != null)
            {
                if (bemix != null)
                {
                    bemix.OnPlayerRightClick(byPlayer, blockSel);
                }
                return true;
            }
            return handled;
        }
    }
}

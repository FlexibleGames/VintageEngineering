using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using VintageEngineering.Electrical;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static Vintagestory.GameContent.BlockLiquidContainerBase;


namespace VintageEngineering
{
    /// <summary>
    /// A generic Fluid Containing Block for Bucket interaction.
    /// </summary>
    public class BlockFluidIO : ElectricBlock
    {        

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use) )
            {
                return false;
            }
            ItemSlot hotbarSlot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
            BlockEntity bentity = null;
            if (blockSel.Position != null)
            {
                bentity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            }
            if (bentity == null) return false;
            
            if (byPlayer != null && hotbarSlot != null && !hotbarSlot.Empty)
            {
                if (hotbarSlot.Itemstack.Collectible.Code.Path.Contains("stick")) // Stick to empty
                {
                    if (bentity is IVELiquidInterface ivela)
                    {
                        BlockEntityContainer bec = bentity as BlockEntityContainer;
                        if (bec != null)
                        {
                            foreach (int slotid in ivela.InputLiquidContainerSlotIDs)
                            {
                                if (bec.Inventory[slotid] != null && !bec.Inventory[slotid].Empty)
                                { 
                                    bec.Inventory[slotid]?.TakeOutWhole(); 
                                }
                            }
                            foreach (int slotid in ivela.OutputLiquidContainerSlotIDs)
                            {
                                if (bec.Inventory[slotid] != null && !bec.Inventory[slotid].Empty)
                                {
                                    bec.Inventory[slotid]?.TakeOutWhole(); 
                                }
                            }
                        }
                    }
                }
                ILiquidSource bucket = hotbarSlot.Itemstack.Collectible as ILiquidSource;
                if (bucket != null)
                {
                    if (!bucket.AllowHeldLiquidTransfer) return false;

                    ItemStack contents = bucket.GetContent(hotbarSlot.Itemstack);
                    if (contents != null && contents.StackSize > 0)
                    {
                        DummySlot topush;
                        if (hotbarSlot.Itemstack.StackSize > 1)
                        {
                            ItemStack singlebucket = hotbarSlot.Itemstack.Clone();
                            singlebucket.StackSize = 1;
                            topush = new((singlebucket.Collectible as ILiquidSource).GetContent(singlebucket));
                        }
                        else
                        {
                            topush = new(contents);
                        }
                        IVELiquidInterface ivelb = bentity as IVELiquidInterface;
                        ItemSlotLiquidOnly pushto = ivelb.GetLiquidAutoPushIntoSlot(blockSel.Face, topush);
                        if (pushto == null) return true;
                        WaterTightContainableProps propsa = BlockLiquidContainerBase.GetContainableProps(contents);
                        int capacityavailable = (int)(pushto.CapacityLitres * propsa.ItemsPerLitre) - (int)(pushto.StackSize);

                        ItemStack nummoved = topush.TakeOut(capacityavailable); //.TakeOut(topush.StackSize);
                        
                        if (nummoved != null && nummoved.StackSize > 0)
                        {
                            // add to pushto slot
                            DummySlot ds = new DummySlot(nummoved);                            

                            int paction = VEHelpers.SplitStackAndPerformAction(api, byPlayer.Entity, hotbarSlot, delegate (ItemStack stack)
                            {
                                bucket.TryTakeContent(stack, nummoved.StackSize);
                                return nummoved.StackSize;
                            });
                            ds.TryPutInto(world, pushto, ds.Itemstack.StackSize);
                            bentity.MarkDirty(true);
                            return true;
                        }
                    }
                }

                IVELiquidInterface ivel = bentity as IVELiquidInterface;
                ItemSlotLiquidOnly pull = ivel?.GetLiquidAutoPullFromSlot(blockSel.Face);
                if (pull == null)
                {
                    BlockEntityContainer bec = bentity as BlockEntityContainer;
                    foreach (int slot_id in ivel.InputLiquidContainerSlotIDs)
                    {
                        if (!bec.Inventory[slot_id].Empty) pull = bec.Inventory[slot_id] as ItemSlotLiquidOnly;
                    }
                }
                
                ILiquidSink sink = hotbarSlot.Itemstack.Collectible as ILiquidSink;

                if (sink != null && pull != null)
                {
                    if (!sink.AllowHeldLiquidTransfer) return false;

                    ItemStack owncontentstack = pull.Itemstack; // GetContent...
                    if (owncontentstack == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

                    ItemStack liquidstackforparticles = owncontentstack.Clone();

                    float liters = GameMath.Max(sink.TransferSizeLitres, sink.CapacityLitres);
                    int moved2 = VEHelpers.SplitStackAndPerformAction(api, byPlayer.Entity, hotbarSlot, (ItemStack stack) => sink.TryPutLiquid(stack, owncontentstack, liters));
                    if (moved2 > 0)
                    {
                        pull.TakeOut(moved2);
                        //DoLiquidMovedEffects(byPlayer, liquidstackforparticles, moved2, EnumLiquidDirection.Fill);
                        bentity.MarkDirty(true);
                        return true;
                    }
                }       
            }

            bool handled = base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (!handled && !byPlayer.WorldData.EntityControls.ShiftKey && blockSel.Position != null)
            {
                if (bentity != null && bentity is BlockEntityOpenableContainer beoc)
                {
                    beoc.OnPlayerRightClick(byPlayer, blockSel);
                }
                return true;
            }
            return handled;
        }
    }
}

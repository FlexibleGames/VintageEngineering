using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using VintageEngineering.Electrical;
using VintageEngineering.Transport.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport.Handlers
{
    public class FluidTransportHandler : ITransportHandler
    {
        public EnumPipeUse PipeType => EnumPipeUse.fluid;

        public void TransportTick(float deltatime, BlockPos pos, IWorldAccessor world, PipeExtractionNode node)
        {
            BEPipeBase us = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
            if (us == null) return; // sanity check
            ItemSlot pull = null;
            BlockPos connectedto = pos.AddCopy(BlockFacing.FromCode(node.FaceCode));
            if (!BEPipeBase.IsChunkLoaded(world, connectedto)) return;
            InventoryBase inv = (InventoryBase)((world.BlockAccessor.GetBlock(connectedto)?.GetInterface<IBlockEntityContainer>(world, connectedto))?.Inventory);
            int stacksize = node.UpgradeRate;
            int numperliter = 0;
            if (inv == null)
            {
                // check to see if block is a liquid block, for debugging
                Block lblock = world.BlockAccessor.GetBlock(connectedto, BlockLayersAccess.FluidOrSolid);
                if (lblock.IsLiquid())
                {
                    ItemStack lblockstack = new ItemStack(lblock);
                    WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(lblockstack);
                    if (wprops != null)
                    {
                        ItemStack portion = wprops.WhenFilled.Stack.Resolve(world, "Filter Pipe", true) ? wprops.WhenFilled.Stack.ResolvedItemstack : null;
                        if (portion == null) return;
                        portion.StackSize = portion.Collectible.MaxStackSize;
                        numperliter = ((int)wprops.ItemsPerLitre);
                        inv = node.Inventory as InventoryBase;
                        pull = inv[2];
                        pull.Itemstack = portion.Clone();
                    }
                }
                else return; 
            }                         
            if (world.BlockAccessor.GetBlockEntity(connectedto) is BlockEntityLiquidContainer)
            {
                pull = GetPullSlot(world, inv, node, true);
            }
            else if (pull == null)
            {
                pull = GetPullSlot(world, inv, node, false);
            }
            if (pull == null) 
            {                 
                return; 
            }
            if (stacksize == -1)
            {
                stacksize = pull.Itemstack.Collectible.MaxStackSize;
            }
            else
            {
                WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(pull.Itemstack);
                if (wprops != null)
                {
                    stacksize = ((int)((node.UpgradeRate / wprops.ItemsPerLitre) * pull.Itemstack.Collectible.MaxStackSize));
                    numperliter = ((int)wprops.ItemsPerLitre);
                }
            }
            ItemStackMoveOperation ismo = new ItemStackMoveOperation(world, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, stacksize);

            ItemSlot push = GetPushSlot(world, node, us.PushConnections, pull, numperliter);

            if (push == null) return; // sanity check 3
            try
            {
                int moved = pull.TryPutInto(push, ref ismo);
                if (moved == 0) return;
            }
            catch (Exception e)
            {
                return;
            }            
        }
        public ItemSlot GetPullSlot(IWorldAccessor world, InventoryBase inventory, PipeExtractionNode node, bool isbasefluidbe = false)
        {
            if (inventory == null) return null;
            if (inventory.Empty || inventory.Count == 0) return null;
            if (node.Filter.Empty || node.Filter.Itemstack.Attributes == null)
            {
                if (isbasefluidbe)
                {
                    foreach (ItemSlot slot in inventory)
                    {
                        if (slot is ItemSlotLiquidOnly) return slot;
                    }
                }
                else
                {
                    IVELiquidInterface iliq = world.BlockAccessor.GetBlock(node.BlockPosition).GetInterface<IVELiquidInterface>(world, node.BlockPosition);
                    if (iliq != null)
                    {
                        return iliq.GetLiquidAutoPullFromSlot(BlockFacing.FromCode(node.FaceCode).Opposite);
                    }
                }
                return null;
            }
            else
            {
                bool isblist = node.Filter.Itemstack.Attributes.GetBool("isblacklist");
                if (!node.Filter.Itemstack.Attributes.HasAttribute("filters"))
                {
                    // empty blacklist? empty whitelist blocks all items                    
                    if (isblist)
                    {
                        // empty blacklist, exclude nothing
                        if (isbasefluidbe)
                        {
                            foreach (ItemSlot slot in inventory)
                            {
                                if (slot is ItemSlotLiquidOnly) return slot;
                            }
                        }
                        else
                        {
                            IVELiquidInterface iliq = world.BlockAccessor.GetBlock(node.BlockPosition).GetInterface<IVELiquidInterface>(world, node.BlockPosition);
                            if (iliq != null)
                            {
                                return iliq.GetLiquidAutoPullFromSlot(BlockFacing.FromCode(node.FaceCode).Opposite);
                            }
                        }
                        return null;
                    }
                    else
                    {
                        // empty whitelist excludes everything...
                        return null;
                    }
                }
                else
                {
                    // we have a filter, it has filters, now we do the crazy part
                    TreeArrayAttribute taa = node.Filter.Itemstack.Attributes["filters"] as TreeArrayAttribute;
                    
                    IVELiquidInterface iliq = world.BlockAccessor.GetBlock(node.BlockPosition).GetInterface<IVELiquidInterface>(world, node.BlockPosition);
                    if (iliq != null)
                    {
                        foreach (int slotid in iliq.OutputLiquidContainerSlotIDs)
                        {
                            if (inventory[slotid] is not ItemSlotLiquidOnly) continue;
                            else
                            {
                                if (inventory[slotid].Empty) continue;
                                bool allowed = true;
                                foreach (TreeAttribute ta in taa.value)
                                {
                                    string thecode = ta.GetString("code", "error");
                                    if (thecode.Contains('*'))
                                    {
                                        // wildcard detected
                                        if (WildcardUtil.Match(new AssetLocation(thecode), inventory[slotid].Itemstack.Collectible.Code))
                                        {
                                            // wildcard matched
                                            if (isblist) allowed = false;
                                        }
                                        else
                                        {
                                            // not a match
                                            if (!isblist) allowed = false;
                                        }
                                    }
                                    else
                                    {
                                        // no wildcard
                                        if (thecode == inventory[slotid].Itemstack.Collectible.Code.ToString())
                                        {
                                            if (isblist) allowed = false;                                            
                                        }
                                        else
                                        {
                                            if (!isblist) allowed = false;                                            
                                        }
                                    }
                                }
                                if (allowed) return inventory[slotid];
                            }
                        }
                        return null;
                    }
                    foreach (ItemSlot slot in inventory)
                    {
                        if (slot is not ItemSlotLiquidOnly) continue;
                        else
                        {
                            if (slot.Empty) continue;
                            bool allowed = true;
                            foreach (TreeAttribute ta in taa.value)
                            {
                                string thecode = ta.GetString("code", "error");
                                if (thecode.Contains('*'))
                                {
                                    // wildcard detected
                                    if (WildcardUtil.Match(new AssetLocation(thecode), slot.Itemstack.Collectible.Code))
                                    {
                                        if (isblist) allowed = false;
                                    }
                                    else
                                    {
                                        if (!isblist) allowed = false;
                                    }
                                }
                                else
                                {
                                    // no wildcard
                                    if (thecode == slot.Itemstack.Collectible.Code.ToString())
                                    {
                                        if (isblist) allowed = false;
                                    }
                                    else
                                    {
                                        if (!isblist) allowed = false;
                                    }
                                }
                            }
                            if (allowed) return slot;
                        }
                    }
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns the first valid ItemSlot for the given slot "pullfrom"<br/>
        /// Returns null if no valid slot is found.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="node">PipeExtractionNode being ticked</param>
        /// <param name="pushcons">PipeConnection list to check for valid push slots.</param>
        /// <param name="pullfrom">ItemSlot that is providing the ItemStack to move.</param>
        /// <param name="perliter">WProps num items per liter of stack being pushed.</param>
        /// <returns>Valid ItemSlot to push into, or null if no slot is found.</returns>
        public ItemSlot GetPushSlot(IWorldAccessor world, PipeExtractionNode node, List<PipeConnection> pushcons, ItemSlot pullfrom, int perliter = 100)
        {
            if (pushcons == null || pushcons.Count == 0) { return null; }
            
            if (node.PipeDistribution == EnumPipeDistribution.Nearest)
            {
                // what is the cost of this call?
                PipeConnection[] conarray = pushcons.ToArray();
                Array.Sort(conarray, (x, y) => x.Distance.CompareTo(y.Distance));

                for (int x = 0; x < conarray.Length; x++)
                {
                    if (!BEPipeBase.IsChunkLoaded(world, conarray[x].Position)) continue;
                    IVELiquidInterface ivel = world.BlockAccessor.GetBlock(conarray[x].Position).GetInterface<IVELiquidInterface>(world, conarray[x].Position);
                    if (ivel != null)
                    {
                        ItemSlotLiquidOnly slot = ivel.GetLiquidAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode), pullfrom);
                        if (slot == null) continue;
                        return slot;
                    }

                    IBlockEntityContainer contain = world.BlockAccessor.GetBlock(conarray[x].Position).GetInterface<IBlockEntityContainer>(world, conarray[x].Position);
                    foreach (ItemSlot slot in contain.Inventory)
                    {
                        if (slot is ItemSlotLiquidOnly && 
                            (slot.Empty || 
                            (slot.Itemstack.StackSize < (slot as ItemSlotLiquidOnly).CapacityLitres*perliter && slot.Itemstack.Collectible == pullfrom.Itemstack.Collectible))) return slot;
                        else continue;
                    }
                }
            }
            else if (node.PipeDistribution == EnumPipeDistribution.Farthest)
            {
                PipeConnection[] conarray = pushcons.ToArray();
                Array.Sort(conarray, (x, y) => y.Distance.CompareTo(x.Distance));
                for (int x = 0; x < conarray.Length; x++)
                {
                    if (!BEPipeBase.IsChunkLoaded(world, conarray[x].Position)) continue;
                    IVELiquidInterface ivel = world.BlockAccessor.GetBlock(conarray[x].Position).GetInterface<IVELiquidInterface>(world, conarray[x].Position);
                    if (ivel != null)
                    {
                        ItemSlotLiquidOnly slot = ivel.GetLiquidAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode), pullfrom);
                        if (slot == null) continue;
                        return slot;
                    }

                    IBlockEntityContainer contain = world.BlockAccessor.GetBlock(conarray[x].Position).GetInterface<IBlockEntityContainer>(world, conarray[x].Position);
                    foreach (ItemSlot slot in contain.Inventory)
                    {
                        if (slot is ItemSlotLiquidOnly && (slot.Empty || slot.Itemstack.StackSize < (slot as ItemSlotLiquidOnly).CapacityLitres)) return slot;
                        else continue;
                    }
                }
            }
            else if (node.PipeDistribution == EnumPipeDistribution.RoundRobin)
            {
                // need to reset or invalidate if a pipe is added at the time of the tick
                if (node.PushEnumerator.Current == null)
                {
                    node.PushEnumerator = pushcons.GetEnumerator();
                    node.PushEnumerator.MoveNext();
                }
                else
                {
                    if (!node.PushEnumerator.MoveNext())
                    {
                        node.PushEnumerator.Dispose();
                        node.PushEnumerator = pushcons.GetEnumerator();
                        node.PushEnumerator.MoveNext();
                    }
                }

                PipeConnection current = node.PushEnumerator.Current;
                if (!BEPipeBase.IsChunkLoaded(world, current.Position)) return null;
                IVELiquidInterface ivel = world.BlockAccessor.GetBlock(current.Position).GetInterface<IVELiquidInterface>(world, current.Position);
                if (ivel != null)
                {
                    ItemSlotLiquidOnly slot = ivel.GetLiquidAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode), pullfrom);                    
                    return slot;
                }
                IBlockEntityContainer contain = world.BlockAccessor.GetBlock(current.Position).GetInterface<IBlockEntityContainer>(world, current.Position);
                foreach (ItemSlot slot in contain.Inventory)
                {
                    if (slot is ItemSlotLiquidOnly && (slot.Empty || slot.Itemstack.StackSize < (slot as ItemSlotLiquidOnly).CapacityLitres)) return slot;
                    else continue;
                }
            }
            else
            {
                // this is Random
                int randomcon = world.Rand.Next(pushcons.Count);
                PipeConnection current = pushcons[randomcon];
                if (!BEPipeBase.IsChunkLoaded(world, current.Position)) return null;
                IVELiquidInterface ivel = world.BlockAccessor.GetBlock(current.Position).GetInterface<IVELiquidInterface>(world, current.Position);
                if (ivel != null)
                {
                    ItemSlotLiquidOnly slot = ivel.GetLiquidAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode), pullfrom);
                    return slot;
                }
                IBlockEntityContainer contain = world.BlockAccessor.GetBlock(current.Position).GetInterface<IBlockEntityContainer>(world, current.Position);
                foreach (ItemSlot slot in contain.Inventory)
                {
                    if (slot is ItemSlotLiquidOnly && (slot.Empty || slot.Itemstack.StackSize < (slot as ItemSlotLiquidOnly).CapacityLitres)) return slot;
                    else continue;
                }
            }
            return null;
        }
    }
}

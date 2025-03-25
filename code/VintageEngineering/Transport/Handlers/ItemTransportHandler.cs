﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Transport.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport.Handlers
{
    public class ItemTransportHandler : ITransportHandler
    {
        public EnumPipeUse PipeType => EnumPipeUse.item;

        public void TransportTick(float deltatime, BlockPos pos, IWorldAccessor world, PipeExtractionNode node)
        {
            if (!BEPipeBase.IsChunkLoaded(world, pos)) return;

            BEPipeBase us = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
            if (us == null) return; // sanity check

            BlockPos connectedto = pos.AddCopy(BlockFacing.FromCode(node.FaceCode));
            if (!BEPipeBase.IsChunkLoaded(world, connectedto)) return;
            if (world.BlockAccessor.GetBlock(connectedto) is BlockMultiblock target)
            {
                // if we're pointed at a multiblock, try to access the core instead.                
                if (target != null)
                {
                    connectedto.Add(target.OffsetInv);
                }
                if (!BEPipeBase.IsChunkLoaded(world, connectedto)) return;
            }
            InventoryBase inv = (InventoryBase)((world.BlockAccessor.GetBlock(connectedto).GetInterface<IBlockEntityContainer>(world, connectedto)).Inventory);
            if (inv == null) return; // sanity check 2
            int stacksize = node.UpgradeRate;
            ItemSlot pull;
            if (world.BlockAccessor.GetBlockEntity(connectedto) is BlockEntityGenericTypedContainer)
            {
                pull = GetPullSlot(inv, node, true);
            }
            else
            { 
                pull = GetPullSlot(inv, node, false); 
            }
            if (pull == null) return;
            if (stacksize == -1)
            {
                stacksize = pull.Itemstack?.Collectible.MaxStackSize ?? 1;
            }
            ItemStackMoveOperation ismo = new ItemStackMoveOperation(world, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, stacksize);            

            ItemSlot push = GetPushSlot(world, node, us.PushConnections, pull);

            if (push == null) return; // sanity check 3

            int moved = pull.TryPutInto(push, ref ismo);
            if (moved == 0) return;
            else pull.MarkDirty();
        }

        public ItemSlot GetPullSlot(InventoryBase inventory, PipeExtractionNode node, bool isGeneric = false)
        {
            if (inventory.Empty || inventory.Count == 0) return null;
            if (node.Filter.Empty || node.Filter.Itemstack.Attributes == null)
            {
                if (isGeneric)
                {
                    return inventory.GetAutoPullFromSlot(BlockFacing.DOWN);
                }
                return inventory.GetAutoPullFromSlot(BlockFacing.FromCode(node.FaceCode).Opposite);
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
                        if (isGeneric)
                        {
                            return inventory.GetAutoPullFromSlot(BlockFacing.DOWN);
                        }
                        return inventory.GetAutoPullFromSlot(BlockFacing.FromCode(node.FaceCode).Opposite);
                    }
                    else
                    {
                        // empty whitelist
                        return null;
                    }
                }
                else
                {
                    // we have a filter, it has filters, now we do the crazy part
                    TreeArrayAttribute taa = node.Filter.Itemstack.Attributes["filters"] as TreeArrayAttribute;
                    foreach (ItemSlot slot in inventory)
                    {
                        if (slot is ItemSlotLiquidOnly) continue;
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
                                        // item is a match
                                        if (isblist) allowed = false;
                                    }
                                    else
                                    {
                                        // not a match
                                        if (!isblist) allowed = false; // a whitelist that didn't match is blocked
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
        /// <returns>Valid ItemSlot to push into, or null if no slot is found.</returns>
        public ItemSlot GetPushSlot(IWorldAccessor world, PipeExtractionNode node, List<PipeConnection> pushcons, ItemSlot pullfrom)
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
                    
                    BlockPos target = conarray[x].Position.Copy();
                    Block targetblock = world.BlockAccessor.GetBlock(target);
                    if (targetblock is BlockMultiblock mbtarget)
                    {
                        // if we're pointed at a multiblock, try to access the core instead.                        
                        if (mbtarget != null)
                        {
                            target.Add(mbtarget.OffsetInv);
                        }
                        if (!BEPipeBase.IsChunkLoaded(world, target)) return null;
                    }                    
                    IBlockEntityContainer contain = world.BlockAccessor.GetBlock(target).GetInterface<IBlockEntityContainer>(world, target);
                    if (contain.Inventory is InventoryBase inv)
                    {                        
                        ItemSlot push = inv.GetAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode).Opposite, pullfrom);
                        if (push == null) 
                        {                             
                            continue; 
                        }
                        return push;
                    }
                    else continue;
                }
            }
            else if (node.PipeDistribution == EnumPipeDistribution.Farthest)
            {
                PipeConnection[] conarray = pushcons.ToArray();
                Array.Sort(conarray, (x, y) => y.Distance.CompareTo(x.Distance));
                for (int x = 0; x < conarray.Length; x++)
                {
                    if (!BEPipeBase.IsChunkLoaded(world, conarray[x].Position)) continue;
                    BlockPos target = conarray[x].Position.Copy();
                    Block targetblock = world.BlockAccessor.GetBlock(target);
                    if (targetblock is BlockMultiblock mbtarget)
                    {
                        // if we're pointed at a multiblock, try to access the core instead.                        
                        if (mbtarget != null)
                        {
                            target.Add(mbtarget.OffsetInv);
                        }
                        if (!BEPipeBase.IsChunkLoaded(world, target)) return null;
                    }                    
                    IBlockEntityContainer contain = world.BlockAccessor.GetBlock(target).GetInterface<IBlockEntityContainer>(world, target);
                    if (contain.Inventory is InventoryBase inv)
                    {
                        ItemSlot push = inv.GetAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode).Opposite, pullfrom);
                        if (push == null) continue;
                        return push;
                    }
                    else continue;
                }
            }
            else if (node.PipeDistribution == EnumPipeDistribution.RoundRobin)
            {
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
                BlockPos target = current.Position.Copy();
                Block targetblock = world.BlockAccessor.GetBlock(target);
                if (targetblock is BlockMultiblock mbtarget)
                {
                    // if we're pointed at a multiblock, try to access the core instead.                        
                    if (mbtarget != null)
                    {
                        target.Add(mbtarget.OffsetInv);
                    }
                    if (!BEPipeBase.IsChunkLoaded(world, target)) return null;
                }                
                IBlockEntityContainer contain = world.BlockAccessor.GetBlock(target).GetInterface<IBlockEntityContainer>(world, target);
                if (contain.Inventory is InventoryBase inv)
                {
                    ItemSlot push = inv.GetAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode).Opposite, pullfrom);                    
                    return push;
                }
            }
            else
            {
                // this is Random                
                int randomcon = world.Rand.Next(pushcons.Count);                
                PipeConnection current = pushcons[randomcon];
                if (!BEPipeBase.IsChunkLoaded(world, current.Position)) return null;
                BlockPos target = current.Position.Copy();
                Block targetblock = world.BlockAccessor.GetBlock(target);
                if (targetblock is BlockMultiblock mbtarget)
                {
                    // if we're pointed at a multiblock, try to access the core instead.                        
                    if (mbtarget != null)
                    {
                        target.Add(mbtarget.OffsetInv);
                    }
                    if (!BEPipeBase.IsChunkLoaded(world, target)) return null;
                }                
                IBlockEntityContainer contain = world.BlockAccessor.GetBlock(target).GetInterface<IBlockEntityContainer>(world, target);
                if (contain.Inventory is InventoryBase inv)
                {
                    ItemSlot push = inv.GetAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode).Opposite, pullfrom);
                    return push;
                }
            }
            return null;
        }
    }
}

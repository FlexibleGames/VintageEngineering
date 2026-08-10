using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
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
            if (!VEHelpers.IsChunkLoaded(world, pos)) return;

            BEPipeBaseNew us = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBaseNew;
            if (us == null) return; // sanity check

            BlockPos connectedto = pos.AddCopy(BlockFacing.FromCode(node.FaceCode));
            if (!VEHelpers.IsChunkLoaded(world, connectedto)) return;
            if (world.BlockAccessor.GetBlock(connectedto) is BlockMultiblock target)
            {
                // if we're pointed at a multiblock, try to access the core instead.
                if (target != null)
                {
                    connectedto.Add(target.OffsetInv);
                }
                if (!VEHelpers.IsChunkLoaded(world, connectedto)) return;
            }
            InventoryBase inv = (InventoryBase)((world.BlockAccessor.GetBlock(connectedto).GetInterface<IBlockEntityContainer>(world, connectedto))?.Inventory);
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
            if (pull == null || pull.Empty) return;
            string pullSubnet = node.SubNet;
            if (stacksize == -1) // stacksize -1 means the steel upgrade, 10 stacks per tick
            {
                stacksize = pull.Itemstack?.Collectible.MaxStackSize*10 ?? 1;
            }
            if (stacksize > pull.Itemstack?.StackSize) stacksize = pull.Itemstack.StackSize;
            ItemStackMoveOperation ismo = new ItemStackMoveOperation(world, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.DirectMerge, stacksize);

            ItemSlot push = GetPushSlot(world, node, node.InsertNodes, pull, pullSubnet);

            if (push == null) return; // sanity check 3
            int moved = 0;
            // base game push slot, not a drawer
            if (push.MaxSlotStackSize == 999999 || push.MaxSlotStackSize == 1) moved = pull.TryPutInto(push, ref ismo);
            else moved = TryPutIntoBulk(pull, push, ref ismo);
            //if (moved == 0) return;
            //else pull.MarkDirty();
        }

        public int TryPutIntoBulk(ItemSlot source, ItemSlot sink, ref ItemStackMoveOperation ismo)
        {
            if (!sink.CanTakeFrom(source, EnumMergePriority.AutoMerge) || !source.CanTake() || source.Itemstack == null) return 0;
            
            InventoryBase inv = sink.Inventory;
            if (inv != null && !inv.CanContain(sink, source)) return 0;
            if (sink.Empty)
            {
                int quant = Math.Min(sink.GetRemainingSlotSpace(source.Itemstack), ismo.RequestedQuantity);
                if (quant > 0)
                {
                    sink.Itemstack = source.TakeOut(quant);
                    ismo.MovedQuantity = ismo.MovableQuantity = Math.Min(sink.StackSize, quant);
                    sink.OnItemSlotModified(sink.Itemstack);
                    source.OnItemSlotModified(sink.Itemstack);
                }
                return ismo.MovedQuantity;
            }
            ItemStackMergeOperation merge = ismo.ToMergeOperation(sink, source);
            ismo = merge;
            int originalquant = ismo.RequestedQuantity;
            ismo.RequestedQuantity = Math.Min(sink.GetRemainingSlotSpace(source.Itemstack), ismo.RequestedQuantity);
            if (ismo.RequestedQuantity > 0)
            {
                TryMergeStacks(merge);
                if (merge.MovedQuantity > 0)
                {
                    sink.OnItemSlotModified(sink.Itemstack);
                    source.OnItemSlotModified(sink.Itemstack);
                }
            }
            ismo.RequestedQuantity = originalquant;
            return merge.MovedQuantity;

        }

        public void TryMergeStacks(ItemStackMergeOperation op)
        {
            // will ignore collectable MaxStackSize and rely on the slots MaxStackSize instead
            op.MovableQuantity = this.GetMergableQuantity(op.SinkSlot, op.SourceSlot, op.CurrentPriority);
            CollectibleObject sinkobj = op.SinkSlot.Itemstack!.Collectible;
            if (op.MovableQuantity == 0)
            {
                return;
            }
            if (!op.SinkSlot.CanTakeFrom(op.SourceSlot, op.CurrentPriority))
            {
                return;
            }
            bool doTemperatureAveraging = false;
            bool doTransitionAveraging = false;
            op.MovedQuantity = GameMath.Min(new int[]
            {
                op.SinkSlot.GetRemainingSlotSpace(op.SourceSlot.Itemstack),
                op.MovableQuantity,
                op.RequestedQuantity
            });
            if (sinkobj.HasTemperature(op.SinkSlot.Itemstack) || sinkobj.HasTemperature(op.SourceSlot.Itemstack))
            {
                if (op.CurrentPriority < EnumMergePriority.DirectMerge && Math.Abs(sinkobj.GetTemperature(op.World, op.SinkSlot.Itemstack) - sinkobj.GetTemperature(op.World, op.SourceSlot.Itemstack)) > 30f)
                {
                    op.MovedQuantity = 0;
                    op.MovableQuantity = 0;
                    op.RequiredPriority = new EnumMergePriority?(EnumMergePriority.DirectMerge);
                    return;
                }
                doTemperatureAveraging = true;
            }
            TransitionState[] sourceTransitionStates = sinkobj.UpdateAndGetTransitionStates(op.World, op.SourceSlot);
            TransitionState[] targetTransitionStates = sinkobj.UpdateAndGetTransitionStates(op.World, op.SinkSlot);
            Dictionary<EnumTransitionType, TransitionState> targetStatesByType = null;
            if (sourceTransitionStates != null)
            {
                bool canDirectStack = true;
                bool canAutoStack = true;
                if (targetTransitionStates == null)
                {
                    op.MovedQuantity = 0;
                    op.MovableQuantity = 0;
                    return;
                }
                targetStatesByType = new Dictionary<EnumTransitionType, TransitionState>();
                foreach (TransitionState state in targetTransitionStates)
                {
                    targetStatesByType[state.Props.Type] = state;
                }
                foreach (TransitionState sourceState in sourceTransitionStates)
                {
                    TransitionState targetState = null;
                    if (!targetStatesByType.TryGetValue(sourceState.Props.Type, out targetState))
                    {
                        canAutoStack = false;
                        canDirectStack = false;
                        break;
                    }
                    if (Math.Abs(targetState.TransitionedHours - sourceState.TransitionedHours) > 4f && Math.Abs(targetState.TransitionedHours - sourceState.TransitionedHours) / sourceState.FreshHours > 0.03f)
                    {
                        canAutoStack = false;
                    }
                }
                if (!canAutoStack && op.CurrentPriority < EnumMergePriority.DirectMerge)
                {
                    op.MovedQuantity = 0;
                    op.MovableQuantity = 0;
                    op.RequiredPriority = new EnumMergePriority?(EnumMergePriority.DirectMerge);
                    return;
                }
                if (!canDirectStack)
                {
                    op.MovedQuantity = 0;
                    op.MovableQuantity = 0;
                    return;
                }
                doTransitionAveraging = true;
            }
            if (op.SourceSlot.Itemstack == null)
            {
                op.MovedQuantity = 0;
                return;
            }
            if (op.MovedQuantity <= 0)
            {
                return;
            }
            if (op.SinkSlot.Itemstack == null)
            {
                op.SinkSlot.Itemstack = new ItemStack(op.SourceSlot.Itemstack.Collectible, 0);
            }
            if (doTemperatureAveraging)
            {
                sinkobj.SetTemperature(op.World, op.SinkSlot.Itemstack, ((float)op.SinkSlot.StackSize * sinkobj.GetTemperature(op.World, op.SinkSlot.Itemstack) + (float)op.MovedQuantity * sinkobj.GetTemperature(op.World, op.SourceSlot.Itemstack)) / (float)(op.SinkSlot.StackSize + op.MovedQuantity), true);
            }
            if (doTransitionAveraging)
            {
                float t = (float)op.MovedQuantity / (float)(op.MovedQuantity + op.SinkSlot.StackSize);
                foreach (TransitionState sourceState2 in sourceTransitionStates!)
                {
                    TransitionState targetState2 = targetStatesByType![sourceState2.Props.Type];
                    sinkobj.SetTransitionState(op.SinkSlot.Itemstack, sourceState2.Props.Type, sourceState2.TransitionedHours * t + targetState2.TransitionedHours * (1f - t));
                }
            }
            // custom HasVoid behavior
            int emptySpace = op.SinkSlot.MaxSlotStackSize - op.SinkSlot.StackSize;
            if (emptySpace == 0 && op.SinkSlot.GetRemainingSlotSpace(op.SourceSlot.Itemstack) > 0)
            {
                emptySpace = op.SinkSlot.GetRemainingSlotSpace(op.SourceSlot.Itemstack);
            }
            if (emptySpace <= op.MovedQuantity)
            {
                //int remainder = op.MovedQuantity - emptySpace;
                op.SinkSlot.Itemstack.StackSize += emptySpace;
                op.SourceSlot.TakeOut(emptySpace);
                op.MovedQuantity = emptySpace;
            }
            else
            {
                op.SinkSlot.Itemstack.StackSize += op.MovedQuantity;
                op.SourceSlot.TakeOut(op.MovedQuantity);
            }
        }

        public virtual int GetMergableQuantity(ItemSlot sinkStack, ItemSlot sourceStack, EnumMergePriority priority)
        {
            if (sinkStack.Itemstack.Collectible.Equals(sourceStack.Itemstack, sinkStack.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                int remain = sinkStack.GetRemainingSlotSpace(sourceStack.Itemstack);
                if (remain > 0)
                {
                    if (sinkStack.StackSize < sinkStack.MaxSlotStackSize || remain > 0)
                    {
                        return Math.Min(remain, sourceStack.StackSize);
                    }
                }
            }
            return 0;
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
                    for(int x = 0; x < inventory.Count; x++) //each (ItemSlot slot in inventory)
                    {
                        ItemSlot slot = inventory[x];
                        if (slot == null) continue;
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
        /// <param name="pushcons">PipeInsertNode list to check for valid push slots.</param>
        /// <param name="pullfrom">ItemSlot that is providing the ItemStack to move.</param>
        /// <returns>Valid ItemSlot to push into, or null if no slot is found.</returns>
        public ItemSlot GetPushSlot(IWorldAccessor world, PipeExtractionNode node, List<PipeInsertNode> pushcons, ItemSlot pullfrom, string subnet)
        {
            if (pushcons == null || pushcons.Count == 0) { return null; }
            if (node.PipeDistribution == EnumPipeDistribution.Nearest)
            {
                // what is the cost of this call?
                PipeInsertNode[] conarray = pushcons.ToArray();
                //Array.Sort(conarray, (x, y) => x.Distance.CompareTo(y.Distance));

                for (int x = 0; x < conarray.Length; x++)
                {
                    if (!VEHelpers.IsChunkLoaded(world, conarray[x].Position)) continue;
                    if (!VEHelpers.IsChunkLoaded(world, conarray[x].NodePosition)) continue;
                    if (subnet != null && subnet != string.Empty && subnet.Length > 0)
                    {
                        string uptodate = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(conarray[x].NodePosition).InsertNodes[conarray[x].Facing.Index].SubNet;
                        if (!uptodate.Contains(subnet)) continue;
                    }
                    BlockPos target = conarray[x].Position.Copy();
                    Block targetblock = world.BlockAccessor.GetBlock(target);
                    if (targetblock is BlockMultiblock mbtarget)
                    {
                        // if we're pointed at a multiblock, try to access the core instead.
                        if (mbtarget != null)
                        {
                            target.Add(mbtarget.OffsetInv);
                        }
                        if (!VEHelpers.IsChunkLoaded(world, target)) return null;
                    }
                    IBlockEntityContainer contain = world.BlockAccessor.GetBlock(target).GetInterface<IBlockEntityContainer>(world, target);
                    if (contain != null && contain.Inventory is InventoryBase inv)
                    {
                        ItemSlot push = inv.GetAutoPushIntoSlot(BlockFacing.FromCode(conarray[x].FaceCon).Opposite, pullfrom);
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
                PipeInsertNode[] conarray = pushcons.ToArray();
                //Array.Sort(conarray, (x, y) => y.Distance.CompareTo(x.Distance));
                for (int x = 0; x < conarray.Length; x++)
                {
                    if (!VEHelpers.IsChunkLoaded(world, conarray[x].Position)) continue;
                    if (subnet != null && subnet != string.Empty && subnet.Length > 0)
                    {
                        string uptodate = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(conarray[x].NodePosition).InsertNodes[conarray[x].Facing.Index].SubNet;
                        if (!uptodate.Contains(subnet)) continue;
                    }
                    BlockPos target = conarray[x].Position.Copy();
                    Block targetblock = world.BlockAccessor.GetBlock(target);
                    if (targetblock is BlockMultiblock mbtarget)
                    {
                        // if we're pointed at a multiblock, try to access the core instead.
                        if (mbtarget != null)
                        {
                            target.Add(mbtarget.OffsetInv);
                        }
                        if (!VEHelpers.IsChunkLoaded(world, target)) return null;
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
                if (node.IsSleeping) return null;
                if (node.PushEnumerator.Current == null)
                {
                    node.PushEnumerator = pushcons.GetEnumerator();
                    node.PushEnumerator.MoveNext();
                }
                else
                {
                    try
                    {
                        if (!node.PushEnumerator.MoveNext())
                        {
                            node.PushEnumerator.Dispose();
                            node.PushEnumerator = pushcons.GetEnumerator();
                            node.PushEnumerator.MoveNext();
                        }
                    }
                    catch //(Exception e)
                    {
                        node.PushEnumerator.Dispose();
                        node.PushEnumerator = pushcons.GetEnumerator();
                        node.PushEnumerator.MoveNext();
                    }
                }                
                PipeInsertNode current = node.PushEnumerator.Current;
                if (!VEHelpers.IsChunkLoaded(world, current.Position)) return null;
                if (subnet != null && subnet != string.Empty && subnet.Length > 0)
                {
                    string uptodate = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(current.NodePosition).InsertNodes[current.Facing.Index].SubNet;
                    if (!uptodate.Contains(subnet)) return null;
                }
                BlockPos target = current.Position.Copy();
                Block targetblock = world.BlockAccessor.GetBlock(target);
                if (targetblock is BlockMultiblock mbtarget)
                {
                    // if we're pointed at a multiblock, try to access the core instead.
                    if (mbtarget != null)
                    {
                        target.Add(mbtarget.OffsetInv);
                    }
                    if (!VEHelpers.IsChunkLoaded(world, target)) return null;
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
                PipeInsertNode current = pushcons[randomcon];
                
                if (!VEHelpers.IsChunkLoaded(world, current.Position)) return null;
                if (subnet != null && subnet != string.Empty && subnet.Length > 0)
                {
                    string uptodate = world.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(current.Position).InsertNodes[current.Facing.Index].SubNet;
                    if (!uptodate.Contains(subnet)) return null;
                }
                BlockPos target = current.Position.Copy();
                Block targetblock = world.BlockAccessor.GetBlock(target);
                if (targetblock is BlockMultiblock mbtarget)
                {
                    // if we're pointed at a multiblock, try to access the core instead.
                    if (mbtarget != null)
                    {
                        target.Add(mbtarget.OffsetInv);
                    }
                    if (!VEHelpers.IsChunkLoaded(world, target)) return null;
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

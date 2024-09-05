using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.API
{
    public class ItemSlotLargeLiquid : ItemSlotLiquidOnly
    {
        public ItemSlotLargeLiquid(InventoryBase inventory, float capacityLitres) : base(inventory, capacityLitres)
        {
            this.MaxSlotStackSize = (int)(capacityLitres * 100);
        }

        public void SetCapacity(int capacity, int itemperliter = 100)
        {
            if (CapacityLitres != capacity)
            {
                CapacityLitres = capacity;
                MaxSlotStackSize = capacity * itemperliter; // as a default, gets specific when you put something in here
            }
        }

        /// <summary>
        /// Attempts to TAKE items from sourceSlot into our slot
        /// </summary>
        /// <param name="sourceSlot">Source to pull from.</param>
        /// <param name="op">Move Operation</param>
        /// <returns>Quantity moved.</returns>
        public virtual int TryTakeFrom(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (!CanTakeFrom(sourceSlot, EnumMergePriority.AutoMerge) || !sourceSlot.CanTake() || sourceSlot.Itemstack == null) return 0;

            if (!this.Inventory.CanContain(this, sourceSlot)) return 0;

            if (this.itemstack == null)
            {
                int quant = Math.Min(this.GetRemainingSlotSpace(sourceSlot.Itemstack), op.RequestedQuantity);
                if (quant > 0)
                {
                    this.itemstack = sourceSlot.TakeOut(quant);
                    op.MovedQuantity = (op.MovableQuantity = Math.Min(this.StackSize, quant));
                    this.OnItemSlotModified(this.itemstack);
                    sourceSlot.OnItemSlotModified(this.itemstack);
                }
                return op.MovedQuantity;
            }
            ItemStackMergeOperation mergeop = op.ToMergeOperation(this, sourceSlot);
            op = mergeop;
            int origquant = op.RequestedQuantity;
            op.RequestedQuantity = Math.Min(this.GetRemainingSlotSpace(itemstack), op.RequestedQuantity);
            TryMergeStacks(mergeop);
            if (mergeop.MovedQuantity > 0)
            {
                this.OnItemSlotModified(this.itemstack);
                sourceSlot.OnItemSlotModified(itemstack);
            }
            op.RequestedQuantity = origquant;
            return mergeop.MovedQuantity;
        }

        /// <summary>
        /// Attempts to TAKE items from sourceSlot into our slot
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="sourceSlot">Source of items</param>
        /// <param name="quantity">Amount requested</param>
        /// <returns>Quantity Moved</returns>
        public virtual int TryTakeFrom(IWorldAccessor world, ItemSlot sourceSlot, int quantity = 1)
        {
            ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, quantity);
            return TryTakeFrom(sourceSlot, ref op);
        }

        /// <summary>
        /// Attempts to push items from this slot into the sinkSlot.
        /// </summary>
        /// <param name="sinkSlot">Slot to move to</param>
        /// <param name="op">Operation holding move details.</param>
        /// <returns>Quantity moved.</returns>
        public override int TryPutInto(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
        {
            if (!sinkSlot.CanTakeFrom(this, EnumMergePriority.AutoMerge) || !this.CanTake() || this.itemstack == null)
            {
                return 0;
            }
            InventoryBase inventoryBase = sinkSlot.Inventory;
            if (inventoryBase != null && !inventoryBase.CanContain(sinkSlot, this))
            {
                return 0;
            }
            if (sinkSlot.Itemstack == null)
            {
                int q = Math.Min(sinkSlot.GetRemainingSlotSpace(this.itemstack), op.RequestedQuantity);
                if (q > 0)
                {
                    sinkSlot.Itemstack = this.TakeOut(q);
                    op.MovedQuantity = (op.MovableQuantity = Math.Min(sinkSlot.StackSize, q));
                    sinkSlot.OnItemSlotModified(sinkSlot.Itemstack);
                    this.OnItemSlotModified(sinkSlot.Itemstack);
                }
                return op.MovedQuantity;
            }
            ItemStackMergeOperation mergeop = op.ToMergeOperation(sinkSlot, this);
            op = mergeop;
            int origRequestedQuantity = op.RequestedQuantity;
            op.RequestedQuantity = Math.Min(sinkSlot.GetRemainingSlotSpace(this.itemstack), op.RequestedQuantity);
            
            TryMergeStacks(mergeop); // <-- custom call

            if (mergeop.MovedQuantity > 0)
            {
                sinkSlot.OnItemSlotModified(sinkSlot.Itemstack);
                this.OnItemSlotModified(sinkSlot.Itemstack);
            }
            op.RequestedQuantity = origRequestedQuantity;
            return mergeop.MovedQuantity;
        }

        public override int GetRemainingSlotSpace(ItemStack forItemstack)
        {
            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(forItemstack);
            if (props != null)
            {
                if (MaxSlotStackSize != (int)(CapacityLitres * props.ItemsPerLitre))
                { 
                    this.MaxSlotStackSize = (int)((CapacityLitres * props.ItemsPerLitre)); 
                }
                return MaxSlotStackSize - this.StackSize;
            }
            return MaxSlotStackSize;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            if (this.Inventory != null && Inventory.PutLocked) return false;

            ItemStack source = sourceSlot.Itemstack;
            return source != null 
                && ((source.Collectible.GetStorageFlags(source) & this.StorageType) > (EnumItemStorageFlags)0
                    && (this.itemstack == null || GetMergableQuantity(this.itemstack, source, priority) > 0))
                && GetRemainingSlotSpace(source) > 0;
        }

        public virtual int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (sinkStack.Collectible.Equals(sourceStack, sinkStack, GlobalConstants.IgnoredStackAttributes) 
                && sinkStack.StackSize < MaxSlotStackSize)
            {
                return Math.Min(MaxSlotStackSize - sinkStack.StackSize, sourceStack.StackSize);
            }
            return 0;
        }

        public virtual void TryMergeStacks(ItemStackMergeOperation op)
        {
            // will ignore collectable MaxStackSize and rely on the slots MaxStackSize instead
            op.MovableQuantity = this.GetMergableQuantity(op.SinkSlot.Itemstack, op.SourceSlot.Itemstack, op.CurrentPriority);
            CollectibleObject sinkobj = op.SinkSlot.Itemstack.Collectible;
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
                foreach (TransitionState sourceState2 in sourceTransitionStates)
                {
                    TransitionState targetState2 = targetStatesByType[sourceState2.Props.Type];
                    sinkobj.SetTransitionState(op.SinkSlot.Itemstack, sourceState2.Props.Type, sourceState2.TransitionedHours * t + targetState2.TransitionedHours * (1f - t));
                }
            }
            op.SinkSlot.Itemstack.StackSize += op.MovedQuantity;
            op.SourceSlot.Itemstack.StackSize -= op.MovedQuantity;
            if (op.SourceSlot.Itemstack.StackSize <= 0)
            {
                op.SourceSlot.Itemstack = null;
            }
        }
    }
}

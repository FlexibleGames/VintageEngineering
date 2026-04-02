using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.API
{
    public static class VEHelpers
    {
        /// <summary>
        /// A quick check to determine if a chunk at a given position is loaded.<br/>
        /// Unlike the base-game call, this one ignores neighboring chunks.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="atpos">BlockPos to check.</param>
        /// <returns>True if chuck is loaded.</returns>
        public static bool IsChunkLoaded(IWorldAccessor world, BlockPos atpos)
        {
            if (world.BlockAccessor.GetChunk(atpos.X / GlobalConstants.ChunkSize,
                atpos.InternalY / GlobalConstants.ChunkSize,
                atpos.Z / GlobalConstants.ChunkSize) == null)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// Checks if all chunks from given position around at a given radius are loaded.<br/>
        /// If any of them are not loaded it will return false;
        /// </summary>
        /// <param name="world"></param>
        /// <param name="atpos"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public static bool IsChunkLoadedRadius(IWorldAccessor world, BlockPos atpos, int radius = 1)
        {
            if (IsChunkLoaded(world, atpos))
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos tocheck = atpos.AddCopy(x * GlobalConstants.ChunkSize, 0, z * GlobalConstants.ChunkSize);
                        if (!IsChunkLoaded(world, tocheck)) return false;
                    }
                }
            }
            else return false;

            return true;
        }

        public static int SplitStackAndPerformAction(ICoreAPI l_api, Entity byEntity, ItemSlot slot, System.Func<ItemStack, int> action)
        {
            if (slot.Itemstack == null)
            {
                return 0;
            }
            if (slot.Itemstack.StackSize == 1)
            {
                int num = action(slot.Itemstack);
                if (num > 0)
                {
                    int maxStackSize = slot.Itemstack.Collectible.MaxStackSize;
                    EntityPlayer entityPlayer = byEntity as EntityPlayer;
                    if (entityPlayer == null)
                    {
                        return num;
                    }
                    entityPlayer.WalkInventory(delegate (ItemSlot pslot)
                    {
                        if (pslot.Empty || pslot is ItemSlotCreative || pslot.StackSize == pslot.Itemstack.Collectible.MaxStackSize)
                        {
                            return true;
                        }
                        int mergableq = slot.Itemstack.Collectible.GetMergableQuantity(slot.Itemstack, pslot.Itemstack, EnumMergePriority.DirectMerge);
                        if (mergableq == 0)
                        {
                            return true;
                        }
                        BlockLiquidContainerBase blockLiquidContainerBase = slot.Itemstack.Collectible as BlockLiquidContainerBase;
                        BlockLiquidContainerBase invLiqBlock = pslot.Itemstack.Collectible as BlockLiquidContainerBase;
                        int? num3;
                        if (blockLiquidContainerBase == null)
                        {
                            num3 = null;
                        }
                        else
                        {
                            ItemStack content = blockLiquidContainerBase.GetContent(slot.Itemstack);
                            num3 = ((content != null) ? new int?(content.StackSize) : null);
                        }
                        int? num4 = num3;
                        int valueOrDefault = num4.GetValueOrDefault();
                        int? num5;
                        if (invLiqBlock == null)
                        {
                            num5 = null;
                        }
                        else
                        {
                            ItemStack content2 = invLiqBlock.GetContent(pslot.Itemstack);
                            num5 = ((content2 != null) ? new int?(content2.StackSize) : null);
                        }
                        num4 = num5;
                        if (valueOrDefault != num4.GetValueOrDefault())
                        {
                            return true;
                        }
                        slot.Itemstack.StackSize += mergableq;
                        pslot.TakeOut(mergableq);
                        slot.MarkDirty();
                        pslot.MarkDirty();
                        return true;
                    });
                }
                return num;
            }
            ItemStack containerStack = slot.Itemstack.Clone();
            containerStack.StackSize = 1;
            int num2 = action(containerStack);
            if (num2 > 0)
            {
                slot.TakeOut(1);
                EntityPlayer entityPlayer2 = byEntity as EntityPlayer;
                if (entityPlayer2 == null || !entityPlayer2.Player.InventoryManager.TryGiveItemstack(containerStack, true))
                {
                    l_api.World.SpawnItemEntity(containerStack, byEntity.SidedPos.XYZ, null);
                }
                slot.MarkDirty();
            }
            return num2;
        }
    }
}

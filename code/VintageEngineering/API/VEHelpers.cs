using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.API
{
    public static class VEHelpers
    {
        public static class MBStandardColors
        {
            // 1 fence - black
            // 2 platform - mid gray
            // 3 ladder - white
            // 4 heavyeng - dark purple
            // 5 concrete - cyan
            // 6 lighteng - pink
            // 8 fluid - yellow
            // 9 wood slab - orange
            // 10 power - lime
            // 11 interaction - red
            // 12 item io - green
            public static int SteelFence => ColorUtil.ColorFromRgba(30, 30, 30, 180);
            public static int Platform => ColorUtil.ColorFromRgba(128, 128, 128, 180);
            public static int SteelLadder => ColorUtil.ColorFromRgba(250, 250, 250, 180);
            public static int ReinforcedConcrete => ColorUtil.ColorFromRgba(14, 125, 123, 180);
            public static int IronSheetMetal => ColorUtil.ColorFromRgba(10, 10, 250, 180);
            public static int HeavyEng => ColorUtil.ColorFromRgba(54, 6, 91, 180);
            public static int LightEng => ColorUtil.ColorFromRgba(153, 102, 192, 180);
            public static int FluidIO => ColorUtil.ColorFromRgba(215, 215, 0, 180);
            public static int ItemIO => ColorUtil.ColorFromRgba(5, 180, 5, 180);
            public static int PowerIO => ColorUtil.ColorFromRgba(37, 230, 0, 180);
            public static int Interaction => ColorUtil.ColorFromRgba(250, 0, 0, 180);
            public static int TreatedSlabB => ColorUtil.ColorFromRgba(81, 41, 26, 180);
        }

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
        /// If any of them are not loaded it will return false<br/>
        /// Note: Radius is in Chunks not Blocks.
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
                    l_api.World.SpawnItemEntity(containerStack, byEntity.Pos.XYZ, null);
                }
                slot.MarkDirty();
            }
            return num2;
        }

        public static void DoLiquidMovedEffects(ICoreAPI l_api, IPlayer player, ItemStack contentStack, int moved, BlockLiquidContainerBase.EnumLiquidDirection dir)
        {
            if (player == null)
            {
                return;
            }
            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(contentStack);
            float litresMoved = (float)moved / ((props != null) ? props.ItemsPerLitre : 1f);
            IClientPlayer clientPlayer = player as IClientPlayer;
            if (clientPlayer != null)
            {
                clientPlayer.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            }
            l_api.World.PlaySoundAt((dir == BlockLiquidContainerBase.EnumLiquidDirection.Fill) ? (((props != null) ? props.FillSound : null) ?? "sounds/effect/water-fill.ogg") : (((props != null) ? props.PourSound : null) ?? "sounds/effect/water-pour.ogg"), player.Entity, player, true, 16f, GameMath.Clamp(litresMoved / 5f, 0.35f, 1f));
            l_api.World.SpawnCubeParticles(player.Entity.Pos.AheadCopy(0.25).XYZ.Add(0.0, (double)(player.Entity.SelectionBox.Y2 / 2f), 0.0), contentStack, 0.75f, (int)litresMoved * 2, 0.45f, null, null);
        }
    }
}

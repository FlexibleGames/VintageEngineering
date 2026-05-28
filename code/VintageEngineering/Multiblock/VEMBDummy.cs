using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Multiblock
{
    /// <summary>
    /// Dummy Block for VE Multiblock System
    /// </summary>
    public class VEMBDummy : Block, IMultiblockOffset
    {
        public int IOType { get; internal set; }
        public int RotationY
        {
            get
            {                
                switch (Variant["side"])
                {
                    case "east": return 270;
                    case "south": return 180;
                    case "west": return 90;
                    default: return 0;
                }
            }
        }
        public override void OnLoaded(ICoreAPI api)
        {
            // does not contain any LOCATION SPECIFIC information!!
            // ONLY global values that do not change for all Blocks of this type.
            // Relies on BlockEntity for all LOCATION SPECIFIC return values.
            // Encodes horizontal block facing for proper offsets and colselboxes.
            base.OnLoaded(api);
            string io = Variant["io"];
            switch (io)
            {
                case "none": break;
                case "item": IOType = (int)(EnumMultiblockIO.ItemInput | EnumMultiblockIO.ItemOutput); break;
                case "fluid": IOType = (int)(EnumMultiblockIO.FluidInput | EnumMultiblockIO.FluidOutput); break;
                case "power": IOType = (int)(EnumMultiblockIO.PowerInput | EnumMultiblockIO.PowerOutput); break;
                case "signal": IOType = (int)(EnumMultiblockIO.SignalInput | EnumMultiblockIO.SignalOutput); break;
                case "gas": IOType = (int)(EnumMultiblockIO.GasInput | EnumMultiblockIO.GasOutput); break;
                case "mechanical": IOType = (int)(EnumMultiblockIO.MechanicalInput | EnumMultiblockIO.MechanicalOutput); break;
                case "interaction": IOType = (int)(EnumMultiblockIO.Interaction); break;
            }
        }

        /// <summary>
        /// From the IMultiblockOffset interface
        /// </summary>
        /// <param name="pos">Position</param>
        /// <returns>Position of Core (Controller) block.</returns>
        public BlockPos GetControlBlockPos(BlockPos pos)
        {
            return GetOffset(pos)?.AsBlockPos.Copy();
        }

        /// <summary>
        /// Gets Multiblock offset at given pos<br/>
        /// Returns 0,0,0 if given pos is not a VEMultiblockEntity
        /// </summary>
        /// <param name="pos">BlockPos to check</param>
        /// <returns>Vec3i or 0,0,0</returns>
        public Vec3i GetOffset(BlockPos pos)
        {
            if (pos == null) return Vec3i.Zero; 
            VEMBEntityDummy dummy = api.World.BlockAccessor.GetBlockEntity<VEMBEntityDummy>(pos);
            return dummy == null ? Vec3i.Zero : dummy.Offset;
        }

        public bool IsValid(BlockPos pos)
        {
            return api.World.BlockAccessor.GetBlockEntity<VEMBEntityDummy>(pos) != null;
        }

        #region BlatentBaseMultiblockCode
        // This section is tweaked to not rely on encoding offset into the block but rather a block entity.
        // While this strategy creates more block entities in a multiblock, it prevents the need of adding
        // thousands of new blocks whos purpose would just encode the offset.
        private void Handle<K>(IBlockAccessor access, int x, int y, int z, BlockMultiblock.BlockCallDelegateInterface<K> onInterface,
            BlockMultiblock.BlockCallDelegateBlock onIsMultiblock, BlockMultiblock.BlockCallDelegateBlock onOtherwise) where K : class
        {
            Block block = access.GetBlock(new BlockPos(x, y, z));
            K blockcast = block as K;
            if (blockcast == null)
            {
                blockcast = (block.GetBehavior(typeof(K), true) as K);
            }
            if (blockcast != null)
            {
                onInterface(blockcast); // interface specific calls
                return;
            }
            if (block is VEMBDummy)
            {
                onIsMultiblock(block); // this particular block
                return;
            }
            onOtherwise(block); // everything else
        }
        private T Handle<T, K>(IBlockAccessor access, int x, int y, int z, BlockMultiblock.BlockCallDelegateInterface<T, K> onInterface,
            BlockMultiblock.BlockCallDelegateBlock<T> onIsMultiblock, BlockMultiblock.BlockCallDelegateBlock<T> onOtherwise) where K : class
        {
            Block block = access.GetBlock(new BlockPos(x, y, z));
            K blockcast = block as K;
            if (blockcast == null)
            {
                blockcast = (block.GetBehavior(typeof(K), true) as K);
            }
            if (blockcast != null)
            {
                return onInterface(blockcast);
            }
            if (block is VEMBDummy)
            {
                return onIsMultiblock(block);
            }
            return onOtherwise(block);
        }

        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
        {
            BlockSelection bsOffseted = blockSel.Clone();
            Vec3i offsetinv = -GetOffset(blockSel.Position);
            bsOffseted.Position.Add(offsetinv);
            Handle<IMultiBlockActivate>(world.BlockAccessor, bsOffseted.Position.X, bsOffseted.Position.InternalY, bsOffseted.Position.Z,
                (inf) => inf.MBActivate(world, caller, bsOffseted, activationArgs, offsetinv),
                (block) => base.Activate(world, caller, bsOffseted, activationArgs),
                (block) => block.Activate(world, caller, bsOffseted, activationArgs));
        }

        public override BlockSounds GetSounds(IBlockAccessor ba, BlockSelection blockSel, ItemStack stack = null)
        {
            Vec3i offsetinv = -GetOffset(blockSel.Position);

            return Handle<BlockSounds, IMultiBlockInteract>(
                ba,
                blockSel.Position.X + offsetinv.X, blockSel.Position.InternalY + offsetinv.Y, blockSel.Position.Z + offsetinv.Z,
                (inf) => inf.MBGetSounds(ba, blockSel, stack, offsetinv),
                (block) => base.GetSounds(ba, blockSel.AddPosCopy(offsetinv), stack),
                (block) => block.GetSounds(ba, blockSel.AddPosCopy(offsetinv), stack)
            );
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor ba, BlockPos pos)
        {
            Vec3i offsetinv = -GetOffset(pos);
            return Handle<Cuboidf[], IMultiBlockColSelBoxes>(
               ba,
               pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
               (inf) => inf.MBGetSelectionBoxes(ba, pos, offsetinv),
               (block) => new Cuboidf[] { Cuboidf.Default() },
               (block) => block.Id == 0 ? new Cuboidf[] { Cuboidf.Default() } : block.GetSelectionBoxes(ba, pos.AddCopy(offsetinv))
           );
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor ba, BlockPos pos)
        {
            Vec3i offsetinv = -GetOffset(pos);
            return Handle<Cuboidf[], IMultiBlockColSelBoxes>(
               ba,
               pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
               (inf) => inf.MBGetCollisionBoxes(ba, pos, offsetinv),
               (block) => new Cuboidf[] { Cuboidf.Default() },
               (block) => block.GetCollisionBoxes(ba, pos.AddCopy(offsetinv))
           );
        }
        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
        {
            Vec3i offsetinv = -GetOffset(pos);
            return Handle<bool, IMultiBlockInteract>(
                world.BlockAccessor,
                pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
                (inf) => inf.MBDoPartialSelection(world, pos, offsetinv),
                (block) => base.DoPartialSelection(world, pos.AddCopy(offsetinv)),
                (block) => block.DoPartialSelection(world, pos.AddCopy(offsetinv))
            );
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            Vec3i offsetinv = -GetOffset(blockSel.Position);
            BlockSelection bsOffseted = blockSel.Clone();
            bsOffseted.Position.Add(offsetinv);

            return Handle<float, IMultiBlockBlockBreaking>(
                  api.World.BlockAccessor,
                  bsOffseted.Position.X, bsOffseted.Position.InternalY, bsOffseted.Position.Z,
                  (inf) => inf.MBOnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter, offsetinv),
                  (block) => base.OnGettingBroken(player, bsOffseted, itemslot, remainingResistance, dt, counter),
                  (block) => {
                      if (api is ICoreClientAPI capi)
                      {
                          capi.World.CloneBlockDamage(blockSel.Position, blockSel.Position.AddCopy(offsetinv));
                      }
                      return block.OnGettingBroken(player, bsOffseted, itemslot, remainingResistance, dt, counter);
                  }
              );
        }
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            Vec3i offsetinv = -GetOffset(pos);
            Block block = world.BlockAccessor.GetBlock(pos.AddCopy(offsetinv));
            if (block.Id == 0)
            {
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
                return;
            }

            var blockInf = block as IMultiBlockBlockBreaking;
            if (blockInf == null) blockInf = block.GetBehavior(typeof(IMultiBlockBlockBreaking), true) as IMultiBlockBlockBreaking;

            if (blockInf != null)
            {
                blockInf.MBOnBlockBroken(world, pos, offsetinv, byPlayer);
                return;
            }

            // Prevent Stack overflow
            if (block is VEMBDummy) return;

            block.OnBlockBroken(world, pos.AddCopy(offsetinv), byPlayer, dropQuantityMultiplier);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Vec3i offsetinv = GetOffset(pos);
            if (offsetinv != null)
            {
                offsetinv = -offsetinv;
                return Handle<ItemStack, IMultiBlockInteract>(
                    world.BlockAccessor,
                    pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
                    (inf) => inf.MBOnPickBlock(world, pos, offsetinv),
                    (block) => base.OnPickBlock(world, pos.AddCopy(offsetinv)),
                    (block) => block.OnPickBlock(world, pos.AddCopy(offsetinv))
                );
            }
            else return base.OnPickBlock(world, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false; // only block if we can't interact via permissions with this block
            }
            if (byPlayer.Entity.Controls.Sneak)
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }
            else
            {
                Vec3i offsetinv = -GetOffset(blockSel.Position);
                BlockSelection bsOffseted = blockSel.Clone();
                bsOffseted.Position.Add(offsetinv);

                return Handle<bool, IMultiBlockInteract>(
                    world.BlockAccessor,
                    bsOffseted.Position.X, bsOffseted.Position.InternalY, bsOffseted.Position.Z,
                    (inf) => inf.MBOnBlockInteractStart(world, byPlayer, blockSel, offsetinv),
                    (block) => base.OnBlockInteractStart(world, byPlayer, bsOffseted),
                    (block) => block.OnBlockInteractStart(world, byPlayer, bsOffseted)
                );
            }
        }        

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.Entity.Controls.Sneak)
            {
                return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
            }
            else
            {
                Vec3i offsetinv = -GetOffset(blockSel.Position);
                BlockSelection bsOffseted = blockSel.Clone();
                bsOffseted.Position.Add(offsetinv);

                return Handle<bool, IMultiBlockInteract>(
                    world.BlockAccessor,
                    bsOffseted.Position.X, bsOffseted.Position.InternalY, bsOffseted.Position.Z,
                    (inf) => inf.MBOnBlockInteractStep(secondsUsed, world, byPlayer, blockSel, offsetinv),
                    (block) => base.OnBlockInteractStep(secondsUsed, world, byPlayer, bsOffseted),
                    (block) => block.OnBlockInteractStep(secondsUsed, world, byPlayer, bsOffseted)
                );
            }
        }
        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.Entity.Controls.Sneak)
            {
                base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
            }
            else
            {
                Vec3i offsetinv = -GetOffset(blockSel.Position);
                BlockSelection bsOffseted = blockSel.Clone();
                bsOffseted.Position.Add(offsetinv);

                Handle<IMultiBlockInteract>(
                    world.BlockAccessor,
                    bsOffseted.Position.X, bsOffseted.Position.InternalY, bsOffseted.Position.Z,
                    (inf) => inf.MBOnBlockInteractStop(secondsUsed, world, byPlayer, blockSel, offsetinv),
                    (block) => base.OnBlockInteractStop(secondsUsed, world, byPlayer, bsOffseted),
                    (block) => block.OnBlockInteractStop(secondsUsed, world, byPlayer, bsOffseted)
                );
            }
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            Vec3i offsetinv = -GetOffset(blockSel.Position);
            BlockSelection bsOffseted = blockSel.Clone();
            bsOffseted.Position.Add(offsetinv);

            return Handle<bool, IMultiBlockInteract>(
                world.BlockAccessor,
                bsOffseted.Position.X, bsOffseted.Position.InternalY, bsOffseted.Position.Z,
                (inf) => inf.MBOnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason, offsetinv),
                (block) => base.OnBlockInteractCancel(secondsUsed, world, byPlayer, bsOffseted, cancelReason),
                (block) => block.OnBlockInteractCancel(secondsUsed, world, byPlayer, bsOffseted, cancelReason)
            );
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer)
        {
            Vec3i offsetinv = -GetOffset(blockSel.Position);
            BlockSelection bsOffseted = blockSel.Clone();
            bsOffseted.Position.Add(offsetinv);

            return Handle<WorldInteraction[], IMultiBlockInteract>(
                world.BlockAccessor,
                bsOffseted.Position.X, bsOffseted.Position.InternalY, bsOffseted.Position.Z,
                (inf) => inf.MBGetPlacedBlockInteractionHelp(world, blockSel, forPlayer, offsetinv),
                (block) => base.GetPlacedBlockInteractionHelp(world, bsOffseted, forPlayer),
                (block) => block.GetPlacedBlockInteractionHelp(world, bsOffseted, forPlayer)
            );
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            if (!IsValid(pos)) return base.GetPlacedBlockInfo(world, pos, forPlayer);

            Vec3i offsetinv = -GetOffset(pos);
            BlockPos mainPos = pos.AddCopy(offsetinv);
            Block block = world.BlockAccessor.GetBlock(mainPos);

            // Prevent Stack overflow
            if (block is VEMBDummy) return "ERROR";

            return block.GetPlacedBlockInfo(world, mainPos, forPlayer);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            Vec3i offsetinv = -GetOffset(pos);
            var world = capi.World;
            return Handle<int, IMultiBlockBlockBreaking>(
                world.BlockAccessor,
                pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
                (inf) => inf.MBGetRandomColor(capi, pos, facing, rndIndex, offsetinv),
                (block) => base.GetRandomColor(capi, pos, facing, rndIndex),
                (block) => block.GetRandomColor(capi, pos, facing, rndIndex)
            );
        }

        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
        {
            Vec3i offsetinv = -GetOffset(pos);
            var world = capi.World;
            return Handle<int, IMultiBlockBlockBreaking>(
                world.BlockAccessor,
                pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
                (inf) => inf.MBGetColorWithoutTint(capi, pos, offsetinv),
                (block) => base.GetColorWithoutTint(capi, pos),
                (block) => block.GetColorWithoutTint(capi, pos)
            );
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            Vec3i offsetinv = -GetOffset(pos);
            return Handle<bool, IMultiBlockBlockProperties>(
                blockAccessor,
                pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
                (inf) => inf.MBCanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea, offsetinv),
                (nblock) => base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea),
                (nblock) => nblock.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea)
            );
        }


        public override JsonObject GetAttributes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Vec3i offsetinv = -GetOffset(pos);
            return Handle<JsonObject, IMultiBlockBlockProperties>(
                blockAccessor,
                pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
                (inf) => inf.MBGetAttributes(blockAccessor, pos),
                (block) => base.GetAttributes(blockAccessor, pos),
                (block) => block.GetAttributes(blockAccessor, pos)
            );
        }

        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
        {
            Vec3i offsetinv = -GetOffset(pos);
            IBlockAccessor blockAccessor = api.World.BlockAccessor;

            return Handle<int, IMultiBlockBlockProperties>(
                blockAccessor,
                pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
                (inf) => inf.MBGetRetention(pos, facing, type, offsetinv),
                (block) => base.GetRetention(pos, facing, EnumRetentionType.Heat),
                (block) => block.GetRetention(pos, facing, EnumRetentionType.Heat)
            );
        }

        public override float GetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos)
        {
            Vec3i offsetinv = -GetOffset(pos);
            IBlockAccessor blockAccessor = api.World.BlockAccessor;

            return Handle<float, IMultiBlockBlockProperties>(
                blockAccessor,
                pos.X + offsetinv.X, pos.InternalY + offsetinv.Y, pos.Z + offsetinv.Z,
                (inf) => inf.MBGetLiquidBarrierHeightOnSide(face, pos, offsetinv),
                (block) => base.GetLiquidBarrierHeightOnSide(face, pos),
                (block) => block.GetLiquidBarrierHeightOnSide(face, pos)
            );
        }

        public override T GetBlockEntity<T>(BlockPos position)
        {
            Vec3i offsetinv = -GetOffset(position);
            Block block = api.World.BlockAccessor.GetBlock(position.AddCopy(offsetinv));

            // Prevent endless recursion stack overflow, should we ever end up in a corrupted world situation
            if (block is VEMBDummy) return base.GetBlockEntity<T>(position);

            return block.GetBlockEntity<T>(position.AddCopy(offsetinv));
        }

        public override T GetBlockEntity<T>(BlockSelection blockSel)
        {
            Vec3i offsetinv = -GetOffset(blockSel.Position);
            Block block = api.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(offsetinv));

            // Prevent endless recursion stack overflow, should we ever end up in a corrupted world situation
            if (block is VEMBDummy) return base.GetBlockEntity<T>(blockSel);

            BlockSelection bs = blockSel.Clone();
            bs.Position.Add(offsetinv);

            return block.GetBlockEntity<T>(bs);
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {            
            int angleIndex = ((angle / 90) % 4 + 4) % 4;
            if (angleIndex == 0) return Code;

            string rotatedcode = "north";
            switch (angleIndex)
            {
                case 1:
                    rotatedcode = "west";
                    break;
                case 2:
                    rotatedcode = "south";
                    break;
                case 3:
                    rotatedcode = "east";
                    break;
                default:                    
                    break;
            }

            return new AssetLocation(Code.Domain, "vembdummy-" + Variant["io"] + "-" + rotatedcode);
        }

        public override T GetInterface<T>(IWorldAccessor world, BlockPos pos)
        {
            if (pos == null || world?.BlockAccessor == null) return base.GetInterface<T>(world, pos);
            Vec3i offsetinv = -GetOffset(pos);

            T blockt = this as T;
            if (blockt != null)
            {
                return blockt;
            }

            Block block = world.BlockAccessor.GetBlock(pos.AddCopy(offsetinv));
            if (block is VEMBDummy) return base.GetInterface<T>(world, pos); // prevent infinite loop

            return block.GetInterface<T>(world, pos.AddCopy(offsetinv));
        }

        #endregion

    }
}

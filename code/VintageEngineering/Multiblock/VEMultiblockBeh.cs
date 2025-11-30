using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Multiblock
{
    public class VEMultiblockBeh : BlockBehavior, IMultiBlockActivate, IMultiBlockBlockBreaking, IMultiBlockBlockProperties, IMultiBlockColSelBoxes, IMultiBlockInteract
    {
        public VEMultiblockStructure mbs = new VEMultiblockStructure();        

        /// <summary>
        /// What is the Y Rotation for this instance given the block placement orientation?
        /// </summary>
        public int RotateY { 
            get
            {                
                switch (base.block.Variant["side"])
                {
                    case "east": return 270;
                    case "south": return 180;
                    case "west": return 90;
                    default: return 0;
                }
            } 
        }

        public VEMultiblockBeh(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            mbs = properties["multiblockStructure"]?.AsObject<VEMultiblockStructure>();
            mbs.HighlightSlotID = properties["highlightID"].AsInt(23);
            mbs.InitHighlightColors(properties["blockHighlightColors"]);
            mbs?.InitForUse(RotateY);

        }

        public bool TriggerValidation(IWorldAccessor world, IPlayer byPlayer, BlockSelection sel, BlockPos core)
        {
            VEMBEntityCore mbcore = world.BlockAccessor.GetBlockEntity<VEMBEntityCore>(core);
            if (mbcore != null)
            {
                int layer = mbcore.activelayer;
                
                int missing = mbs.InCompleteBlockCount(world, core, null, layer);
                if (missing > 0)
                { 
                    if (world.Side == EnumAppSide.Client)
                    { 
                        mbs.HighlightIncompleteParts(world, byPlayer, core, layer); 
                        // TODO: Trigger client error
                    }
                }
                else
                {
                    if (layer == mbs.MaxY)
                    {
                        // holy cow, it's built! 
                        if (world.Side == EnumAppSide.Client) mbs.ClearHighlights(world, byPlayer);
                    }
                    else
                    {
                        // next layer please
                        layer++;
                        mbcore.activelayer = layer;
                        TriggerValidation(world, byPlayer, sel, core);
                    }
                }
            }
            return true;
        }

        #region IMultiblockActivate
        /// <summary>
        /// When a Command Block, console command or (perhaps in future) non-player entity wants to activate this placed block
        /// </summary>
        /// <param name="world"></param>
        /// <param name="caller"></param>
        /// <param name="blockSel"></param>
        /// <param name="args"></param>
        /// <param name="offset"></param>
        public void MBActivate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute args, Vec3i offset)
        {
            // "offset" encodes which block was selected for this trigger, offset from this core
            base.block.Activate(world, caller, blockSel, args);
            // for a future where autoclickers are a thing
        }
        #endregion

        #region IMultiBlockBlockBreaking
        public void MBOnBlockBroken(IWorldAccessor world, BlockPos pos, Vec3i offset, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // this is going to be a huge PITA
            throw new NotImplementedException();
        }

        public int MBGetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex, Vec3i offsetInv)
        {
            return base.block.GetRandomColor(capi, pos, facing, rndIndex);
        }

        public int MBGetColorWithoutTint(ICoreClientAPI capi, BlockPos pos, Vec3i offsetInv)
        {
            return base.block.GetColorWithoutTint(capi, pos);
        }

        public float MBOnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter, Vec3i offsetInv)
        {
            BlockSelection bs = blockSel.Clone();
            bs.Position = bs.Position.AddCopy(offsetInv);
            return base.block.OnGettingBroken(player, bs, itemslot, remainingResistance, dt, counter);
        }

        #endregion

        #region IMultiBlockBlockProperties
        public bool MBCanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea, Vec3i offsetInv)
        {
            //BlockPos bs = pos.AddCopy(offsetInv);
            return base.block.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
        }

        public float MBGetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos, Vec3i offsetInv)
        {
            BlockPos bs = pos.AddCopy(offsetInv);
            return base.block.GetLiquidBarrierHeightOnSide(face, bs);
        }

        public int MBGetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type, Vec3i offsetInv)
        {
            BlockPos bs = pos.AddCopy(offsetInv);
            return base.block.GetRetention(bs, facing, type);
        }

        public JsonObject MBGetAttributes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return base.block.Attributes;
        }

        #endregion

        #region IMultiBlockColSelBoxes
        public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return [Cuboidf.Default()];
        }

        public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return [Cuboidf.Default()];
        }
        #endregion

        #region IMultiBlockInteract
        public bool MBDoParticalSelection(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            BlockPos bs = pos.AddCopy(offset);
            return base.block.DoParticalSelection(world, bs);
        }

        public bool MBOnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            BlockSelection bs = blockSel.Clone();
            bs.Position = bs.Position.AddCopy(offset);
            return base.block.OnBlockInteractStart(world, byPlayer, bs);
        }

        public bool MBOnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            BlockSelection bs = blockSel.Clone();
            bs.Position = bs.Position.AddCopy(offset);
            return base.block.OnBlockInteractStep(secondsUsed, world, byPlayer, bs);
        }

        public void MBOnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            BlockSelection bs = blockSel.Clone();
            bs.Position = bs.Position.AddCopy(offset);
            base.block.OnBlockInteractStep(secondsUsed, world, byPlayer, bs);
        }

        public bool MBOnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason, Vec3i offset)
        {
            BlockSelection bs = blockSel.Clone();
            bs.Position = bs.Position.AddCopy(offset);
            return base.block.OnBlockInteractCancel(secondsUsed, world, byPlayer, bs, cancelReason);
        }

        public ItemStack MBOnPickBlock(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            BlockPos bs = pos.AddCopy(offset);
            return base.block.OnPickBlock(world, bs);
        }

        public WorldInteraction[] MBGetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer, Vec3i offset)
        {
            BlockSelection bs = blockSel.Clone();
            bs.Position = bs.Position.AddCopy(offset);
            return base.block.GetPlacedBlockInteractionHelp(world, bs, forPlayer);
        }

        public BlockSounds MBGetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack stack, Vec3i offset)
        {
            BlockSelection bs = blockSel.Clone();
            bs.Position = bs.Position.AddCopy(offset);
            return base.block.GetSounds(blockAccessor, bs, stack);
        }
        #endregion
    }
}

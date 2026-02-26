using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Multiblock
{
    public class MBBEInteractable : BlockEntity
    {
        private BlockPos corePosition;
        /// <summary>
        /// What is the BlockPos of the Core linked to this Interactable Block? Null if not yet set.
        /// </summary>
        public BlockPos CorePosition => corePosition;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (corePosition == null)
            {
                FindMBCore(api.World);
            }
        }

        public void FindMBCore(IWorldAccessor world)
        {
            // Check neighbors to start the search...
            int limit = 1000;

            List<BlockPos> blocksChecked = new List<BlockPos>();
            List<BlockPos> blocksToCheck = new List<BlockPos>();

            blocksChecked.Add(this.Pos);
            blocksToCheck.Add(this.Pos);

            bool found = false;

            while (blocksToCheck.Count > 0)
            {
                List<BlockPos> blockstoadd = new List<BlockPos>();

                foreach (BlockPos pos in blocksToCheck)
                {
                    BlockPos start = pos.AddCopy(-1, -1, -1);
                    BlockPos end = pos.AddCopy(1, 1, 1);
                    world.BlockAccessor.WalkBlocks(start, end, delegate (Block dblock, int x, int y, int z)
                    {
                        string blockcode = dblock.Code.Path;
                        if (dblock.BlockId != 0 &&                         
                            !blockcode.Contains("rock") &&
                            !blockcode.Contains("gravel") &&
                            !blockcode.Contains("sand") &&
                            !blockcode.Contains("ore") &&
                            !blockcode.Contains("stone") &&
                            !(dblock.BlockMaterial == EnumBlockMaterial.Soil) &&
                            !dblock.IsLiquid()
                            ) // block is NOT air
                        {
                            BlockPos bcheck = new BlockPos(x, y, z, this.Pos.dimension);
                            if (!blocksChecked.Contains(bcheck))
                            {
                                if (world.BlockAccessor.GetBlock(bcheck) is VEMBCore core)
                                {
                                    if (core.Variant["state"] == "incomplete")
                                    {
                                        // WE FOUND IT!
                                        corePosition = bcheck.Copy();
                                        //blocksToCheck.Clear();
                                        found = true;
                                        blockstoadd.Clear();
                                        //blocksChecked.Clear();
                                        return;
                                    }
                                }
                                else
                                {
                                    blockstoadd.Add(bcheck);
                                    blocksChecked.Add(bcheck);
                                }
                            }
                        }
                    }, false);
                    if (found) break;
                }
                blocksToCheck.Clear();
                 if (blockstoadd.Count > 0 && blocksChecked.Count < limit) { blocksToCheck.AddRange(blockstoadd); }
                blockstoadd.Clear();
            }
            blocksChecked.Clear();
        }        

        public bool TriggerValidation(IWorldAccessor world, IPlayer byPlayer, BlockSelection sel)
        {
            if (corePosition == null) FindMBCore(world);
            if (corePosition != null)
            {
                VEMultiblockBeh beh = world.BlockAccessor.GetBlock(corePosition)?.GetBehavior<VEMultiblockBeh>();
                if (beh != null)
                {
                    beh.TriggerValidation(world, byPlayer, sel, corePosition);
                }
            }
            return true;
        }

        internal void SetMBCore(BlockPos core)
        {
            corePosition = core.Copy();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            corePosition = tree.GetBlockPos("corepos");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (corePosition != null) tree.SetBlockPos("corepos", corePosition);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Transport.API;
using VintageEngineering.Transport.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageEngineering.Transport
{
    public abstract class BlockPipeBase : Block
    {
        protected ICoreClientAPI capi;
        protected ICoreServerAPI sapi;
        protected EnumPipeUse _pipeUse;

        /// <summary>
        /// What type of pipe is this? Item, Fluid, Gas, etc, etc.
        /// </summary>
        public EnumPipeUse PipeUse
        { get { return _pipeUse; } }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
            }
            else
            {
                capi = api as ICoreClientAPI;
            }
            _pipeUse = Enum.Parse<EnumPipeUse>(this.LastCodePart());
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            // TODO Redetect any potential connections as something changed.
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            // Detect Connections and adjust shape accordingly. This is done in the BE.
            BEPipeBase pipebe = api.World.BlockAccessor.GetBlockEntity(blockPos) as BEPipeBase;
            if (pipebe != null)
            {
                pipebe.MarkPipeDirty(world); // this builds connection information
                PipeNetworkManager pnm = api.ModLoader.GetModSystem<PipeNetworkManager>(true);
                if (pnm != null)
                {
                    pnm.OnPipeBlockPlaced(world, blockPos);
                }
            }
            base.OnBlockPlaced(world, blockPos, byItemStack);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            // Returns an array of all selection boxes that are interactable for this block.
            // Build the array based on number of connections this block has, always in the order
            // N, E, S, W, U, D, Base; where Base is the core pipe object.
            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // TODO: detect a wrench and handle, or open GUI if needed.
            BEPipeBase pipe = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEPipeBase;

            if (pipe != null)
            {
                return pipe.OnPlayerRightClick(world, byPlayer, blockSel);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BEPipeBase pipebe = api.World.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
            if (pipebe != null)
            {
                //pipebe.MarkPipeDirty(world); // this builds connection information
                PipeNetworkManager pnm = api.ModLoader.GetModSystem<PipeNetworkManager>(true);
                if (pnm != null)
                {
                    pnm.OnPipeBlockBroken(world, pos);
                }
            }
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // TODO Get any extraction node inventory drops. 
            // Don't forget the base pipe block.
            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        /// <summary>
        /// Converts a BlockSelection object into a BlockFacing direction based on the pipes active connections
        /// and index of the selection.<br/>
        /// Returns NULL if the center core BASE object was the object interacted with.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="blockSelection">BlockSelection object</param>
        /// <returns>BlockFacing object or NULL if Core Interaction</returns>
        public BlockFacing ConvertSelectionToDirection(IWorldAccessor world, BlockSelection blockSelection)
        {
            // BlockSelection includes the SelectionIndex which is the index of the selection box interacted
            // with as returned by GetSelectionBoxes(..) above. Need to convert that index into the actual direction
            // player interacted with as any index could be any direction.            
            switch (blockSelection.SelectionBoxIndex)
            {
                case 0: return BlockFacing.NORTH; 
                case 1: return BlockFacing.EAST;
                case 2: return BlockFacing.SOUTH;
                case 3: return BlockFacing.WEST;
                case 4: return BlockFacing.UP;
                case 5: return BlockFacing.DOWN;
                case 6: return null;
                default: return null;
            }
        }

    }
}

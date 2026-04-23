using System;
using System.Text;
using VintageEngineering.Transport.API;
using VintageEngineering.Transport.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public class BlockPipeBaseNew : Block, IWrenchOrientable
    {
        protected ICoreClientAPI capi;
        protected ICoreServerAPI sapi;
        protected EnumPipeUse _pipeUse;
        private bool _firstEvent = true;

        /// <summary>
        /// What type of pipe is this? item, fluid, gas, etc, etc.<br/>
        /// Created by parsing the last code part of the block, i.e. pipe-item vs pipe-fluid.
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
                //_capi.Input.InWorldAction += InputWorldAction;
            }
            _pipeUse = Enum.Parse<EnumPipeUse>(this.LastCodePart());
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BEPipeBaseNew bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBaseNew;
            if (bep != null)
            {
                StringBuilder sb = new StringBuilder();
                bep.GetBlockInfo(forPlayer, sb);
                return sb.ToString().TrimEnd();
            }
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            // Redetect any potential connections as something changed.
            BEPipeBaseNew pipebe = api.World.BlockAccessor.GetBlockEntity(pos) as BEPipeBaseNew;
            if (pipebe != null)
            {
                pipebe.PipeNeighborChanged(world, pos, neibpos);
            }
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack); // this actually spawns the BE

            // Detect Connections and adjust shape accordingly. This is done in the BE.
            BEPipeBaseNew pipebe = api.World.BlockAccessor.GetBlockEntity(blockPos) as BEPipeBaseNew;
            if (pipebe != null)
            {
                pipebe.NewPipePlaced(world, true); // this builds connection information                
            }
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
            if (byPlayer == null) return true;
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }
            if (!byPlayer.InventoryManager.ActiveHotbarSlot.Empty &&
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible is BlockPipeBaseNew)
            {
                // pipe in hand, build on top of targeted block
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            BEPipeBaseNew pipe = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEPipeBaseNew;
            if (pipe != null && _firstEvent)
            {
                // Pass event to BE pipe base
                pipe.OnPlayerRightClick(world, byPlayer, blockSel);
                _firstEvent = false;
                return true;
            }
            base.OnBlockInteractStart(world, byPlayer, blockSel);

            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api.Side == EnumAppSide.Server) _firstEvent = true;
            //base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            _firstEvent = true;
            return true;
            //return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BEPipeBaseNew pipebe = api.World.BlockAccessor.GetBlockEntity(pos) as BEPipeBaseNew;
            if (pipebe != null)
            {
                pipebe.OnBlockBroken(byPlayer);
            }
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {                        
            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        /// <summary>
        /// Converts a BlockSelection object into a BlockFacing direction based on the pipes active connections
        /// and index of the selection.<br/>
        /// Returns NULL if the _center core BASE object was the object interacted with.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="blockSelection">BlockSelection object</param>
        /// <returns>BlockFacing object or NULL if Core Interaction</returns>
        public BlockFacing ConvertSelectionToDirection(IWorldAccessor world, BlockSelection blockSelection)
        {
            // BlockSelection includes the SelectionIndex which is the index of the selection box interacted
            // with as returned by GetSelectionBoxes(..) above. Need to convert that index into the actual direction
            // player interacted with as any index could be any direction.
            if (blockSelection.SelectionBoxIndex < 6) return BlockFacing.ALLFACES[blockSelection.SelectionBoxIndex];
            else return null;
        }
        /// <summary>
        /// Defined in the IWrenchOrientatable interface, called by the wrench item.
        /// </summary>
        /// <param name="byEntity"></param>
        /// <param name="blockSel"></param>
        /// <param name="dir"></param>
        public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            if (byEntity.Controls.Sneak)
            {
                OnBlockInteractStart(api.World, (byEntity as EntityPlayer).Player, blockSel);
                _firstEvent = true;
            }
        }
    }
}

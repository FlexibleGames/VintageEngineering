using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Blocks
{
    public class BlockCrudeOil : BlockForFluidsLayer, IBlockFlowing
    {
        public string Flow { get; set; }
        public Vec3i FlowNormali { get; set; }
        
        public bool IsLava => false;

        public int Height {  get; set; }

        public override bool ForFluidsLayer => true;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            string f = this.Variant["flow"];
            this.Flow = ((f != null) ? string.Intern(f) : null);
            Vec3i flowNormali;
            if (this.Flow == null)
            {
                flowNormali = null;
            }
            else
            {
                Cardinal cardinal = Cardinal.FromInitial(this.Flow);
                flowNormali = ((cardinal != null) ? cardinal.Normali : null);
            }
            this.FlowNormali = flowNormali;
            string h = this.Variant["height"];
            this.Height = ((h != null) ? h.ToInt(0) : 7);
        }

        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            return base.GetColorWithoutTint(capi, pos);
        }
        public override float GetAmbientSoundStrength(IWorldAccessor world, BlockPos pos)
        {            
            return (float)((world.BlockAccessor.GetBlockId(pos) == 0 && world.BlockAccessor.IsSideSolid(pos.X, pos.Y - 1, pos.Z, BlockFacing.UP)) ? 1 : 0);
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            BlockBehavior[] blockBehaviors = this.BlockBehaviors;
            for (int i = 0; i < blockBehaviors.Length; i++)
            {
                blockBehaviors[i].OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
            }
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            return false;
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            Block oldBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            if (oldBlock.DisplacesLiquids(world.BlockAccessor, blockSel.Position) && !oldBlock.IsReplacableBy(this))
            {
                failureCode = "notreplaceable";
                return false;
            }
            bool result = true;
            if (byPlayer != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                failureCode = "claimed";
                return false;
            }
            bool preventDefault = false;
            foreach (BlockBehavior blockBehavior in this.BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;
                bool behaviorResult = blockBehavior.CanPlaceBlock(world, byPlayer, blockSel, ref handled, ref failureCode);
                if (handled != EnumHandling.PassThrough)
                {
                    result = (result && behaviorResult);
                    preventDefault = true;
                }
                if (handled == EnumHandling.PreventSubsequent)
                {
                    return result;
                }
            }
            return !preventDefault || result;
        }
    }
}

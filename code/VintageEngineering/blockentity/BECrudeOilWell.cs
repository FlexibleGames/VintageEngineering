using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using VintageEngineering.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    public class BECrudeOilWell : BlockEntity, IOilWell
    {
        private ICoreServerAPI sapi;
        private long _fluidportions;        
        public long RemainingPortions => _fluidportions;

        public long MaxPPS => (long)base.Block.Attributes["portionpersecond"].AsDouble(50);

        public string OilBlockCode => base.Block.Attributes["oilblockcode"].AsString("vinteng:crudeoil");

        public string OilPortionCode => base.Block.Attributes["oilportioncode"].AsString("vinteng:crudeoilportion");

        public int TricklePortions => base.Block.Attributes["trickleportions"].AsInt(100);

        public bool CanBeInfinite => base.Block.Attributes["canbeinfinite"].AsBool(true);

        public long MaxDepositBlocks => (long)base.Block.Attributes["maxdepositblocks"].AsDouble(200000);

        public long MinDepositBlocks => (long)base.Block.Attributes["mindepositblocks"].AsDouble(1500);

        /// <summary>
        /// Is this considered a large well, set when this block is generated.
        /// </summary>
        public bool IsLarge = false;
        /// <summary>
        /// Is this deposit generated? <br/>
        /// Generation does not happen when the block is created but after all neighboring chunks are loaded to
        /// ensure all blocks can be accessed
        /// </summary>
        public bool IsGenerated = false;
        /// <summary>
        /// The internal TickHandler ID
        /// </summary>
        private long _tickHandler = 0;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server) 
            {
                sapi = api as ICoreServerAPI;
                if (!IsGenerated) _tickHandler = RegisterGameTickListener(OnGameTick, 3000, 100);
            }
        }
        /// <summary>
        /// Only run server-side, will track and build the deposit when all neighboring chunks are loaded.
        /// </summary>
        /// <param name="dt">Time in seconds since last tick.</param>
        public void OnGameTick(float dt)
        {
            if (IsGenerated)
            {
                if (this.TickHandlers.Count > 0)
                {
                    // remove this ticking event as it is not needed.
                    this.UnregisterGameTickListener(_tickHandler);
                    _tickHandler = 0;
                }
            }
            else
            {
                bool ready = sapi.World.IsFullyLoadedChunk(Pos);
                if (ready)
                {
                    IBulkBlockAccessor bbaccessor = sapi.World.GetBlockAccessorBulkUpdate(true, true, false);
                    bbaccessor.UpdateSnowAccumMap = false;                    
                    (this.Block as BlockCrudeOilWell).BuildOilSpout(bbaccessor, Pos.Copy(), null, sapi.World.Rand as NormalRandom, IsLarge);
                    foreach (KeyValuePair<BlockPos, BlockUpdate> pair in bbaccessor.StagedBlocks)
                    {
                        pair.Value.NewSolidBlockId = 0;// pair.Value.NewFluidBlockId;
                    }                    
                    bbaccessor.Commit();
                    bbaccessor.PostCommitCleanup(bbaccessor.StagedBlocks.Values.ToList<BlockUpdate>());                    
                    IsGenerated = true;
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            // this is called on the client, while the values are on the server, need to push values to client
            Item portion = Api.World.GetItem(new AssetLocation(OilPortionCode));
            int perliter = 100;
            if (portion != null) 
            {
                ItemStack portionstack = new ItemStack(portion);
                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(portionstack);
                if (props != null)
                {
                    perliter = (int)props.ItemsPerLitre;
                }
            }
            if (RemainingPortions > 0) dsc.AppendLine($"{RemainingPortions / perliter}L {Lang.Get("vinteng:gui-word-remaining")}");
            else 
            {
                if (CanBeInfinite) dsc.AppendLine($"{Lang.Get("vinteng:gui-depleted")}, {Lang.Get("vinteng:gui-isinfinite")}");
                else dsc.AppendLine($"{Lang.Get("vinteng:gui-depleted")}");
            }
        }

        public void InitDeposit(bool isLarge, IBlockAccessor access, IRandom wgenrand, Block wellblock, ICoreAPI l_api)
        {
            IsLarge = isLarge;
            long minblocks = MinDepositBlocks;
            if (isLarge) minblocks = (long)(MaxDepositBlocks * 0.8);
            long maxblocks = MaxDepositBlocks;
            if (isLarge) maxblocks *= 2;
            long numblocks = l_api.World.Rand.NextInt64(minblocks, maxblocks+1);
            
            if (numblocks < 0) numblocks = long.MaxValue; // value overrun, make it effectively infinite
            try
            {
                AssetLocation portion = new AssetLocation(OilPortionCode);
                ItemStack portionstack = new ItemStack(l_api.World.GetItem(portion));
                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(portionstack);
                if (props != null)
                {
                    _fluidportions = 1000 * numblocks * (int)props.ItemsPerLitre;
                }
                else
                {
                    _fluidportions = 1000 * numblocks * 100;
                }
                MarkDirty(true);
            }
            catch (Exception ex)
            {
                l_api.Logger.Error(ex);
            }
        }

        public long PumpTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client) return -1;
            long amount = 0;
            if (_fluidportions > 0)
            {
                amount = (long)(MaxPPS * dt);
                if (amount > _fluidportions) amount = _fluidportions;
                _fluidportions -= amount;
            }
            else
            {
                amount = (long)(TricklePortions * dt);
                if (!CanBeInfinite) amount = 0;
            }
            return amount;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            this._fluidportions = tree.GetLong("fluidleft", 25);
            this.IsLarge = tree.GetBool("islarge", false);
            this.IsGenerated = tree.GetBool("isgenerated", true);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetLong("fluidleft", this._fluidportions);
            tree.SetBool("isgenerated", this.IsGenerated);
            tree.SetBool("islarge", this.IsLarge);
        }
    }
}

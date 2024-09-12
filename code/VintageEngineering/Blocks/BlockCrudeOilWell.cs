﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace VintageEngineering.Blocks
{
    public class BlockCrudeOilWell: Block
    {
        private int _maxDepositRadius;
        private int _minDepositRadius;
        private int _minTempForLarge;
        private int _maxTempForLarge;
        private float _minRainForLarge;
        private float _maxRainForLarge;
        private int _extraRadiusLarge;
        private float _oddsForLarge;

        AssetLocation _oilblockloc;
        Block _oilBlock;

        private bool _genDeposits;
        private bool _genSpouts;
        private bool _genPool;

        public BlockCrudeOilWell()
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (Attributes != null)
            {
                _maxDepositRadius = Attributes["maxDepositRadius"].AsInt(8);
                _minDepositRadius = Attributes["minDepositRadius"].AsInt(3);

                _minTempForLarge = Attributes["minTempForLarge"].AsInt(28);
                _maxTempForLarge = Attributes["maxTempForLarge"].AsInt(50);

                _minRainForLarge = Attributes["minRainForLarge"].AsFloat(0f);
                _maxRainForLarge = Attributes["maxRainForLarge"].AsFloat(0.4f);

                _extraRadiusLarge = Attributes["extraRadiusLarge"].AsInt(4);
                _oddsForLarge = Attributes["oddsForLarge"].AsFloat(0.25f);

                _genDeposits = Attributes["depositgen"].AsBool(true);
                _genSpouts = Attributes["spoutgen"].AsBool(true);
                _genPool = Attributes["poolgen"].AsBool(true);

                _oilblockloc = new AssetLocation(Attributes["oilblockcode"].AsString("vinteng:crudeoil-still-7"));
                _oilBlock = api.World.GetBlock(_oilblockloc);
            }
        }
        
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor access, BlockPos pos, BlockFacing face, LCGRandom wrand)
        {
            if (pos.Y > 5) return false;
            if (_oilBlock == null || _oilBlock.Id == 0) return false;
            int surfacey = access.GetTerrainMapheightAt(pos); // surface
            if (Math.Abs(surfacey - TerraGenConfig.seaLevel) > 40) return false;
            BlockPos watercheck1 = new BlockPos(pos.X, surfacey + 1, pos.Z, BlockLayersAccess.Fluid);
            Block waterblock1 = access.GetBlock(watercheck1);
            if (waterblock1.Id != 0 && waterblock1.IsLiquid() && waterblock1.LiquidCode.Contains("water")) return false;

            foreach (BlockFacing bface in BlockFacing.HORIZONTALS)
            {
                BlockPos tocheck = pos.AddCopy(bface, 4);
                tocheck.Y = access.GetTerrainMapheightAt(tocheck) + 1;
                if (Math.Abs(tocheck.Y - surfacey) > 4) return false;
                Block bcheck = access.GetBlock(tocheck);
                if (bcheck.Id != 0 && bcheck.IsLiquid() && bcheck.LiquidCode.Contains("water")) return false;
            }

            bool isLarge = wrand.NextFloat() <= _oddsForLarge; // first check on large deposits
            access.SetBlock(this.BlockId, pos.Copy());
            if (this.EntityClass != null)
            {
                access.SpawnBlockEntity(this.EntityClass, pos.Copy(), null);
                IOilWell bewell = access.GetBlockEntity(pos) as IOilWell; // grab the BE of the well
                if (bewell != null)
                {
                    // initalize the well object
                    bewell.InitDeposit(isLarge, access, wrand, this, this.api);
                }
            }
            BuildOilSpout(access, pos, face, wrand, isLarge);
            return true;
        }

        public void BuildOilSpout(IBlockAccessor access, BlockPos pos, BlockFacing face, LCGRandom wrand, bool isLarge)
        {            
            if (isLarge)
            {
                ClimateCondition climate = access.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues);
                float wtemp = climate.Temperature;
                float wrain = climate.Rainfall;
                if (wtemp < _minTempForLarge || wtemp > _maxTempForLarge) isLarge = false;
                if (wrain < _minRainForLarge || wrain > _maxRainForLarge) isLarge = false;
            }
            int surfacey = access.GetTerrainMapheightAt(pos); // surface            
            
            int radius = wrand.NextInt(_maxDepositRadius + 1); // grab a radius
            if (radius < _minDepositRadius) radius = _minDepositRadius; // ensure it's at least minimum size
            radius += isLarge ? _extraRadiusLarge : 0; // add on any bonus for deserts
            int pooldepth = wrand.NextInt(3) + 1; // surface pool depth
            int spoutheight = isLarge ? wrand.NextInt(8) + 6 : wrand.NextInt(4) + 4; // height of the spout
            if (!_genSpouts) spoutheight = 0;            
            
            int bubblecentery = surfacey / 2;
            BlockPos bubblepos = new BlockPos(pos.X, bubblecentery, pos.Z, BlockLayersAccess.Default);
            int spoutmaxy = surfacey + spoutheight;
            BlockPos poolcenter = new BlockPos(pos.X, surfacey, pos.Z, BlockLayersAccess.Default);
            // Place the spout
            for (int y = 1; y < spoutmaxy - pos.Y; y++)
            {
                access.SetBlock(_oilBlock.Id, pos.UpCopy(y));
                if (isLarge)
                {
                    access.SetBlock(_oilBlock.Id, pos.UpCopy(y).AddCopy(BlockFacing.NORTH));
                    access.SetBlock(_oilBlock.Id, pos.UpCopy(y).AddCopy(BlockFacing.EAST));
                    access.SetBlock(_oilBlock.Id, pos.UpCopy(y).AddCopy(BlockFacing.SOUTH));
                    access.SetBlock(_oilBlock.Id, pos.UpCopy(y).AddCopy(BlockFacing.WEST));
                }       
                access.TriggerNeighbourBlockUpdate(pos.UpCopy(y));
            }
            if (isLarge && _genSpouts)
            {
                int extraspoutstart = spoutmaxy - pos.Y;
                for (int y = extraspoutstart; y < extraspoutstart + spoutheight; y++)
                {
                    access.SetBlock(_oilBlock.Id, pos.UpCopy(y));
                }
            }
            if (_genDeposits)
            {
                List<BlockPos> bubble = BuildBubble(access, bubblepos, radius);
                foreach (BlockPos bub in bubble)
                {
                    access.SetBlock(_oilBlock.Id, bub);
                }
            }
            if (_genPool)
            {
                List<BlockPos> pool = BuildSurfacePool(access, poolcenter, radius, pooldepth, wrand);
                foreach (BlockPos bub in pool)
                {
                    access.SetBlock(_oilBlock.Id, bub);
                    access.TriggerNeighbourBlockUpdate(bub);
                }
            }
        }

        private List<BlockPos> BuildBubble(IBlockAccessor access, BlockPos center, int radius)
        {
            List<BlockPos> output = new List<BlockPos>();
            radius++;
            int maxoffsetx = center.X + radius;
            int maxoffsety = center.Y + radius;
            int maxoffsetz = center.Z + radius;
            int radiussq = radius * radius;
            for (int x = center.X - radius; x <= maxoffsetx; x++)
            {
                for (int y = center.Y - radius; y <= maxoffsety; y++)
                {
                    for (int z = center.Z - radius; z <= maxoffsetz; z++)
                    {                        
                        if (center.DistanceTo(x, y, z) <= radius && y > 1)
                        { 
                            output.Add(new BlockPos(x, y, z, BlockLayersAccess.Default)); 
                        }
                    }
                }
            }
            return output;
        }

        private List<BlockPos> BuildSurfacePool(IBlockAccessor access, BlockPos center, int radius, int depth, LCGRandom wrand)
        {
            List<BlockPos> output = new List<BlockPos>();
            List<BlockPos> disc = new List<BlockPos>();
            int radiussq = radius * radius;
            // first we need to generate a 'disc' of positions to check
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                for (int z = center.Z - radius;z <= center.Z + radius; z++)
                {
                    if (center.DistanceSqTo(x, center.Y, z) <= radiussq)
                    {
                        disc.Add(new BlockPos(x, center.Y, z, BlockLayersAccess.Default));
                    }
                }
            }
            foreach (BlockPos pos in disc)
            {
                if (wrand.NextFloat() > 0.9f) continue; // skip 10% of the disc, should prevent it from being a perfect circle
                Block blockat = access.GetBlock(pos);
                if (blockat.IsLiquid() && blockat.LiquidCode.Contains("oil")) continue; // skip existing oil blocks
                int topblocky = access.GetTerrainMapheightAt(pos);
                for (int offset = 0; offset < depth; offset++)
                {
                    output.Add(new BlockPos(pos.X, topblocky - offset, pos.Z, BlockLayersAccess.Default));
                }
            }
            disc.Clear();
            return output;
        }
    }
}
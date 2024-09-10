using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

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
                _oilblockloc = new AssetLocation(Attributes["oilblockcode"].AsString("vinteng:crudeoil"));
                _oilBlock = api.World.GetBlock(_oilblockloc);
            }
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor access, BlockPos pos, BlockFacing face, LCGRandom wrand)
        {
            if (pos.Y > 5) return false;
            if (_oilBlock.Id == 0) return false;
            access.SetBlock(this.BlockId, pos);
            if (this.EntityClass != null)
            {
                access.SpawnBlockEntity(this.EntityClass, pos, null);
            }
            return true;            
        }

        public void BuildOilSpout(IBlockAccessor access, BlockPos pos, BlockFacing face, LCGRandom wrand)
        {
            bool isLarge = wrand.NextFloat() <= _oddsForLarge; // first check on large deposits
            if (isLarge)
            {
                ClimateCondition climate = access.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues);
                float wtemp = climate.Temperature;
                float wrain = climate.Rainfall;
                if (wtemp < _minTempForLarge || wtemp > _maxTempForLarge) isLarge = false;
                if (wrain < _minRainForLarge || wrain > _maxRainForLarge) isLarge = false;
            }
            int largeRad = isLarge ? wrand.NextInt(_extraRadiusLarge + 1) : 0;
            int radius = wrand.NextInt(_maxDepositRadius + 1); // grab a radius
            if (radius < _minDepositRadius) radius = _minDepositRadius; // ensure it's at least minimum size
            radius += largeRad; // add on any bonus for deserts
            int pooldepth = wrand.NextInt(3) + 1; // surface pool depth
            int spoutheight = isLarge ? wrand.NextInt(6) + 6 : wrand.NextInt(3) + 3; // height of the spout

            int surfacey = access.GetTerrainMapheightAt(pos); // surface

            int bubblecentery = surfacey / 2;
            BlockPos bubblepos = new BlockPos(pos.X, bubblecentery, pos.Z, BlockLayersAccess.Default);
            int spoutmaxy = surfacey + spoutheight + 1;
            BlockPos poolcenter = new BlockPos(pos.X, surfacey, pos.Z, BlockLayersAccess.Default);
            // Place the spout
            for (int y = pos.Y + 1; y < spoutmaxy; y++)
            {
                access.SetBlock(_oilBlock.Id, pos.UpCopy(y));
                if (isLarge)
                {
                    access.SetBlock(_oilBlock.Id, pos.UpCopy(y).AddCopy(BlockFacing.NORTH));
                    access.SetBlock(_oilBlock.Id, pos.UpCopy(y).AddCopy(BlockFacing.EAST));
                    access.SetBlock(_oilBlock.Id, pos.UpCopy(y).AddCopy(BlockFacing.SOUTH));
                    access.SetBlock(_oilBlock.Id, pos.UpCopy(y).AddCopy(BlockFacing.WEST));
                }
            }
            List<BlockPos> bubble = BuildBubble(access, bubblepos, radius);
            foreach (BlockPos bub in bubble)
            {
                access.SetBlock(_oilBlock.Id, bub);
            }
            List<BlockPos> pool = BuildSurfacePool(access, poolcenter, radius, pooldepth, wrand);
            foreach (BlockPos bub in pool)
            {
                access.SetBlock(_oilBlock.Id, bub);
            }
        }

        private List<BlockPos> BuildBubble(IBlockAccessor access, BlockPos center, int radius)
        {
            List<BlockPos> output = new List<BlockPos>();
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
                        if (center.DistanceSqTo(x, y, z) <= radiussq && y > 1)
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
                if (wrand.NextFloat() > 0.9f) continue; // skip 10% of the disc
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VintageEngineering.Multiblock
{
    /// <summary>
    /// Export the schematic for these with the command: /we gmc (or /we generate-multiblock-code)<br/>
    /// WHILE looking at the core block to ensure offsets are generated properly.<br/>
    /// Be sure to add "blockHighlightColors" section to the properties of the behavior (see CreosoteOven multiblock JSON for an example)<br/>
    /// highlightID value defined in JSON is so different machines can highlight seperately from one-another, should be unique per multiblock.
    /// </summary>
    public class VEMultiblockStructure
    {
        public int HighlightSlotID = 0;        

        public Dictionary<AssetLocation, int> BlockNumbers = new Dictionary<AssetLocation, int>();
        
        public List<BlockOffsetAndNumber> Offsets = new List<BlockOffsetAndNumber>();

        public string OffsetsOrientation;

        public Dictionary<int, AssetLocation> BlockCodes;
        public List<BlockOffsetAndNumber> TransformedOffsets;
        public Dictionary<int, int> BlockHighlightColors;
        public Dictionary<int, string> BlockSwapMapping;

        public int MaxY => TransformedOffsets?.Max(v => v.Y) ?? int.MinValue;

        public int GetOrCreateBlockNumber(Block block)
        {
            if (!BlockNumbers.TryGetValue(block.Code, out int blockNum))
            {
                blockNum = BlockNumbers[block.Code] = 1 + BlockNumbers.Count;
            }
            return blockNum;
        }
        public void InitForUse(float rotateYDeg)
        {
            Matrixf mat = new Matrixf();
            mat.RotateYDeg(rotateYDeg);

            BlockCodes = new Dictionary<int, AssetLocation>();
            TransformedOffsets = new List<BlockOffsetAndNumber>();

            foreach (KeyValuePair<AssetLocation, int> val in BlockNumbers)
            {                
                BlockCodes[val.Value] = val.Key;
            }

            for (int i = 0; i < Offsets.Count; i++)
            {
                Vec4i offset = Offsets[i];
                Vec4f offsetTf = new Vec4f(offset.X, offset.Y, offset.Z, 0);
                Vec4f tfedOffset = mat.TransformVector(offsetTf);
                TransformedOffsets.Add(new BlockOffsetAndNumber() { X = (int)Math.Round(tfedOffset.X), Y = (int)Math.Round(tfedOffset.Y), Z = (int)Math.Round(tfedOffset.Z), W = offset.W });
            }
        }

        public string RotDegToDirection(float rotateYDeg)
        {
            if (rotateYDeg == 0) return "north";
            if (rotateYDeg == 90) return "west";
            if (rotateYDeg == 180) return "south";
            return "east";
        }

        public void InitHighlightColors(JsonObject json)
        {
            JsonObject[] array = json.AsArray();
            BlockHighlightColors = new Dictionary<int, int>();
            foreach (JsonObject obj in array)
            {
                int color = ColorUtil.ColorFromRgba(obj["r"].AsInt(), obj["g"].AsInt(), obj["b"].AsInt(), obj["a"].AsInt());
                BlockHighlightColors.Add(obj["w"].AsInt(), color);
            }
        }

        public void InitBlockSwapMapping(JsonObject json)
        {
            JsonObject[] array = json.AsArray();
            BlockSwapMapping = new Dictionary<int, string>();
            foreach (JsonObject obj in array)
            {
                BlockSwapMapping.Add(obj["w"].AsInt(), obj["tocode"].AsString());
            }
        }

        public void SwapBlocks(IWorldAccessor world, BlockPos centerPos, bool isComplete, string side)
        {
            //IBulkBlockAccessor bulk = world.GetBlockAccessorBulkUpdate(true, true, false);
            if (isComplete)
            {
                for (int i = 0; i < TransformedOffsets.Count; i++)
                {
                    Vec4i offset = TransformedOffsets[i];
                    if (offset.X == 0 && offset.Y == 0 && offset.Z == 0) continue;
                    Block toswap = world.GetBlock(new AssetLocation(BlockSwapMapping[offset.W] + $"-{side}"));
                    if (toswap != null)
                    {
                        BlockPos swappos = new BlockPos(centerPos.X + offset.X, centerPos.InternalY + offset.Y, centerPos.Z + offset.Z);
                        world.BlockAccessor.SetBlock(toswap.Id, swappos);
                        world.BlockAccessor.GetBlockEntity<VEMBEntityDummy>(swappos)?.SetOffset(offset);
                    }
                }
            }
            else
            {
                for (int i = 0; i < TransformedOffsets.Count;i++)
                {
                    Vec4i offset = TransformedOffsets[i];
                    if (offset.X == 0 && offset.Y == 0 && offset.Z == 0) continue;
                    Block swapback = world.GetBlock(new AssetLocation(BlockCodes[offset.W]));
                    if (swapback != null)
                    {
                        BlockPos swappos = new BlockPos(centerPos.X + offset.X, centerPos.InternalY + offset.Y, centerPos.Z + offset.Z);
                        world.BlockAccessor.SetBlock(0, swappos);
                        world.BlockAccessor.SetBlock(swapback.Id, swappos);
                        world.BlockAccessor.MarkBlockModified(swappos);
                    }
                }
            }
        }

        public void WalkMatchingBlocks(IWorldAccessor world, BlockPos centerPos, Action<Block, BlockPos> onBlock)
        {
            if (TransformedOffsets == null)
            {
                throw new InvalidOperationException("Call InitForUse() first");
            }

            BlockPos pos = new BlockPos(centerPos.dimension);

            for (int i = 0; i < TransformedOffsets.Count; i++)
            {
                Vec4i offset = TransformedOffsets[i];

                pos.Set(centerPos.X + offset.X, centerPos.Y + offset.Y, centerPos.Z + offset.Z);
                Block block = world.BlockAccessor.GetBlock(pos);

                if (WildcardUtil.Match(BlockCodes[offset.W], block.Code))
                {
                    onBlock?.Invoke(block, pos);
                }
            }
        }
        /// <summary>
        /// Check if the Multiblock structure is complete, ignoring Air, returns number of incomplete blocks.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="centerPos"></param>
        /// <param name="onMismatch"></param>
        /// <param name="layer">-1 to check all, 0 for first layer, 1 the layer above that and so on.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Thrown if InitForUse is not called.</exception>
        public int InCompleteBlockCount(IWorldAccessor world, BlockPos centerPos, PositionMismatchDelegate onMismatch = null, int layer = -1)
        {
            if (TransformedOffsets == null)
            {
                throw new InvalidOperationException("Call InitForUse() first");
            }

            int qinc = 0;            

            for (int i = 0; i < TransformedOffsets.Count; i++)
            {
                if (layer != -1 && TransformedOffsets[i].Y > layer) continue;

                Vec4i offset = TransformedOffsets[i];
                if (offset.X == 0 && offset.Y == 0 && offset.Z == 0) continue;

                Block block = world.BlockAccessor.GetBlockRaw(centerPos.X + offset.X, centerPos.InternalY + offset.Y, centerPos.Z + offset.Z);

                if (!WildcardUtil.Match(BlockCodes[offset.W], block.Code))
                {
                    onMismatch?.Invoke(block, BlockCodes[offset.W]);
                    qinc++;
                }
            }
            return qinc;
        }
        public void ClearHighlights(IWorldAccessor world, IPlayer player)
        {
            world.HighlightBlocks(player, HighlightSlotID, new List<BlockPos>(), new List<int>());
        }
        /// <summary>
        /// Highlight incomplete locations for Multiblock, optionally set a Layer restriction.<br/>
        /// Core must be on the bottom layer, layer 0, and layer 1 would be the positions directly above that and so on.<br/>
        /// layer defaults to -1, which will highlight ALL missing parts.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="player"></param>
        /// <param name="centerPos"></param>
        /// <param name="layer">-1 to highlight all, 0 to highlight first layer, 1 the layer above that and so on.</param>
        /// <exception cref="InvalidOperationException">Call InitHighlightColors()!</exception>
        public void HighlightIncompleteParts(IWorldAccessor world, IPlayer player, BlockPos centerPos, int layer = -1)
        {
            if (BlockHighlightColors == null)
            {
                throw new InvalidOperationException("Call InitHighlightColors()!");
            }
            List<BlockPos> blocks = new List<BlockPos>();
            List<int> colors = new List<int>();

            for (int i = 0; i < TransformedOffsets.Count; i++)
            {
                if (layer != -1 && TransformedOffsets[i].Y > layer) continue;

                Vec4i offset = TransformedOffsets[i];
                if (offset.X == 0 && offset.Y == 0 && offset.Z == 0) continue;
                Block block = world.BlockAccessor.GetBlockRaw(centerPos.X + offset.X, centerPos.InternalY + offset.Y, centerPos.Z + offset.Z);
                AssetLocation desireBlockLoc = BlockCodes[offset.W];

                if (!WildcardUtil.Match(BlockCodes[offset.W], block.Code))
                {
                    blocks.Add(new BlockPos(offset.X, offset.Y, offset.Z).Add(centerPos));

                    if (block.Id != 0)
                    {
                        // Highlight colors are set via JSON indexed by BlockNumber (w value)
                        // Must call InitHighlightColors() first!!
                        colors.Add(BlockHighlightColors[offset.W]);

                        //colors.Add(ColorUtil.ColorFromRgba(215, 94, 94, 64));
                    }
                    else
                    {
                        // Air Blocks... 
                        //int col = world.SearchBlocks(desireBlockLoc)[0].GetColor(world.Api as ICoreClientAPI, centerPos);
                        //col &= ~(255 << 24);
                        //col |= 96 << 24;
                        colors.Add(BlockHighlightColors[offset.W]);
                    }
                }
            }
            world.HighlightBlocks(player, HighlightSlotID, blocks, colors);
        }
    }
}

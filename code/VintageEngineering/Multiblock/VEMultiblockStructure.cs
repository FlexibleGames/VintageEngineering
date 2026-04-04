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
        public Dictionary<int, string[]> Attachables;
        public Dictionary<int, JsonObject> AttributesByNumber;

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

        /// <summary>
        /// Checks the given offset for a match in TransformedOffsets of this structure, returns the Block Number if found.
        /// </summary>
        /// <param name="offsetcheck"></param>
        /// <returns></returns>
        public int GetBlockNumFromOffset(Vec3i offsetcheck)
        {
            int? w = TransformedOffsets.FirstOrDefault(b =>
                b.X == offsetcheck.X &&
                b.Y == offsetcheck.Y &&
                b.Z == offsetcheck.Z)?.W;

            return w ?? -1;
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

        public void InitAttachable(JsonObject json)
        {
            if (json == null) return;
            JsonObject[] array = json.AsArray();
            if (array == null || array.Length == 0) return;

            Attachables = new Dictionary<int, string[]>();

            foreach (JsonObject obj in array)
            {
                Attachables.Add(obj["w"].AsInt(), obj["sides"].AsArray<string>(Array.Empty<string>(), null));
            }
        }

        public void InitAttributes(JsonObject json)
        {
            if (json == null) return;
            JsonObject[] array = json.AsArray();
            if (array == null || array.Length == 0) return;

            AttributesByNumber = new Dictionary<int, JsonObject>();

            foreach (JsonObject obj in array)
            {
                AttributesByNumber.Add(obj["w"].AsInt(), obj["attributes"]);
            }
        }

        /// <summary>
        /// Get the Allowed Variants of valid blocks for a given BlockNum in a VEMultiblock schematic.<br/>
        /// These options are all driven by custom structure attributes and custom code.
        /// </summary>
        /// <param name="blockNum">Block Number, commonly offset.W</param>
        /// <param name="facing">What direction this instance is facing.</param>
        /// <returns>A List of strings.</returns>
        public List<string> GetAllowedVariants(int blockNum, string facing, bool forSwap = false)
        {
            List<string> allowedVariants = new List<string>();            
            if (facing == null)  return allowedVariants;

            if (BlockCodes[blockNum].IsWildCard)
            {
                if (AttributesByNumber[blockNum].KeyExists("isfacing") ? AttributesByNumber[blockNum]["isfacing"].AsBool(false) : false)
                {                    
                    string orientation = AttributesByNumber[blockNum].KeyExists("orientation") ? AttributesByNumber[blockNum]["orientation"].AsString() : string.Empty;
                    if (orientation != "any")
                    {                        
                        BlockFacing bfacing = BlockFacing.FromCode(facing);
                        if (orientation == "facing")
                        {
                            allowedVariants.Add(facing);
                        }
                        if (orientation == "opposite")
                        {
                            allowedVariants.Add(bfacing.Opposite.Code);
                        }
                        else if (orientation == "cw")
                        {
                            allowedVariants.Add(bfacing.GetCW().Code);
                        }
                        else if (orientation == "ccw")
                        {
                            allowedVariants.Add(bfacing.GetCCW().Code);
                        }
                    }
                    else
                    {
                        foreach (BlockFacing face in BlockFacing.HORIZONTALS)
                        {
                            allowedVariants.Add(face.Code);
                        }
                    }
                }
                else if (forSwap && AttributesByNumber[blockNum].KeyExists("defaultvariant"))
                {
                    allowedVariants.Add(AttributesByNumber[blockNum]["defaultvariant"].AsString(string.Empty));
                }
            }
            return allowedVariants;
        }

        public void SwapBlocks(IWorldAccessor world, BlockPos centerPos, bool isComplete, string side)
        {
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
                        world.BlockAccessor.MarkBlockModified(swappos);
                    }
                }
            }
            else
            {
                for (int i = 0; i < TransformedOffsets.Count;i++)
                {
                    Vec4i offset = TransformedOffsets[i];
                    if (offset.X == 0 && offset.Y == 0 && offset.Z == 0) continue;
                    string swapvariant = string.Empty;
                    Block swapback = null;
                    if (BlockCodes[offset.W].IsWildCard)
                    {
                        List<string> variants = GetAllowedVariants(offset.W, side, true);
                        swapvariant = BlockCodes[offset.W].ToString().Replace("*", variants[0]);
                        swapback = world.GetBlock(new AssetLocation(swapvariant));
                    }
                    else swapback = world.GetBlock(new AssetLocation(BlockCodes[offset.W]));
                    if (swapback != null)
                    {
                        BlockPos swappos = new BlockPos(centerPos.X + offset.X, centerPos.InternalY + offset.Y, centerPos.Z + offset.Z);
                        world.BlockAccessor.SetBlock(0, swappos);
                        world.BlockAccessor.SetBlock(swapback.Id, swappos);
                        world.BlockAccessor.MarkBlockModified(swappos);
                        world.BlockAccessor.TriggerNeighbourBlockUpdate(swappos);
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
       
                List<string> allowedVariants = new List<string>();                

                if (BlockCodes[offset.W].IsWildCard)
                {
                    allowedVariants = GetAllowedVariants(offset.W, world.BlockAccessor.GetBlock(centerPos).Variant["side"]);
                }

                if (allowedVariants.Count > 0)
                {
                    if (!WildcardUtil.MatchesVariants(BlockCodes[offset.W], block.Code, allowedVariants.ToArray()))
                    {
                        onMismatch?.Invoke(block, BlockCodes[offset.W]);
                        qinc++;
                    }
                }
                else if (!WildcardUtil.Match(BlockCodes[offset.W], block.Code))
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

                List<string> allowedVariants = new List<string>();

                if (BlockCodes[offset.W].IsWildCard)
                {
                    allowedVariants = GetAllowedVariants(offset.W, world.BlockAccessor.GetBlock(centerPos).Variant["side"]);
                }

                if (allowedVariants.Count > 0)
                {
                    if (!WildcardUtil.MatchesVariants(BlockCodes[offset.W], block.Code, allowedVariants.ToArray()))
                    {
                        blocks.Add(new BlockPos(offset.X, offset.Y, offset.Z).Add(centerPos));
                        colors.Add(BlockHighlightColors[offset.W]);
                    }
                }
                else if (!WildcardUtil.Match(BlockCodes[offset.W], block.Code))
                {
                    blocks.Add(new BlockPos(offset.X, offset.Y, offset.Z).Add(centerPos));
                    colors.Add(BlockHighlightColors[offset.W]);                    
                }
            }
            world.HighlightBlocks(player, HighlightSlotID, blocks, colors);
        }
    }
}

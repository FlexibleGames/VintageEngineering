using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using VintageEngineering.Electrical.Systems.Catenary;
using VintageEngineering.Multiblock;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical
{
    public class BEMBPowerConnector : ElectricSimpleBE, IMBPassThrough
    {
        [AllowNull]
        private BlockPos _corePosition;

        private ElectricBEBehavior _coreBehavior;

        public BlockPos CorePosition { get { return _corePosition; } }

        public BlockEntity CoreEntity => Api.World.BlockAccessor.GetBlockEntity(_corePosition);

        public Block CoreBlock => Api.World.BlockAccessor.GetBlock(_corePosition);

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                if (ListenerID > 0) UnregisterGameTickListener(ListenerID);
                ListenerID = 0;
            }
            if (ValidateCore())
            {                                        
                _coreBehavior = api.World.BlockAccessor.GetBlockEntity(_corePosition).GetBehavior<ElectricBEBehavior>();
            }            
        }

        public bool ValidateCore()
        {
            if (_corePosition != null)
            {
                if (Api != null)
                {
                    Block core = Api.World.BlockAccessor.GetBlock(CorePosition);
                    IElectricalBlockEntity ibe = core.GetInterface<IElectricalBlockEntity>(Api.World, CorePosition);
                    if (core != null && core.Id != 0 && ibe != null) return true;
                    else
                    {
                        // double check what this is connected to
                        return GetCore();
                    }
                }
            }
            else
            {
                // check what we are connected to
                return GetCore();
            }
            return false;
        }

        public bool GetCore()
        {
            string position = base.Block.Variant["position"];
            BlockFacing underneath = BlockFacing.FromCode(position).Opposite;
            BlockPos underpos = this.Pos.AddCopy(underneath);
            Block underblock = Api.World.BlockAccessor.GetBlock(underpos);
            ElectricBlock us = base.Block as ElectricBlock;
            if (us == null) return false;

            if (underblock.Id != 0)
            {
                if (underblock is VEMBDummy dummy)
                {
                    // we are sitting on a Multiblock
                    if (dummy.Variant["io"] == "power")
                    {
                        if (dummy.GetOffset(underpos) == Vec3i.Zero) return false;
                        // we are on a power dummy block
                        VEMBEntityCore mbcore = Api.World.BlockAccessor.GetBlockEntity<VEMBEntityCore>(underpos.AddCopy(-dummy.GetOffset(underpos)));
                        if (mbcore != null)
                        {
                            if (mbcore.Multiblock == null || mbcore.Multiblock.mbs == null) return false;
                            if (mbcore.Multiblock.mbs.AttributesByNumber == null) return false;
                            if (mbcore.Multiblock.mbs.AttributesByNumber.Count == 0) return false;

                            int number = mbcore.Multiblock.mbs.GetBlockNumFromOffset(dummy.GetOffset(underpos));

                            if (number == -1 || !mbcore.Multiblock.mbs.AttributesByNumber.ContainsKey(number)) return false;

                            EnumElectricalPowerTier ourtier = Enum.Parse<EnumElectricalPowerTier>(us.Attributes["wireNodes"].AsArray()[0]["powertier"].AsString());
                            EnumElectricalPowerTier coretier = Enum.Parse<EnumElectricalPowerTier>(mbcore.Multiblock.mbs.AttributesByNumber[number]["powertier"].AsString());
                            if (ourtier == coretier)
                            {
                                _corePosition = mbcore.Pos.Copy();
                                return true;
                            }
                            else return false;
                        }
                    }
                }
                else
                {
                    if (underblock is ElectricBlock)
                    {
                        // we are attached to a single block machine
                        return false;
                    }
                }
            }
            return false;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {            
            string position = base.Block.Variant["position"];
            BlockFacing underneath = BlockFacing.FromCode(position).Opposite;
            BlockPos underpos = this.Pos.AddCopy(underneath);
            Block underblock = Api.World.BlockAccessor.GetBlock(underpos);
            if (underblock is not VEMBDummy)
            {
                Api.World.BlockAccessor.BreakBlock(Pos, null, 1);
                if (Api is ICoreClientAPI capi)
                {
                    capi.World.Player.ShowChatNotification($"Place after the Multiblock is formed.");
                }
            }
            else
            {
                base.OnBlockPlaced(byItemStack);
            }
        }

        public void NeighborBlockChanged(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            string position = base.Block.Variant["position"];
            BlockFacing underneath = BlockFacing.FromCode(position).Opposite;
            BlockPos underpos = this.Pos.AddCopy(underneath);
            Block underblock = Api.World.BlockAccessor.GetBlock(underpos);
            if (underblock is not VEMBDummy)
            {
                // block under us is not a dummy means the MB was deconstructed
                world.BlockAccessor.BreakBlock(pos, null, 1); // self destruct                
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (CorePosition != null)
            {
                dsc.AppendLine($"{Lang.Get("vinteng:gui-coreat")} {this.CorePosition.ToLocalPosition(Api).ToBlockPos()}");
            }
            else
            {
                dsc.AppendLine(Lang.Get("vinteng:gui-coreinvalid"));
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (_corePosition != null) tree.SetBlockPos("coreposition", _corePosition);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _corePosition = tree.GetBlockPos("coreposition", null);
        }
    }
}

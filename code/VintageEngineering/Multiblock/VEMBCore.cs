using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using Vintagestory.API.Client;
using VintageEngineering.API;

namespace VintageEngineering.Multiblock
{
    /// <summary>
    /// The Core Block of Multiblock Machines<br/>
    /// Multiblocks are not directly wire-connectable but will require a Power Connection block (of the correct tier) placed in the proper location on the multiblock.
    /// </summary>
    public class VEMBCore : ElectricBlock
    {

        public VEMultiblockBeh Multiblock { get { return this.GetBehavior<VEMultiblockBeh>(); } } 

        private EnumElectricalPowerTier[] powerTiers;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (Attributes["wireNodes"].Exists)
            {
                JsonObject[] nodes = Attributes["wireNodes"].AsArray();
                if (nodes != null && nodes.Length > 0)
                {
                    powerTiers = new EnumElectricalPowerTier[nodes.Length];
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        powerTiers[i] = Enum.Parse<EnumElectricalPowerTier>(nodes[i]["powertier"]?.AsString("None"));
                    }
                }
            }
        }

        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
        {
            VEMBEntityCore entity = world.BlockAccessor.GetBlockEntity<VEMBEntityCore>(blockSel.Position);
            if (entity != null)
            {
                entity.ActivateCore(world, caller, blockSel, activationArgs);
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false; // only block if we can't interact via permissions with this block
            }
            if (byPlayer != null && byPlayer.InventoryManager != null)
            {
                if (byPlayer.InventoryManager.ActiveHotbarSlot != null && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
                {
                    bool extDebug = (api as ICoreClientAPI)?.Settings.Bool["extendedDebugInfo"] == true;
                    if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Code.Path.Contains("stick"))
                    {                        
                        if (extDebug)
                        {
                            VEMBEntityCore core = world.BlockAccessor.GetBlockEntity<VEMBEntityCore>(blockSel.Position);
                            if (core != null) core.Electric.electricpower = 0;
                        }
                    }
                    if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Tool == EnumTool.Wrench)
                    {
                        if (Variant["state"] == "incomplete")
                        {
                            Multiblock.TriggerValidation(world, byPlayer, blockSel, blockSel.Position);
                        }
                        else
                        {
                            VEMBEntityCore core = world.BlockAccessor.GetBlockEntity<VEMBEntityCore>(blockSel.Position);
                            if (core != null) core.OnPlayerRightClick(byPlayer, blockSel);
                        }
                    }
                    // Fluid Interaction
                    ItemSlot hotbar = byPlayer.InventoryManager.ActiveHotbarSlot;
                    ILiquidSource source = hotbar.Itemstack.Collectible as ILiquidSource;
                    VEMBEntityCore mbcore = world.BlockAccessor.GetBlockEntity<VEMBEntityCore>(blockSel.Position);
                    IVELiquidInterface ivel = mbcore as IVELiquidInterface;
                    if (source != null && ivel != null)
                    {
                        if (!source.AllowHeldLiquidTransfer) return false;
                        
                        ItemStack tomove = source.GetContent(hotbar.Itemstack);
                        if (tomove != null && tomove.StackSize > 0)
                        {
                            DummySlot topush;
                            if (hotbar.Itemstack.StackSize > 1) // holding more than one bucket
                            {
                                ItemStack singlebucket = hotbar.Itemstack.Clone();
                                singlebucket.StackSize = 1;
                                topush = new((singlebucket.Collectible as ILiquidSource).GetContent(singlebucket));
                            }
                            else
                            {
                                topush = new(tomove);
                            }                            
                            ItemSlotLiquidOnly pushto = (ItemSlotLiquidOnly)ivel.GetLiquidAutoPushIntoSlot(blockSel.Face, topush);
                            if (pushto == null) return true;
                            WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(tomove);
                            int capfree = (int)(pushto.CapacityLitres * wprops.ItemsPerLitre) - (int)(pushto.StackSize);
                            int nummoved = topush.TryPutInto(world, pushto, topush.StackSize);
                            if (nummoved > 0)
                            {
                                VEHelpers.SplitStackAndPerformAction(api, byPlayer.Entity, hotbar, delegate (ItemStack stack)
                                {
                                    source.TryTakeContent(stack, nummoved);
                                    return nummoved;
                                });
                                VEHelpers.DoLiquidMovedEffects(api, byPlayer, tomove, nummoved, BlockLiquidContainerBase.EnumLiquidDirection.Pour);
                                mbcore.MarkDirty(true);
                                return true;
                            }
                        }
                    }
                    ILiquidSink sink = hotbar.Itemstack.Collectible as ILiquidSink;
                    ItemSlotLiquidOnly pull = ivel?.GetLiquidAutoPullFromSlot(blockSel.Face);                    
                    if (sink != null && pull != null)
                    {
                        if (!sink.AllowHeldLiquidTransfer) return false;
                        ItemStack owncontentstack = pull.Itemstack;
                        if (owncontentstack != null)
                        {
                            ItemStack liquidparticles = owncontentstack.Clone();
                            float liters = GameMath.Max(sink.TransferSizeLitres, sink.CapacityLitres);
                            int moved2 = VEHelpers.SplitStackAndPerformAction(api, byPlayer.Entity, hotbar, (ItemStack stack) => sink.TryPutLiquid(stack, owncontentstack, liters));
                            if (moved2 > 0) 
                            {
                                pull.TakeOut(moved2);
                                mbcore.MarkDirty(true);
                                return true;
                            }
                        }
                    }
                }
            }
             return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (Variant["state"] == "built")
            { 
                Multiblock?.MBOnBlockBroken(world, pos, Vec3i.Zero, byPlayer, dropQuantityMultiplier); 
            }
            else 
            {
                Multiblock.mbs.ClearHighlights(world, byPlayer);
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier); 
            }
        }

        /// <summary>
        /// Retrieves the Power Tier supported at a given connection Index
        /// </summary>
        /// <param name="connectionIndex">Connection Index</param>
        /// <returns>EnumElectricalPowerTier</returns>
        public EnumElectricalPowerTier GetPowerTierAt(int connectionIndex)
        {
            if (powerTiers == null) return EnumElectricalPowerTier.None;
            if (connectionIndex > powerTiers.Length) return EnumElectricalPowerTier.None;
            return powerTiers[connectionIndex];
        }

        public override bool CanAttachWire(IWorldAccessor world, Block wireitem, BlockSelection selection)
        {
            // Multiblocks do not directly connect to a wire, but they have to define what power tier they need

            return false;
        }

    }
}

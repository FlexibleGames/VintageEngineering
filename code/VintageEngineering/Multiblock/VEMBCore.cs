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

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer != null && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible?.Tool == EnumTool.Wrench)
                {
                    Multiblock.TriggerValidation(world, byPlayer, blockSel, blockSel.Position);
                }
                if (byPlayer.InventoryManager.ActiveHotbarSlot != null &&
                    !byPlayer.InventoryManager.ActiveHotbarSlot.Empty &&
                    byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Code.Path.Contains("stick"))
                {
                    bool extDebug = true;
                    if (extDebug)
                    {
                        VEMBEntityCore core = world.BlockAccessor.GetBlockEntity<VEMBEntityCore>(blockSel.Position);
                        if (core != null) core.Electric.electricpower = 0;
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

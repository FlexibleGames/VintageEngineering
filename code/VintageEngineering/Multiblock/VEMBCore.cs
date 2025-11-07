using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VintageEngineering.Multiblock
{
    /// <summary>
    /// The Core Block of Multiblock Machines
    /// </summary>
    public class VEMBCore : ElectricBlock
    {
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

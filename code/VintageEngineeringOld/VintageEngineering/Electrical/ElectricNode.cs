using System;
using System.Collections.Generic;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Base Electric Node interface.
    /// </summary>
    public interface IElectricNode
    {
        BlockPos Position { get; set; }
        WireAnchor WireAnchor { get; set; }
        EnumElectricalPowerTier PowerTier { get; set; }
    }

    /// <summary>
    /// Base Electric Node defined by JSON Behavior values
    /// </summary>
    public class BaseElectricNode : IElectricNode
    {
        public BlockPos Position { get; set; }
        public WireAnchor WireAnchor { get; set; }
        public EnumElectricalPowerTier PowerTier { get; set; }

        public BaseElectricNode() { }
        public BaseElectricNode(BlockPos pos, WireAnchor wireAnchor, EnumElectricalPowerTier powerTier)
        {
            Position = pos;
            WireAnchor = wireAnchor;
            PowerTier = powerTier;
        }
    }
}

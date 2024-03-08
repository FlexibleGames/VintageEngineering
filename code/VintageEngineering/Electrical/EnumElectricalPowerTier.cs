using System;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// The Tier of electrical network supported by this object.
    /// </summary>
    [Flags]
    public enum EnumElectricalPowerTier
    {
        None = 0,
        LV = 1,
        MV = 2,
        HV = 4,
        EV = 8,
        Any = LV | MV | HV | EV
    }
}

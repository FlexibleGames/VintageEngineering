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
    /// <summary>
    /// The purpose of an Electrical object
    /// </summary>
    public enum EnumElectricalEntityType
    {
        /// <summary>
        /// A consumer of power
        /// </summary>
        Consumer,
        /// <summary>
        /// A producer/generator of power
        /// </summary>
        Producer,
        /// <summary>
        /// Storage
        /// </summary>
        Storage,
        /// <summary>
        /// Storage with a bridge between 2 networks
        /// <br>Allows for different tiers of networks.</br>
        /// </summary>
        Transformer,
        /// <summary>
        /// An interactable switch between two networks.
        /// </summary>
        Toggle,
        /// <summary>
        /// Some other type of entity not covered in any other given types.
        /// </summary>
        Other
    }
}

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
    /// The purpose of an Electrical Block Entity
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
        /// Storage, it stores power.
        /// </summary>
        Storage,
        /// <summary>        
        /// <br>A bridge between different tiers of networks.</br>
        /// </summary>
        Transformer,
        /// <summary>
        /// An interactable switch between two networks.
        /// </summary>
        Toggle,
        /// <summary>
        /// A Relay is a dummy node that simply allows wires to connect to it. Like the nodes 
        /// of a Telephone pole.
        /// </summary>
        Relay,
        /// <summary>
        /// Some other type of entity not covered in any other given types.
        /// </summary>
        Other
    }
}

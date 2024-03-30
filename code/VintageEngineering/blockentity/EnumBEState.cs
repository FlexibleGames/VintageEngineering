using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VintageEngineering
{
    /// <summary>
    /// What State is this Block Entity at?
    /// </summary>
    public enum EnumBEState
    {
        /// <summary>
        /// Machine turned off by the player.
        /// </summary>
        Off,
        /// <summary>
        /// Machine is On and Crafting.
        /// </summary>
        On,
        /// <summary>
        /// Machine is On and NOT Crafting.
        /// </summary>
        Sleeping,
        /// <summary>
        /// Machine is On, it wants to craft, but something is wrong and has to pause.
        /// </summary>
        Paused
    }
}

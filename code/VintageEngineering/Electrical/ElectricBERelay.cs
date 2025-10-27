using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// A Relay is a simple block for wire connections, like on the tops of power poles.    
    /// </summary>
    public class ElectricBERelay : BlockEntity
    {
        // what use is this? Why might we need these to be smarter?
        // this is a type of block that has no internal power, doesn't tick but still needs
        // to be part of an electric network and wiring system
    }
}

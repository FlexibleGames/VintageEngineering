using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// A Relay is a simple block for wire connections, like on the tops of power poles.
    /// </summary>
    public class ElectricBERelay : ElectricBENoGUI
    {
        public override bool CanReceivePower => false;
        public override bool CanExtractPower => false;

        // TODO: Detect what this is attached to and whether to pass power to/from it.
    }
}

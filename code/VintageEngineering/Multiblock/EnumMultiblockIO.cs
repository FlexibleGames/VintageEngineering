using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VintageEngineering.Multiblock
{
    [Flags]
    public enum EnumMultiblockIO
    {
        None = 0,
        ItemInput = 1,
        ItemOutput = 2,
        FluidInput = 4,
        FluidOutput = 8,
        PowerInput = 16,
        PowerOutput = 32,
        SignalInput = 64,
        SignalOutput = 128,
        GasInput = 256,
        GasOutput = 512,
        MechanicalInput = 1024,
        MechanicalOutput = 2048,
        Interaction = 4096
    }
}

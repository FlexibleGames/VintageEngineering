using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Multiblock
{
    /// <summary>
    /// Dummy BlockEntity for VE Multiblock System
    /// </summary>
    public class VEMBEntityDummy : BlockEntity
    {
        private Vec3i _offset;

        /// <summary>
        /// Offset of this block from the Core of the Multiblock
        /// </summary>
        public Vec3i Offset { get { return _offset; } }

    }
}

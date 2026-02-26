using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Multiblock
{
    public interface IMBPassThrough
    {
        /// <summary>
        /// Core BlockEntity that this interfaces with.
        /// </summary>
        BlockEntity CoreEntity { get; }

        /// <summary>
        /// Core Block that this interfaces with.
        /// </summary>
        Block CoreBlock { get; }

        /// <summary>
        /// Core BlockPos that this interfaces with.
        /// </summary>
        BlockPos CorePosition { get; }
    }
}

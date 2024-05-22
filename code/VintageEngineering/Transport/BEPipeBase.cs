using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public abstract class BEPipeBase: BlockEntityOpenableContainer
    {
        protected long NetworkID;

        protected List<PipeConnection> connections;
        protected PipeConnection[] extractionConnections;

        protected bool[] extractionSides;   // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] disconnectedSides; // uses BlockFacing index, N, E, S, W, U, D

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (extractionConnections == null) { extractionConnections = new PipeConnection[6]; }
            if (extractionSides == null) { extractionSides = new bool[6]; }
            if (disconnectedSides == null) {  disconnectedSides = new bool[6]; }
        }

    }
}

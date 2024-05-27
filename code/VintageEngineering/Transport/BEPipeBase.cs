using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public abstract class BEPipeBase : BlockEntityOpenableContainer
    {
        protected long _networkID;        

        protected List<PipeConnection> pushConnections;
        protected PipeConnection[] extractionConnections;

        protected bool[] connectionSides;   // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] extractionSides;   // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] disconnectedSides; // uses BlockFacing index, N, E, S, W, U, D

        /// <summary>
        /// Used by Extraction nodes to sort and push into based on settings.<br/>
        /// PipeConnection object contains a Distance variable set when this list is built.
        /// </summary>
        public List<PipeConnection> PushConnections
        { get { return pushConnections; } }

        /// <summary>
        /// NetworkID assigned to this pipe block. Should not be 0.
        /// </summary>
        public long NetworkID
        {
            get { return _networkID; }
            set { _networkID = value; }
        }

        /// <summary>
        /// Sides which have a valid connection available, uses BlockFacing index, N, E, S, W, U, D
        /// </summary>
        public bool[] ConnectionSides
        {
            get { return connectionSides; }
        }
        /// <summary>
        /// Sides which are set to Extraction Mode, uses BlockFacing index, N, E, S, W, U, D
        /// </summary>
        public bool[] ExtractionSides
        { get { return extractionSides; } }

        /// <summary>
        /// Sides which have valid connections but the player disconnected them manually, uses BlockFacing index, N, E, S, W, U, D
        /// </summary>
        public bool[] DisconnectedSides
        { get { return disconnectedSides; } }        

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (extractionConnections == null) { extractionConnections = new PipeConnection[6]; }
            connectionSides ??= new bool[6];
            if (extractionSides == null) { extractionSides = new bool[6]; }
            if (disconnectedSides == null) {  disconnectedSides = new bool[6]; }
        }

        /// <summary>
        /// Rebuild the connection directions; for example, when a Neighbor block changes.
        /// </summary>
        /// <param name="world">WorldAccessor object</param>
        public virtual void MarkPipeDirty(IWorldAccessor world)
        {

        }
        /// <summary>
        /// Rebuild this pipe blocks push connection list based on the given BlockPos array.<br/>
        /// BlockPos array should be all the block positions of the pipes, not the connected machines.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pipenetwork">BlockPos array of all pipes in this network.</param>
        public virtual void RebuildPushConnections(IWorldAccessor world, BlockPos[] pipenetwork)
        {

        }
    }
}

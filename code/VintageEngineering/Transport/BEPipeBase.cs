using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public abstract class BEPipeBase : BlockEntity
    {
        protected long _networkID;
        protected MeshData _meshData;
        //protected MeshRef _meshRef;

        protected List<PipeConnection> pushConnections;
        protected PipeExtractionNode[] extractionNodes;
        protected int numExtractionConnections;
        protected int numInsertionConnections;


        protected bool[] connectionSides;   // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] extractionSides;   // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] disconnectedSides; // uses BlockFacing index, N, E, S, W, U, D
        protected bool[] insertionSides;    // uses BlockFacing index, N, E, S, W, U, D

        /// <summary>
        /// Used by Extraction nodes to sort and push into based on settings.<br/>
        /// PipeConnection object contains a Distance variable set when this list is built.
        /// </summary>
        public List<PipeConnection> PushConnections
        { get { return pushConnections; } }

        /// <summary>
        /// Number of extraction nodes for this pipe block<br/>
        /// If 0, this block doesn't need to tick.
        /// </summary>
        public int NumExtractionConnections
        {  get { return numExtractionConnections; } }

        /// <summary>
        /// Number of insertion nodes for this pipe block.
        /// </summary>
        public int NumInsertionConnections
        { get { return numInsertionConnections; } }

        /// <summary>
        /// NetworkID assigned to this pipe block. Should not be 0.
        /// </summary>
        public long NetworkID
        {
            get { return _networkID; }
            set { _networkID = value; }
        }

        /// <summary>
        /// Sides which have a valid pipe->pipe connection available, uses BlockFacing index, N, E, S, W, U, D<br/>
        /// Pipe to pipe connections only, not insertion or extraction connections.
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
        /// <summary>
        /// Sides which have a valid block to insert into, does not include pipe->pipe connections.
        /// </summary>
        public bool[] InsertionSides
        { get { return insertionSides; } }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            extractionNodes ??= new PipeExtractionNode[6];
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
            bool shapedirty = false;
            // Check all 6 sides
            // the order is N, E, S, W, U, D
            for (int f = 0; f < BlockFacing.ALLFACES.Length; f++)
            {
                Block dblock = world.BlockAccessor.GetBlock(this.Pos.AddCopy(BlockFacing.ALLFACES[f]), BlockLayersAccess.Solid);
                BlockEntity dbe = world.BlockAccessor.GetBlockEntity(this.Pos.AddCopy(BlockFacing.ALLFACES[f]));

                // NEED to track NetworkID's of all faces, merge networks, join networks as needed.

                if (dblock.Id == 0) // face direction is air block
                {
                    // block is air, not a valid block to connect to.
                    if (extractionSides[f])
                    {
                        // while the block is air, we have an extraction node trying to connect to it
                        // TODO need to drop any upgrade or filter it may contain.

                        numExtractionConnections--;
                        shapedirty = true;
                        extractionSides[f] = false;
                    }
                    if (disconnectedSides[f])
                    {
                        // connection was previously manually overridden, remove that flag
                        disconnectedSides[f] = false;
                    }
                    if (insertionSides[f])
                    {                        
                        numInsertionConnections--; // block is now air, nothing to insert into
                        insertionSides[f] = false;
                        shapedirty = true;
                    }
                    if (connectionSides[f])
                    {
                        connectionSides[f] = false;
                        shapedirty = true;
                    }
                }
                else
                {
                    // block is NOT air, meaning a valid block
                    // need to check the entity now
                }
            }

            if (shapedirty) RebuildShape();
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

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {            
            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        /// <summary>
        /// Rebuilds the shape based on the connection flags, should ONLY be called when a neighbor block changes
        /// or the player overrides a valid connection.<br/>
        /// Does NOT need to be called when adding extraction node upgrades or filters!
        /// </summary>
        public virtual void RebuildShape()
        {

        }

        /// <summary>
        /// Checks the block position to determine whether this pipe type can interface with it.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pos">Position to check</param>
        /// <returns>True if pipe connection is supported.</returns>
        public virtual bool CanConnectTo(IWorldAccessor world, BlockPos pos)
        {
            return false;
        }

        /// <summary>
        /// Adds an extraction tick event for a single extraction node for this block entity.
        /// </summary>
        /// <param name="delayms">Required Tick Delay</param>
        /// <param name="tickEvent">Tick Handler Method</param>
        /// <returns>listenerID</returns>
        public long AddExtractionTickEvent(int delayms, Action<float> tickEvent)            
        {
            return this.RegisterGameTickListener(tickEvent, delayms);
        }
        /// <summary>
        /// Removes a ExtractionNode tick event from the pool.
        /// </summary>
        /// <param name="lid">ListenerID to remove.</param>
        public void RemoveExtractionTickEvent(long lid)
        {
            this.UnregisterGameTickListener(lid);
        }
    }
}

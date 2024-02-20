using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using VintageEngineering.Electrical.Systems.Catenary; // could change to base VS API

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Specific Wire Connector BE Behavior for Electric Network connections.
    /// <br>Used for setting the PowerTier of the anchor point.</br>
    /// <br>Power simulation involves the BlockEntity and IElectricalBlockEntity interface.</br>
    /// <br>Base class : BEBehaviorWire</br>
    /// </summary>
    public class BEBehaviorElectricConnector : BEBehaviorWire
    {
        private CatenaryMod cm;
        private Dictionary<int, BaseElectricNode> electricNodes;
        // ElectricNetworkMod enm;

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            /// This method is called right after the block entity was spawned or right after it was loaded 
            ///     from a newly loaded chunk. You do have access to the world and its blocks at this point.
            /// However if this block entity already existed then FromTreeAttributes is called first!
            base.Initialize(api, properties);
            if (api.Side == EnumAppSide.Client)
            {
                cm = api.ModLoader.GetModSystem<CatenaryMod>();
            }
            if (this.Blockentity.Block is IWireAnchor wireblock)
            {
                if (electricNodes == null)
                {
                    electricNodes = new Dictionary<int, BaseElectricNode>(); //new BaseElectricNode[wireblock.NumAnchorsInBlock(EnumWireFunction.Power)];
                }
                JsonObject[] behwirenodes = properties["wireNodes"].AsArray();
                for (int i = 0; i < behwirenodes.Length; i++)
                {
                    WireAnchor anchor = wireblock.GetWireAnchorInBlock(behwirenodes[i]["index"].AsInt(0));
                    if (anchor == null) throw new ArgumentNullException("WireAnchor anchor");

                    electricNodes[i] = new BaseElectricNode(this.Pos, anchor, behwirenodes[i]["powertier"].AsObject<EnumElectricalPowerTier>());
                }
            }
        }

        public BEBehaviorElectricConnector(BlockEntity blockentity) : base(blockentity)
        {
        }
    }
}

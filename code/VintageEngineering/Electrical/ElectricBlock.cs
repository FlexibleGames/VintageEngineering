using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Generic Block for all Electrical Machines
    /// <br>Loads more data from JSON for machines.</br>
    /// </summary>
    public class ElectricBlock : WiredBlock
    {
        /// <summary>
        /// What sort of Machine is this?
        /// <br>Valid Types: Consumer, Producer, Storage, Transformer, Toggle, Relay, Other</br>
        /// </summary>
        public EnumElectricalEntityType ElectricalEntityType 
        { 
            get
            {
                return Enum.Parse<EnumElectricalEntityType>(this.Attributes["entitytype"].AsString("Other"));
            }
        }

        /// <summary>
        /// What is the max power this machine can hold?
        /// <br>Type : Unsigned Long (ulong)</br>
        /// <br>Max Value : 18,446,744,073,709,551,615</br>
        /// </summary>
        public ulong MaxPower
        {
            get
            {
                return (ulong)this.Attributes["maxpower"].AsDouble();
            }
        }

        /// <summary>
        /// What is the MAX Power per second this machine can give or accept
        /// <br>Type : Unsigned Long (ulong)</br>
        /// <br>Max Value : 18,446,744,073,709,551,615</br>
        /// </summary>
        public ulong MaxPPS
        {
            get
            {
                return (ulong)this.Attributes["maxpps"].AsDouble();
            }
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api); // base call sets wire anchors and functions

        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            IWireNetwork wiredblock = world.BlockAccessor.GetBlockEntity(pos) as IWireNetwork;
            if (wiredblock != null) { return Environment.NewLine + this.Code.ToString() + Environment.NewLine + wiredblock.GetNetworkInfo(); }
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false; // only block if we can't interact via permissions with this block
            }
            if (base.OnWireInteractionStart(world, byPlayer, blockSel)) return true;
            return true;
        }

        /// <summary>
        /// Vital Call: Override to determine if a given Wire type (Block) can connect to this Machine.
        /// <br>Wire Type (Block) Defined in the Catenary mod.</br>
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="wireitem">Wire Block Player is Holding</param>
        /// <param name="selectionIndex">Selection Index for Wire Node</param>
        /// <returns>True if wire connection allowed.</returns>
        public override bool CanAttachWire(IWorldAccessor world, Block wireitem, BlockSelection selection)
        {
            // TODO: check max-connections with number of current connections
            if (wireitem is not BlockWire) return false;

            IWireAnchor anchor = world.BlockAccessor.GetBlock(selection.Position) as IWireAnchor;

            // get the max allowed number of connections for this node
            int maxcons = anchor.GetMaxConnections(selection.SelectionBoxIndex);

            CatenaryMod cm = api.ModLoader.GetModSystem<CatenaryMod>(true);
            int havecons = cm.GetNumberConnectionsAt(selection);
            if (havecons == -1) return false; // wirenode was invalid! Do not allow a connection

            if (maxcons == 0 || havecons >= maxcons) return false; // we're maxed on connections to this node.

            EnumElectricalPowerTier blocktier = Enum.Parse<EnumElectricalPowerTier>(this.Attributes["wireNodes"].AsArray()[selection.SelectionBoxIndex]["powertier"].AsString());
            if (wireitem.Attributes["wirefunction"].AsString("") == "Power")
            {
                EnumElectricalPowerTier wiretier = Enum.Parse<EnumElectricalPowerTier>(wireitem.Attributes["powertier"].AsString());
                if (blocktier != wiretier) return false;
            }
            return true; 
        }
    }
}

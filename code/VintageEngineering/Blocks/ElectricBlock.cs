using System;
using System.Runtime.CompilerServices;
using VintageEngineering.Electrical.Systems;
using VintageEngineering.Electrical.Systems.Catenary;
using VintageEngineering.RecipeSystem;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Generic Block for all Electrical Machines
    /// <br>Loads more data from JSON for machines.</br>
    /// </summary>
    public class ElectricBlock : WiredBlock
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api); // IMPORTANT base call sets wire anchors and functions

            RegisterRecipeMachine(api);
        }

        private void RegisterRecipeMachine(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;

            string[] recipeTypes = Attributes?["recipeMachine"]?.AsArray<string>();
            if (recipeTypes == null || recipeTypes.Length == 0) return;

            VERecipeRegistrySystem mod = api.ModLoader.GetModSystem<VERecipeRegistrySystem>(true);
            if (mod == null) return;
            foreach (string recipeType in recipeTypes)
            {
                mod.RegisterRecipeMachine(recipeType, this);
            }
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            bool extDebug = (api as ICoreClientAPI)?.Settings.Bool["extendedDebugInfo"] == true;
            string baseText = base.GetPlacedBlockInfo(world, pos, forPlayer);
            if (extDebug)
            {
                // The base method iterates the block behaviors, the decors, and the block entity. It also adds the
                // default block description. There is no easy way to run everything in the base method except adding
                // the default block description.
                //
                // The default block description is also used to show the block info of the item when it is in the
                // inventory. So simply removing the default block description is also not an option.
                //
                // So instead let the base class add everything, then remove the default block description.
                string descLangCode = Code.Domain + AssetLocation.LocationSeparator + ItemClass.ToString().ToLowerInvariant() + "desc-" + Code.Path;
                string desc = Lang.GetMatching(descLangCode);
                baseText = baseText.Replace(desc, "").TrimEnd();

                baseText = "Code: " + this.Code.ToString() + Environment.NewLine + baseText;
            }
            return baseText;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false; // only block if we can't interact via permissions with this block
            }
            blockSel.Block = this;
            if (base.OnWireInteractionStart(world, byPlayer, blockSel)) return true;
            BlockEntity machEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (machEntity != null)
            {
                if (machEntity is BlockEntityOpenableContainer)
                {
                    // allows the GUI to be opened
                    (machEntity as BlockEntityOpenableContainer).OnPlayerRightClick(byPlayer, blockSel);
                }
                return true;
            }
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

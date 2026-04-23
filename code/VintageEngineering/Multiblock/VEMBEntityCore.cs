using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VintageEngineering.Multiblock
{
    /// <summary>
    /// The Core BlockEntity of Multiblock machines
    /// </summary>
    public class VEMBEntityCore : ElectricContainerBE
    {
        public VEMultiblockBeh Multiblock { get { return this.Block.GetBehavior<VEMultiblockBeh>(); } }

        internal int activelayer = 0;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }
        /// <summary>
        /// Universal Multiblock check on the Variant of the block, "built" means its a fully formed machine ready for action.
        /// </summary>
        public bool IsBuilt => base.Block.Variant["state"] == "built";
        public override InventoryBase Inventory => throw new NotImplementedException();

        public override string InventoryClassName => throw new NotImplementedException();

        /// <summary>
        /// Called when a MB is formed or broken. Override to add validation.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="caller"></param>
        /// <param name="blockSel"></param>
        /// <param name="activationArgs"></param>
        public virtual void ActivateCore(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
        {

        }

        /// <summary>
        /// Passed from the event in the Block, useful for triggering GUI's, validation, or inventory management.
        /// </summary>
        /// <param name="byPlayer"></param>
        /// <param name="blockSel"></param>
        /// <returns>Return true to process event on server and sync.</returns>
        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("activelayer", activelayer);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            activelayer = tree.GetInt("activelayer", 0);
        }
    }
}

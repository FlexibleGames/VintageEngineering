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

        public override InventoryBase Inventory => throw new NotImplementedException();

        public override string InventoryClassName => throw new NotImplementedException();

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

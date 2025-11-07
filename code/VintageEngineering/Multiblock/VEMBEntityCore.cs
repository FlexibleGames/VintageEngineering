using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using Vintagestory.API.Common;

namespace VintageEngineering.Multiblock
{
    /// <summary>
    /// The Core BlockEntity of Multiblock machines
    /// </summary>
    public class VEMBEntityCore : ElectricContainerBE
    {
        public MultiblockStructure mbs = new MultiblockStructure();

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            mbs = base.Block.Attributes["multiblockStructure"]?.AsObject<MultiblockStructure>();
            int rotYDeg = 0;
            switch (base.Block.Variant["side"])
            {
                case "East": rotYDeg = 270; break;
                case "South": rotYDeg = 180; break;
                case "West": rotYDeg = 90; break;
                default: break;
            }
            mbs?.InitForUse(rotYDeg);
        }

        public override InventoryBase Inventory => throw new NotImplementedException();

        public override string InventoryClassName => throw new NotImplementedException();

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }
    }
}

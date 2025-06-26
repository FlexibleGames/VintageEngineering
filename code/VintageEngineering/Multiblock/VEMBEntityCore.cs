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
        public override InventoryBase Inventory => throw new NotImplementedException();

        public override string InventoryClassName => throw new NotImplementedException();

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using VintageEngineering.GUI;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VintageEngineering
{    
    public class BEBattery: ElectricSimpleBE
    {
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            Electric.MachineState = EnumBEState.On;
        }
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            Electric.MachineState = EnumBEState.On;
        }
        // A very simple block entity that doesn't need much other than an Electric interface
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine();
            double percentfull = ((double)Electric.CurrentPower / Electric.MaxPower);
            int wholepercent = (int)(percentfull * 100);
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {IconHelper.PercentToBar(wholepercent, 10)} {wholepercent:N0}%");
        }
    }
}

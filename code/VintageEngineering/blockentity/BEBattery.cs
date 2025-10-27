using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
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
            int numexes = wholepercent / 10;
            string exes = "";
            switch (numexes)
            {
                case 0: exes = "----------"; break;
                case 1: exes = "X---------"; break;
                case 2: exes = "XX--------"; break;
                case 3: exes = "XXX-------"; break;
                case 4: exes = "XXXX------"; break;
                case 5: exes = "XXXXX-----"; break;
                case 6: exes = "XXXXXX----"; break;
                case 7: exes = "XXXXXXX---"; break;
                case 8: exes = "XXXXXXXX--"; break;
                case 9: exes = "XXXXXXXXX-"; break;
                case 10: exes = "XXXXXXXXXX"; break;
                default: break;
            }

            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {exes} {wholepercent:N0}%");
        }
    }
}

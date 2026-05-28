using System;
using System.Text;
using VintageEngineering.Electrical;
using VintageEngineering.GUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageEngineering.blockentity
{
    public class BEMVTransformer : ElectricSimpleBE
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
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            
            dsc.AppendLine();
            double percentfull = (double)Electric.CurrentPower / Electric.MaxPower;
            int wholepercent = (int)(percentfull * 100);
            
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {IconHelper.PercentToBar(wholepercent, 10)} {wholepercent:N0}%");
        }       

        public int GetRotation()
        {
            string side = Block.Variant["side"];
            // The BlockFacing horiztonal index goes counter-clockwise from east. That needs to be converted so that
            // it goes counter-clockwise from north instead.
            int adjustedIndex = ((BlockFacing.FromCode(side)?.HorizontalAngleIndex ?? 1) + 3) & 3;
            return adjustedIndex * 90;
        }

    }
}

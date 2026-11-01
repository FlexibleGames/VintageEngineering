using System;
using System.Collections.Generic;
using System.Text;
using VintageEngineering.Electrical;
using VintageEngineering.GUI;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageEngineering
{
    public class BELVSolarPanel : ElectricSimpleBE
    {
        private long _listenerID = 0;
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            Electric.MachineState = EnumBEState.On;
            if (api.Side == EnumAppSide.Server)
            {
                _listenerID = RegisterGameTickListener(new Action<float>(OnSimTick), 1000, 1000);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            Electric.MachineState = EnumBEState.On;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            ClimateCondition thisPos = Api.World.BlockAccessor.GetClimateAt(Pos);
            float sunlight = Api.World.Calendar.GetDayLightStrength(Pos);
            float clouds = thisPos.RainCloudOverlay;
            float intensity = sunlight - (clouds / 2f);
            intensity = GameMath.Clamp(intensity, 0, 2);
            float powerpercent = 0f;
            if (Electric.MaxPower > 0) powerpercent = (float)(Electric.CurrentPower / (double)Electric.MaxPower);
            int percentpower = (int)(powerpercent * 100);

            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"Light at Pos: {intensity}");
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {IconHelper.PercentToBar(percentpower, 10)} {percentpower:N0}%");
        }

        public void OnSimTick(float dt)
        {
            ClimateCondition thisPos = Api.World.BlockAccessor.GetClimateAt(Pos);
            float sunlight = Api.World.Calendar.GetDayLightStrength(Pos);
            float clouds = thisPos.RainCloudOverlay;
            float intensity = sunlight - (clouds / 2f);
            intensity = GameMath.Clamp(intensity, 0, 2);
            if (intensity >= 0.5)
            {                
                // we have enough light to generate
                ulong tickpower = (ulong)(Electric.MaxPPS * dt * intensity);
                if (Electric.MaxPower <= Electric.CurrentPower)
                {
                    Electric.electricpower = Electric.MaxPower;
                    return;
                }
                ulong remaining = Electric.MaxPower - Electric.CurrentPower;
                if (tickpower > remaining) GameMath.Clamp(tickpower, 0, remaining);
                Electric.electricpower += tickpower;
            }
        }
    }
}

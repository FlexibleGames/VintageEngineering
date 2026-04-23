using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace VintageEngineering.API
{
    public class ItemLiquidFuel : Item
    {
        // why THE HELL is ItemLiquidPortion an internal class?
        public override void OnGroundIdle(EntityItem entityItem)
        {
            entityItem.Die(EnumDespawnReason.Removed, null);
            if (entityItem.World.Side == EnumAppSide.Server)
            {
                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(entityItem.Itemstack);
                float litres = (float)entityItem.Itemstack.StackSize / ((props != null) ? props.ItemsPerLitre : 1f);
                entityItem.World.SpawnCubeParticles(entityItem.Pos.XYZ, entityItem.Itemstack, 0.75f, (int)(litres * 2f), 0.45f, null, null);
                entityItem.World.PlaySoundAt(new AssetLocation("sounds/environment/smallsplash"), (double)((float)entityItem.Pos.X), (double)((float)entityItem.Pos.InternalY), (double)((float)entityItem.Pos.Z), null, true, 32f, 1f);
            }
            base.OnGroundIdle(entityItem);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (this.Attributes["liquidfuel"].Exists)
            {
                LiquidFuelProperties lfp = LiquidFuelProperties.FromJSON(this.Attributes["liquidfuel"]);
                dsc.AppendLine($"{Lang.Get("vinteng:gui-energypersec")}: {lfp.EnergykJs:N1}");
                dsc.AppendLine($"{Lang.Get("vinteng:gui-burntime")}: {lfp.Duration:N1}");
                dsc.AppendLine($"{Lang.Get("vinteng:gui-burntemp")}: {lfp.BurnTemp:N0}°C");
                dsc.AppendLine($"{Lang.Get("vinteng:gui-soot")}: {lfp.Soot:N1}");
                float totalpower = (lfp.EnergykJs * 100) * lfp.Duration;
                dsc.AppendLine($"{Lang.Get("vinteng:gui-totalpower")}: {totalpower:N1}");
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }
}

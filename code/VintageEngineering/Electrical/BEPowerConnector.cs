using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical
{
    public class BEPowerConnector : ElectricSimpleBE
    {
        private float clientUpdateDelay = 0f;
        private BlockPos attachedTo;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                if (base.ListenerID > 0) api.Event.UnregisterGameTickListener(ListenerID);
                base.ListenerID = 0;
                ListenerID = api.Event.RegisterGameTickListener(new Action<float>(OnPowerTick), 250, 500);
            }
            attachedTo = this.Pos.AddCopy(BlockFacing.FromCode(this.Block.Variant["position"]).Opposite);
        }

        public void OnPowerTick(float deltatime)
        {


            clientUpdateDelay += deltatime;
            if (clientUpdateDelay > 0.5f) MarkDirty(true);
        }
    }
}

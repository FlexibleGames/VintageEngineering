using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.blockBhv;
using VintageEngineering.Electrical;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.ServerMods.NoObf;

namespace VintageEngineering.blockentity
{
    public class BEElectricKinetic : ElectricSimpleBE
    {

        public bool isGenerator { get { return Block.Code.Path.Contains("alternator"); } }

        public ElectricBEBehavior Electricity;

        ElectricKineticMotorBhv genBhv;

        ElectricKineticAlternatorBhv consBhv;
        private float sleepTimer = 0;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side != EnumAppSide.Client)
            {
                RegisterGameTickListener(OnCommonTick, 100, 0);
            } 
        }
        public void OnCommonTick(float dt)
        {
            if(Api.Side == EnumAppSide.Client) { return; }
            if (!Electric.IsEnabled) return;
            if (Electric.IsSleeping)
            {
                if (sleepTimer < 2f) { sleepTimer += dt; return; }
                else sleepTimer = 0;
            }

            if(!isGenerator)
            {
                float PPT = Electric.MaxPPS * dt;
                if (Electric.CurrentPower == 0 || Electric.CurrentPower < PPT) { return; }
                if(genBhv != null)
                {
                    Single powerwanted = genBhv.getPowReq();
                    if (Electric.CurrentPower < powerwanted)
                    {
                        genBhv.ConsumePower(Electric.CurrentPower);
                        Electric.electricpower = 0;
                    }
                    else
                    {
                        Single consumed = genBhv.getPowReq();
                        genBhv.ConsumePower(consumed);
                        Electric.electricpower -= (ulong)Math.Round(consumed);
                    }
                }
            }
            if(isGenerator)
            {
                if (consBhv != null)
                {
                    float powermade = consBhv.ProducePower();
                    Electric.electricpower += (ulong)powermade;
                    if (Electric.CurrentPower > Electric.MaxPower) Electric.electricpower = Electric.MaxPower;
                }
            }

        }

        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);
            consBhv = GetBehavior<ElectricKineticAlternatorBhv>();
            genBhv = GetBehavior<ElectricKineticMotorBhv>();
        }

    }
}

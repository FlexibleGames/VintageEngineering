using System;
using VintageEngineering.blockBhv;
using VintageEngineering.Electrical;
using Vintagestory.API.Common;


namespace VintageEngineering.blockentity
{
    public class BEElectricKinetic : ElectricSimpleBE
    {

        public bool isGenerator { get { return Block.Code.Path.Contains("alternator"); } }

        //public ElectricBEBehavior Electricity;

        private long _clientUpdateMS = 0L;

        private ElectricKineticMotorBhv genBhv;
        private ElectricKineticAlternatorBhv consBhv;

        private float sleepTimer = 0;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnSimTick, 100, 0);
            }
            _clientUpdateMS = api.World.ElapsedMilliseconds;
        }
        public void OnSimTick(float dt)
        {
            //if(Api.Side == EnumAppSide.Client) { return; }
            if (!Electric.IsEnabled) return;
            if (Electric.IsSleeping)
            {
                if (sleepTimer < 2f) { sleepTimer += dt; return; }
                else sleepTimer = 0;
            }

            if (isGenerator)
            {
                if (consBhv != null)
                {
                    float powermade = consBhv.ProducePower();
                    Electric.electricpower += (ulong)powermade;
                    if (Electric.CurrentPower > Electric.MaxPower) Electric.electricpower = Electric.MaxPower;
                }
            }
            else
            {
                float PPT = Electric.MaxPPS * dt;
                if (Electric.CurrentPower == 0 || Electric.CurrentPower < PPT) { return; }
                if(genBhv != null)
                {
                    Single powerwanted = genBhv.GetMechanicalPowerRequired();
                    if (Electric.CurrentPower < powerwanted)
                    {
                        // if this is out of power, then it should just stop providing mechanical power
                        genBhv.ConsumePower(Electric.CurrentPower);
                        Electric.electricpower = 0;
                    }
                    else
                    {
                        Single consumed = genBhv.GetMechanicalPowerRequired();
                        genBhv.ConsumePower(consumed);
                        Electric.electricpower -= (ulong)Math.Round(consumed);
                    }
                }
            }
            // update client values every half second
            if (Api.World.ElapsedMilliseconds - _clientUpdateMS > 500L)
            {
                _clientUpdateMS = Api.World.ElapsedMilliseconds;
                MarkDirty(true);
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

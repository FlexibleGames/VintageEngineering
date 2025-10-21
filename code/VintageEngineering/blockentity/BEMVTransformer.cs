using System;
using System.Text;
using VintageEngineering.Electrical;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageEngineering.blockentity
{
    public class BEMVTransformer : ElectricSimpleBE
    {
        public bool IsPowered { get; set; } = false;
        public bool IsActive { get; set; } = true;

        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private float _updateBouncer = 0f;
        private long _clientUpdateMS = 0L;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 250, 0);
                if (Electric.CurrentPower > 0) IsPowered = true;
                if (IsActive || IsPowered) SetState(EnumBEState.On);
                else SetState(EnumBEState.Sleeping);
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (Electric.AnimUtil != null)
                {
                    Electric.AnimUtil.InitializeAnimator("vemvtransformer", null, null, new Vec3f(0, GetRotation(), 0f));
                }
            }
            _clientUpdateMS = api.World.ElapsedMilliseconds;
        }

        public void OnSimTick(float dt)
        {
            if (Api.World.ElapsedMilliseconds - _clientUpdateMS > 500L)
            {
                _clientUpdateMS = Api.World.ElapsedMilliseconds;
                MarkDirty(true);
            }

            if (Electric.MachineState == EnumBEState.Sleeping)
            {
                _updateBouncer += dt;
                if (_updateBouncer < 2f)
                {
                    return;
                }
                _updateBouncer = 0f;
            }

            if (Electric.CurrentPower > 0 && Electric.MachineState != EnumBEState.On)
            {
                IsPowered = true;
                IsActive = true;
                SetState(EnumBEState.On);
            }
            if (Electric.CurrentPower == 0)
            {
                IsPowered = false;
                IsActive = false;
                SetState(EnumBEState.Sleeping);
            }
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            
            dsc.AppendLine();
            double percentfull = Electric.CurrentPower / Electric.MaxPower;
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
                case 10: exes ="XXXXXXXXXX"; break;
                default: break;
            }
            
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {exes} {wholepercent:N0}%");            
        }

        public void SetState(EnumBEState state)
        {
            if (Electric.MachineState != state)
            {
                // only process fully if state is actually changing
                Electric.MachineState = state;
                if (state != EnumBEState.On)
                {
                    IsActive = false;
                }
                else
                {
                    IsActive = true;
                }
            }
            if (Electric.MachineState == EnumBEState.On && IsPowered)
            {
                if (Electric.AnimUtil != null && base.Block.Attributes["craftinganimcode"].Exists)
                {
                    Electric.AnimUtil.StartAnimation(new AnimationMetaData
                    {
                        Animation = base.Block.Attributes["craftinganimcode"].AsString(),
                        Code = base.Block.Attributes["craftinganimcode"].AsString(),
                        AnimationSpeed = 1f,
                        EaseOutSpeed = 4f,
                        EaseInSpeed = 1f
                    });
                }
            }
            else
            {
                if (Electric.AnimUtil != null && Electric.AnimUtil.activeAnimationsByAnimCode.Count > 0)
                {
                    Electric.AnimUtil.StopAnimation(base.Block.Attributes["craftinganimcode"].AsString());
                }
            }
            MarkDirty(true);
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

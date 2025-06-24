using System;
using VintageEngineering.blockentity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

namespace VintageEngineering.blockBhv
{
    public class ElectricKineticMotorBhv : BEBehaviorMPBase
    {
        //how much torqe we are providing
        private float _torque;
        //how fast are we going
        private float _speedSet;
        //how much power do we want to be getting
        private float electricPowerRequired;

        //How much power (per tick) is needed to turn at all
        private static float Power_Min = 1f;
        /// <summary>
        /// How much power (per tick) is needed to turn at fastest + max torque
        /// </summary>
        private static float PowerAtMax = 250f;
        //How much torq will be provided at max
        //private static float resistance_Max = 1f;
        //How fast can be at max, vanilla 0-1
        private static float speed_max = 1f;
        //How high should we clamp _torque?
        private static float torque_max = 5f;

        /// <summary>
        /// Given the set speed and resistance settings how much electrical power is required
        /// to keep the motor spinning.
        /// </summary>
        public ulong ElectricalPowerRequired
        {
            get
            {
                return (ulong)electricPowerRequired;
            }
        }

        public ElectricKineticMotorBhv(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
        }

        public override BlockFacing OutFacingForNetworkDiscovery
        {
            get
            {
                if (Blockentity is BEElectricKinetic entity)
                {
                    return BlockFacing.FromCode(entity.Block.LastCodePart());
                }

                return BlockFacing.NORTH;
            }
        }
        protected MeshData BaseMesh()
        {
            return ObjectCacheUtil.GetOrCreate(Api, "motor-base" + Block.Shape.rotateY, () =>
            {
                AssetLocation path = AssetLocation.Create("vinteng:block/lv/motor/motor-base").WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
                Shape shape = Api.Assets.TryGet(path).ToObject<Shape>();
                (Api as ICoreClientAPI).Tesselator.TesselateShape(Block, shape, out MeshData mesh, new Vec3f(0, Block.Shape.rotateY, 0));
                return mesh;
            });
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(BaseMesh());
            return base.OnTesselation(mesher, tesselator);
        }

        public override int[] AxisSign => OutFacingForNetworkDiscovery.Index switch
        {
            0 => new[]
            {
            +0,
            +0,
            -1
        },
            1 => new[]
            {
            -1,
            +0,
            +0
        },
            2 => new[]
            {
            +0,
            +0,
            -1
        },
            3 => new[]
            {
            -1,
            +0,
            +0
        },
            4 => new[]
            {
            +0,
            -1,
            +0
        },
            5 => new[]
            {
            +0,
            +1,
            +0
        },
            _ => this.AxisSign
        };

        public override float GetTorque(long tick, float speed, out float resistance)
        {
            _torque = 0f;
            resistance = GetResistance(); // now a flat 0.003, same as a windmill rotor.
            _speedSet = 0;
            //Dangerous cast, DC,DA.
            float powAmnt = (Blockentity as BEElectricKinetic).Electric.CurrentPower;

            float direction = (this.propagationDir == this.OutFacingForNetworkDiscovery) ? 1f : -1f;

            // speed is 0 -> 1
            float l_curspeedsetting = (Blockentity as BEElectricKinetic)?.SpeedSetting ?? 0f;
            // _torque is 0 -> 5
            float l_curtorquesetting = (Blockentity as BEElectricKinetic)?.TorqueSetting ?? 0f;
            // if either of these are 0, it returns 0 so ensure we do that.
            if (l_curspeedsetting == 0 || l_curtorquesetting == 0) return 0;

            // powAmnt goes to 2000, so this is 0 -> 200 compared to speed which is 0 -> 1
            // ?? only when power is between 10 and 11 would this make any difference.
            _speedSet = powAmnt > 0 ? l_curspeedsetting : 0f;
            
            // base game Windmill rotor max TorqueFactor is 1.25
            // wind speed is 0 -> 1
            // GetTorque returns speed * TorqueFactor.
            _torque = (_speedSet * l_curtorquesetting);

            GetElectricalPowerRequired();

            // if power is < 10, don't try to turn at all... 
            if (powAmnt <= Power_Min) { return 0f; }

            if ((Blockentity as BEElectricKinetic).Electric.MachineState == EnumBEState.Off) return 0f;

            return propagationDir == OutFacingForNetworkDiscovery ? _torque : -_torque;
        }        

        /// <summary>
        /// How much electrical power (per tick) this object needs
        /// </summary>
        /// <returns>The power (per tick) it wants, in electrical units</returns>
        public float GetElectricalPowerRequired()
        {
            // speed is 0.00 -> 1.00
            // _torque is 0.0 -> 5.0
            if (_speedSet == 0 || _torque == 0)
            {
                electricPowerRequired = 0;
                return 0;
            }

            // a max value of 5 = 4 windmill rotors

            //float speedFactor = MathF.Pow(_speedSet, 1.5f); // Power of 1.5 for hyperbolic curve
            //float torqueFactor = MathF.Pow(normalizedtorque, 1.5f); // Power of 1.5 for hyperbolic curve

            float normalizedtorque = _torque / 5; 
            
            float l_speedpower = _speedSet * 250f;
            float l_torquepower = normalizedtorque * 250f;

            float output = (l_speedpower/2) + (l_torquepower/4);
            output = Math.Clamp(output, Power_Min, PowerAtMax);
            
            // this is sent to the client for GUI use.
            electricPowerRequired = output;

            return output;
        }

        public override float GetResistance()
        {
            return 0.003f;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("torque", _torque);
            tree.SetFloat("speed", _speedSet);
            tree.SetFloat("powerrequired", electricPowerRequired);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _torque = tree.GetFloat("torque", 0f);
            _speedSet = tree.GetFloat("speed", 0f);
            electricPowerRequired = tree.GetFloat("powerrequired", 0f);
        }
    }

    public class ElectricKineticAlternatorBhv : BEBehaviorMPBase
    {
        //The max power generated at full speed
        private float max_Output { get => (Blockentity as BEElectricKinetic).Electric.MaxPPS; }
        //How fast IS max speed? Base wind-speed is 0-1
        private static float speed_max = 0.8f;
        //How much is added to resistance when doing something
        private static float res_Fac = 0.125f;
        //add this much resistance per 100% power over speed_max
        private static float res_Load = 0.25f;
        //How much do we consume doing literally nothing
        private static float base_res = 0f;

        public ElectricKineticAlternatorBhv(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override BlockFacing OutFacingForNetworkDiscovery
        {
            get
            {
                if (Blockentity is BEElectricKinetic entity)
                {
                    return BlockFacing.FromCode(entity.Block.LastCodePart());
                }

                return BlockFacing.NORTH;
            }
        }
        public override int[] AxisSign => OutFacingForNetworkDiscovery.Index switch
        {
            0 => new[]
            {
            +0,
            +0,
            -1
        },
            1 => new[]
            {
            -1,
            +0,
            +0
        },
            2 => new[]
            {
            +0,
            +0,
            -1
        },
            3 => new[]
            {
            -1,
            +0,
            +0
        },
            4 => new[]
            {
            +0,
            -1,
            +0
        },
            5 => new[]
            {
            +0,
            +1,
            +0
        },
            _ => this.AxisSign
        };
        protected MeshData BaseMesh()
        {
            return ObjectCacheUtil.GetOrCreate(Api, "motor-base" + Block.Shape.rotateY, () =>
            {
                AssetLocation path = AssetLocation.Create("vinteng:block/lv/motor/motor-base").WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
                Shape shape = Api.Assets.TryGet(path).ToObject<Shape>();
                (Api as ICoreClientAPI).Tesselator.TesselateShape(Block, shape, out MeshData mesh, new Vec3f(0, Block.Shape.rotateY, 0));
                return mesh;
            });
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(BaseMesh());
            return base.OnTesselation(mesher, tesselator);
        }
        /// <summary>
        /// Returns how much power this is producing given the network speed<br/>
        /// This requires a lot of work.
        /// </summary>
        /// <returns>Power Produced</returns>
        public float GetPowerProduced()
        {
            float spd = network?.Speed * GearedRatio ?? 0f;
            float pow = Math.Abs(spd) / speed_max * max_Output;

            return pow;
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
        }

        public override float GetResistance()
        {
            float spd = Math.Abs(network?.Speed * GearedRatio ?? 0f);
            float resistance = (float)(base_res + ((spd > speed_max)
                ? res_Load + ((res_Fac*2) * (spd/speed_max))
                : res_Load + (res_Fac * (spd / speed_max))));

            return resistance;
        }
    }
}

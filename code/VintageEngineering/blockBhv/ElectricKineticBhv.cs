using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.blockentity;
using VintageEngineering.Blocks;
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

        private float torque;
        private float IVal;
        private float powRec;
        private float powReqed;

        private static float Ins_Min = 10f;
        private static float Ins_Max= 100f;
        private static float torq_Max = 1f;
        private static float kpdMax = 0.85f;
        private static float speed_max = 0.5f;
        private static float res_factor = 0.25f;


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
                var path = AssetLocation.Create("vinteng:block/lv/motor/motor-base").WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
                var shape = Api.Assets.TryGet(path).ToObject<Shape>();
                (Api as ICoreClientAPI).Tesselator.TesselateShape(Block, shape, out var mesh, new Vec3f(0, Block.Shape.rotateY, 0));
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

        public void ConsumePower(float amnt)
        {
            powRec = amnt;
        }

        public override float GetTorque(long tick, float speed, out float resistance)
        {
            torque = 0f;
            resistance = GetResistance();
            IVal = 0;
            //Dangerous cast, DC,DA.
            float powAmnt = (Blockentity as BEElectricKinetic).Electric.CurrentPower;

            if(powAmnt <= Ins_Min) { return torque; }
            IVal = Math.Min(powAmnt, Ins_Max);

            torque = IVal / Ins_Max * torq_Max;

            torque *= kpdMax;

            powReqed = Ins_Max;

            return propagationDir == OutFacingForNetworkDiscovery ? torque : -torque;
        }

        public float getPowRec()
        {
            return powRec;
        }

        public float getPowReq()
        {
            return powReqed;
        }

        public override float GetResistance()
        {
            var spd = Math.Abs(Network?.Speed * GearedRatio ?? 0f);
            float base_res = 0.05f;

            return base_res + Math.Abs((spd>speed_max) 
                ? res_factor * (float)Math.Pow(spd / speed_max, 2f)
                : res_factor * spd / speed_max);

        }


    }

    public class ElectricKineticAlternatorBhv : BEBehaviorMPBase
    {

        private float powGive;
        private static float I_Max = 20f;
        private static float speed_max = 0.75f;
        private static float res_Fac = 0.125f;
        private static float res_Load = 0.5f;
        private static float base_res = 0f;
        private static float kpd_max = 0.95f;


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
                var path = AssetLocation.Create("vinteng:block/lv/motor/motor-base").WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
                var shape = Api.Assets.TryGet(path).ToObject<Shape>();
                (Api as ICoreClientAPI).Tesselator.TesselateShape(Block, shape, out var mesh, new Vec3f(0, Block.Shape.rotateY, 0));
                return mesh;
            });
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(BaseMesh());
            return base.OnTesselation(mesher, tesselator);
        }
        public float ProducePow()
        {
            float spd = network?.Speed * GearedRatio ?? 0f;
            float pow = (Math.Abs(spd) <= speed_max)
            ? Math.Abs(spd) / speed_max * I_Max
            : I_Max;

            powGive = pow;
            return pow;
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
        }

        public override float GetResistance()
        {

            var spd = Math.Abs(network?.Speed * GearedRatio ?? 0f);
            var resistance = (float)(base_res + ((spd > speed_max)
                ? res_Load + (res_Fac * (Math.Pow(spd/speed_max,2f)))
                : res_Load + (res_Fac * spd / speed_max)));

            resistance /= kpd_max;

            return resistance;
        }
    }
}

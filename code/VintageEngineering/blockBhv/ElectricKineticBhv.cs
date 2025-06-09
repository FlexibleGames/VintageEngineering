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
        //how much torqe we are providing
        private float torque;
        //how fast are we going
        private float speedSet;
        //how much power are we getting, this is not actually used anywhere...?
        private float mechpowerReceived;
        //how much power do we want to be getting
        private float mechpowerRequested;

        //How much power is needed to turn at all
        private static float Ins_Min = 10f;
        //How much power is needed to turn at fastest + max resistance
        private static float Ins_Max= 250f;
        //How much torq will be provided at max
        private static float resistance_Max = 1f;
        //How fast can be at max, vanilla 0-1
        private static float speed_max = 1f;


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

        public void ConsumePower(float amnt)
        {
            mechpowerReceived = amnt;
        }

        public override float GetTorque(long tick, float speed, out float resistance)
        {
            torque = 0f;
            resistance = GetResistance();
            speedSet = 0;
            //Dangerous cast, DC,DA.
            float powAmnt = (Blockentity as BEElectricKinetic).Electric.CurrentPower;

            if(powAmnt <= Ins_Min) { return torque; }
            //todo: change with GUI
            speedSet = Math.Min(powAmnt/10, speed_max);

            torque = (speedSet * resistance) * 1.25f;

            mechpowerRequested = Math.Min(speedSet + (resistance * 1.5f)*100f,Ins_Max);

            return propagationDir == OutFacingForNetworkDiscovery ? torque : -torque;
        }

        /// <summary>
        /// How much kinetic power this object got last
        /// </summary>
        /// <returns>The power got last, in electrical units</returns>
        public float GetMechanicalPowerReceived()
        {
            return mechpowerReceived;
        }

        /// <summary>
        /// How much kinetic power this object wants
        /// </summary>
        /// <returns>The power it wants, in electrical units</returns>
        public float GetMechanicalPowerRequired()
        {
            return mechpowerRequested;
        }

        //TODO: Replace with GUI
        public override float GetResistance()
        { 
            return Math.Max(1f,resistance_Max);
        }
    }

    public class ElectricKineticAlternatorBhv : BEBehaviorMPBase
    {
        //The max power generated at full speed
        private static float max_Output = 80f;
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
        /// This feels like it's important enough to need serious documentation
        /// </summary>
        /// <returns>a float</returns>
        public float ProducePower()
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

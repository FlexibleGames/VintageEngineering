using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// Base Behavior class for a Wire Connectable Block Entity, <u>stores placed wires.</u>
    /// <br>WiresStart holds array of wire connections starting at this pos</br>
    /// <br>WiresEnd holds array of wire connections whos endpoint is this pos</br>
    /// <br>NetworkIDs holds a Dictionary of selectionindexes mapped to networkids.</br>
    /// <br>Rendering/Tesselation is only processed on the WiresStart array.</br>
    /// </summary>
    public class BEBehaviorWire : BlockEntityBehavior, IWireNetwork
    {
        /// <summary>
        /// Outgoing PlacedWire connections
        /// <br>All processing uses this array only, WiresEnd is for efficient network processing.</br>
        /// </summary>
        public PlacedWire[] WiresStart;

        /// <summary>
        /// Incoming PlacedWire connections
        /// </summary>
        public PlacedWire[] WiresEnd;
        private CatenaryMod cm;
        
        private Cuboidf[] collBoxes;

        private long[] networkIDs;

        public BEBehaviorWire(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            /// This method is called right after the block entity was spawned or right after it was loaded 
            ///     from a newly loaded chunk. You do have access to the world and its blocks at this point.
            /// However if this block entity already existed then FromTreeAttributes is called first!
            base.Initialize(api, properties);
            if (api.Side == EnumAppSide.Client)
            {
                cm = api.ModLoader.GetModSystem<CatenaryMod>(true);                
            }
            IWireAnchor baseblock = api.World.BlockAccessor.GetBlock(Pos) as IWireAnchor;
            if (baseblock != null && networkIDs == null)
            {
                networkIDs = new long[baseblock.NumAnchorsInBlock(EnumWireFunction.Any)];
            }
            if (this.WiresStart != null)
            {
                foreach(PlacedWire wire in this.WiresStart)
                {
                    wire.Block = Api.World.GetBlock(wire.BlockId);
                    networkIDs[wire.StartSelectionIndex] = wire.NetworkID;
                }
            }
            if (this.WiresEnd != null)
            {
                foreach (PlacedWire wire in WiresEnd)
                {
                    wire.Block = Api.World.GetBlock(wire.BlockId);
                    networkIDs[wire.EndSelectionIndex] = wire.NetworkID;
                }
            }
        }

        /// <summary>
        /// Add a new Wire Connection to this location.
        /// <br>Will also automatically change network ID of selectionIndex anchor.</br>
        /// </summary>
        /// <param name="newWire">Wire Connection</param>
        /// <param name="isEndPoint">Add as incoming connection.</param>
        public virtual void AddWire(PlacedWire newWire, bool isEndPoint = false)
        {
            if (!isEndPoint)
            {
                if (this.WiresStart == null)
                {
                    WiresStart = new PlacedWire[0];
                }
                WiresStart.Append(newWire);
                this.networkIDs[newWire.StartSelectionIndex] = newWire.NetworkID;
            }
            else
            {
                if (WiresEnd == null) WiresEnd = new PlacedWire[0];
                WiresEnd.Append(newWire);
                this.networkIDs[newWire.EndSelectionIndex] = newWire.NetworkID;
            }
            collBoxes = null;
            this.Blockentity.MarkDirty(true);
        }

        /// <summary>
        /// Remove a wire connection
        /// <br>Does not modify NetworkIDs of the Anchor!</br>
        /// </summary>
        /// <param name="thewire">Wire Connection to remove</param>
        /// <param name="atEndPoint">True to remove from incoming connections.</param>
        public virtual void RemoveWire(PlacedWire thewire, bool atEndPoint = false)
        {
            if (!atEndPoint)
            {
                if (this.WiresStart != null && WiresStart.Length > 0)
                {
                    WiresStart = this.WiresStart.Remove(thewire);
                }
            }
            else
            {
                if (this.WiresEnd != null && WiresEnd.Length > 0)
                {
                    WiresEnd = this.WiresEnd.Remove(thewire);
                }
            }
            collBoxes = null;
            this.Blockentity.MarkDirty(true);
        }

        /// <summary>
        /// Returns the array of PlacedWire collision boxes at this location.
        /// <br>Used for removing the wire.</br>
        /// </summary>
        /// <returns>Cuboidf Array</returns>
        public Cuboidf[] GetWireCollisionBoxes()
        {
            if (WiresStart == null && WiresEnd == null) return null;
            if (this.collBoxes != null) return collBoxes;
            
            float size = 0.25f;
            List<Cuboidf> cuboids = new List<Cuboidf>();
            //Cuboidf[] cuboids = new Cuboidf[(WiresStart.Length + WiresEnd.Length) * 2];

            // first we add the start wires
            for (int i = 0; i < this.WiresStart.Length; i++)
            {
                PlacedWire wire = WiresStart[i];
                cuboids.Add(new Cuboidf(wire.Start.X - size, wire.Start.Y - size, wire.Start.Z - size, wire.Start.X + size, wire.Start.Y + size, wire.Start.Z + size));
                //cuboids.Add(new Cuboidf(wire.End.X - size, wire.End.Y - size, wire.End.Z - size, wire.End.X + size, wire.End.Y + size, wire.End.Z + size));
                //cuboids[2 * i] = new Cuboidf(wire.Start.X - size, wire.Start.Y - size, wire.Start.Z - size, wire.Start.X + size, wire.Start.Y + size, wire.Start.Z + size);
                //cuboids[2 * i + 1] = new Cuboidf(wire.End.X - size, wire.End.Y - size, wire.End.Z - size, wire.End.X + size, wire.End.Y + size, wire.End.Z + size);
            }
            // then we add the incoming wires
            for (int i = 0; i < this.WiresEnd.Length; i++)
            {
                PlacedWire wire = WiresEnd[i];
                //cuboids.Add(new Cuboidf(wire.Start.X - size, wire.Start.Y - size, wire.Start.Z - size, wire.Start.X + size, wire.Start.Y + size, wire.Start.Z + size));
                cuboids.Add(new Cuboidf(wire.End.X + size, wire.End.Y + size, wire.End.Z + size, wire.End.X - size, wire.End.Y - size, wire.End.Z - size));
                //cuboids[2 * i] = new Cuboidf(wire.Start.X - size, wire.Start.Y - size, wire.Start.Z - size, wire.Start.X + size, wire.Start.Y + size, wire.Start.Z + size);
                //cuboids[2 * i + 1] = new Cuboidf(wire.End.X - size, wire.End.Y - size, wire.End.Z - size, wire.End.X + size, wire.End.Y + size, wire.End.Z + size);
            }

            collBoxes = cuboids.ToArray();
            return collBoxes;
        }

        /// <summary>
        /// Override to Return the networkID associated with selectionbox index    
        /// <br>Returns long.MinValue if selectionIndex > number of anchors.</br>
        /// </summary>
        /// <param name="selectionIndex">SelectionBox index</param>
        /// <returns>NetworkID (long)</returns>
        public virtual long GetNetworkID(int selectionIndex)
        {                        
            if (selectionIndex >= networkIDs.Length) return long.MinValue;
            return networkIDs[selectionIndex];
        }

        public virtual bool SetNetworkID(long networkID, int selectionIndex = 0)
        {
            if (selectionIndex > networkIDs.Length) return false;
            networkIDs[selectionIndex] = networkID;
            this.Blockentity.MarkDirty(true, null);
            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            byte[] startbytes = tree.GetBytes("wiresstart", null);
            byte[] endbytes = tree.GetBytes("wiresend", null);
            byte[] netids = tree.GetBytes("networkids", null);
            if (startbytes != null)
            {
                this.WiresStart = SerializerUtil.Deserialize<PlacedWire[]>(startbytes);
                if (this.Api != null && WiresStart != null)
                {
                    foreach (PlacedWire wire in WiresStart)
                    {
                        wire.Block = Api.World.GetBlock(wire.BlockId);
                    }
                }                
            }
            if (endbytes != null)
            {
                this.WiresEnd = SerializerUtil.Deserialize<PlacedWire[]>(endbytes);
                if (this.Api != null && WiresEnd != null)
                {
                    foreach (PlacedWire wire in WiresEnd)
                    {
                        wire.Block = Api.World.GetBlock(wire.BlockId);
                    }
                }
            }
            if (netids != null)
            {
                this.networkIDs = SerializerUtil.Deserialize<long[]>(netids);
            }
            collBoxes = null;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (this.WiresStart != null)
            {
                tree.SetBytes("wiresstart", SerializerUtil.Serialize<PlacedWire[]>(WiresStart));
            }
            if (this.WiresEnd != null)
            {
                tree.SetBytes("wiresend", SerializerUtil.Serialize<PlacedWire[]>(WiresEnd));
            }
            if (this.networkIDs != null)
            {
                tree.SetBytes("networkids", SerializerUtil.Serialize<long[]>(networkIDs));
            }    
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);
            if (WiresStart == null) return;
            for (int i = 0; i < WiresStart.Length; i++)
            {
                AssetLocation code;
                if (oldBlockIdMapping.TryGetValue(WiresStart[i].BlockId, out code))
                {
                    Block block = worldForNewMappings.GetBlock(code);
                    if (block is null)
                    {
                        worldForNewMappings.Logger.Warning($"Cannot load wire blockid mapping @{this.Blockentity.Pos}, code {code} not found in block registry.");
                    }
                    else
                    {
                        WiresStart[i].BlockId = block.Id;
                        WiresStart[i].Block = block;
                    }
                }
                else
                {
                    worldForNewMappings.Logger.Warning($"Cannot load wire blockid mapping @ {Blockentity.Pos}, blockid {WiresStart[i].BlockId} not found in block registry.");
                }
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            if (this.WiresStart == null)
            {
                return;
            }
            for (int i = 0; i < this.WiresStart.Length; i++)
            {
                Block block = this.Api.World.GetBlock(this.WiresStart[i].BlockId);
                blockIdMapping[block.Id] = block.Code;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            // Called when the Chunk is redrawn
            // Only processing wires that START at this location
            if (this.WiresStart == null) return true;
            BlockPos pos = this.Blockentity.Pos;

            for (int i = 0; i < this.WiresStart.Length; i++)
            {
                PlacedWire wire = WiresStart[i];
                //if (wire.WireMeshData == null) tempmesh = WireMesh.MakeWireMesh(wire.Start, wire.End, wire.WireThickness);                
                if (wire.WireMeshData == null)
                {
                    MeshData tempmesh = cm.GetOrCreateWireMesh(wire.Block);
                    MeshData newmesh = null;
                    newmesh = ModSystemSupportBeamPlacer.generateMesh(wire.Start, wire.End, null, tempmesh, 0.5f);
                    WiresStart[i].WireMeshData = newmesh.Clone();
                }                

                mesher.AddMeshData(WiresStart[i].WireMeshData, 1);
            }
            return true;
        }

        /// <summary>
        /// What Wire Drops are generated if this block is broken
        /// </summary>
        /// <param name="byPlayer"></param>
        /// <returns>ItemStack array</returns>
        public virtual ItemStack[] GetDrops(IPlayer byPlayer)
        {
            List<ItemStack> drops = new List<ItemStack>();
            foreach (PlacedWire wire in this.WiresStart)
            {
                drops.Add(new ItemStack(wire.Block, (int)Math.Ceiling((double)wire.End.DistanceTo(wire.Start))));
            }
            foreach (PlacedWire wire in WiresEnd)
            {
                drops.Add(new ItemStack(wire.Block, (int)Math.Ceiling((double)wire.End.DistanceTo(wire.Start))));
            }
            return drops.ToArray();
        }
    }
}

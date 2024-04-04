using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    public class CatenaryRenderer : IRenderer, IDisposable
    {
        public double RenderOrder => 0.5;

        public int RenderRange => 50;

        private CatenaryMod cm;
        private ICoreClientAPI capi;
        private int chunksize;

        public Matrixf ModelMat = new Matrixf();
        public Dictionary<Vec3i, List<WireConnection>> ConnectionsPerChunk;

        public CatenaryRenderer(ICoreClientAPI c_api, CatenaryMod catenaryMod)
        {
            cm = catenaryMod;
            capi = c_api;
            chunksize = GlobalConstants.ChunkSize;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "catenarynetwork");
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            if (ConnectionsPerChunk == null) return;

            foreach (List<WireConnection> meshes in ConnectionsPerChunk.Values)
            {
                foreach (WireConnection connection in meshes)
                {
                    connection.WireMeshRef?.Dispose(); // dispose of all the meshref's in the entire thing.
                }                
            }
            ConnectionsPerChunk.Clear();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (ConnectionsPerChunk == null || ConnectionsPerChunk.Count == 0) return;
            if (stage != EnumRenderStage.Opaque) return;

            IRenderAPI rpi = capi.Render;
            IClientWorldAccessor worldAccess = capi.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            rpi.GlEnableCullFace();
            rpi.GLEnableDepthTest();
            
            // will use the light values at the players position for all rendered wires...
            IStandardShaderProgram prog = rpi.PreparedStandardShader(worldAccess.Player.Entity.Pos.AsBlockPos.X, 
                                                                worldAccess.Player.Entity.Pos.AsBlockPos.Y,
                                                                worldAccess.Player.Entity.Pos.AsBlockPos.Z);
            //prog.Use();            
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ModelMatrix = ModelMat.Values;

            Stopwatch sw = Stopwatch.StartNew();

            foreach (KeyValuePair<Vec3i, List<WireConnection>> conns in ConnectionsPerChunk)
            {
                Vec3d offset = new Vec3d(conns.Key.X * chunksize, conns.Key.Y * chunksize, conns.Key.Z * chunksize);
                foreach (WireConnection con in conns.Value)
                {
                    AssetLocation wiretexture = new AssetLocation(capi.World.GetBlock(con.BlockId).Attributes["texture"].ToString());
                    int textureid = rpi.GetOrLoadTexture(wiretexture);
                    rpi.BindTexture2d(textureid);
                    prog.ModelMatrix = ModelMat.Identity().Translate(
                        offset.X - camPos.X, offset.Y - camPos.Y, offset.Z - camPos.Z).Values;
                    rpi.RenderMesh(con.WireMeshRef);
                }
            }
            prog.Stop();

            sw.Stop();
            if (sw.ElapsedMilliseconds > 500) // more than a second to render the wires is insane...
            {
                this.capi.Logger.Warning($"Catenary Renderer Overloaded! Took {sw.ElapsedMilliseconds} to render {ConnectionsPerChunk.Values.Count} wires.");
            }
        }

        /// <summary>
        /// Rebuilds wire rendering data indexed on chunk position
        /// <br>Keeps wire connection objects to ensure proper texture gets used.</br>
        /// <br>If chunk_position is set and does not exist in the dataset, update will be skipped.</br>
        /// </summary>
        /// <param name="wireData">Catenary Data</param>
        /// <param name="chunk_position">[Optional] ChunkPosition</param>
        public void UpdateWireMeshes(CatenaryData wireData, Vec3i chunk_position = null) 
        {
            if (capi == null) return;
            IClientWorldAccessor world = capi.World;
            IBlockAccessor accessor = capi.World?.BlockAccessor;

            if (world == null || accessor == null) return; // sanity check

            if (chunk_position != null && !ConnectionsPerChunk.ContainsKey(chunk_position))
            {
                return; // if there are ZERO wire connections in the given chunk then skip the update.
                // this check ONLY happens if chunk_position isn't null
                // as this function is called EVERY time a chunk is created
            }


            //Dictionary<Vec3i, List<WireConnection>> ConPerChunk = new Dictionary<Vec3i, List<WireConnection>>();

            if (ConnectionsPerChunk != null) // we have data already
            {
                foreach (List<WireConnection> mesh in ConnectionsPerChunk.Values)
                {
                    foreach (WireConnection connection in mesh)
                    {
                        connection.WireMeshRef?.Dispose(); // clean it up
                    }                    
                }
                ConnectionsPerChunk.Clear(); // clear it
            }
            else
            {
                ConnectionsPerChunk = new Dictionary<Vec3i, List<WireConnection>>();
            }
            
            foreach (WireConnection conn in wireData.allConnections) // rebuild chunk-based wire meshes
            {
                IWireAnchor block1 = accessor.GetBlock(conn.NodeStart.blockPos) as IWireAnchor;
                IWireAnchor block2 = accessor.GetBlock(conn.NodeEnd.blockPos) as IWireAnchor;

                if (block1 == null || block2 == null) continue;                

                BlockPos blockposStart = conn.NodeStart.blockPos;
                Vec3i chunkpos = new Vec3i(blockposStart.X / chunksize, blockposStart.Y / chunksize, blockposStart.Z / chunksize);

                Vec3f pos1 = conn.NodeStart.blockPos.ToVec3f().AddCopy(-chunkpos.X * chunksize, -chunkpos.Y * chunksize, -chunkpos.Z * chunksize) + block1.GetAnchorPosInBlock(conn.NodeStart);
                Vec3f pos2 = conn.NodeEnd.blockPos.ToVec3f().AddCopy(-chunkpos.X * chunksize, -chunkpos.Y * chunksize, -chunkpos.Z * chunksize) + block2.GetAnchorPosInBlock(conn.NodeEnd);

                if (ConnectionsPerChunk.ContainsKey(chunkpos)) // if we already have wire in the chunk
                {
                    MeshData newMesh = WireMesh.MakeWireMesh(pos1, pos2, conn.WireThickness);
                    if (conn.WireMeshData != null)
                    {
                        conn.WireMeshData.Clear();
                    }
                    newMesh.SetMode(EnumDrawMode.Triangles);
                    conn.WireMeshData = newMesh;
                    conn.WireMeshRef = capi.Render.UploadMesh(conn.WireMeshData);
                    ConnectionsPerChunk[chunkpos].Add(conn); // add it
                }
                else
                {
                    // first wire in the chunk
                    MeshData newMesh = WireMesh.MakeWireMesh(pos1, pos2, conn.WireThickness);
                    if (conn.WireMeshData != null) { conn.WireMeshData.Clear(); }
                    newMesh.SetMode(EnumDrawMode.Triangles);
                    conn.WireMeshData = newMesh;
                    conn.WireMeshRef = capi.Render.UploadMesh(conn.WireMeshData);
                    ConnectionsPerChunk[chunkpos] = new List<WireConnection> { conn };
                }
            }
        }
    }
}

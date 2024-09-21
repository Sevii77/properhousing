using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Lumina.Models.Models;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using static ProperHousing.ProperHousing;

namespace ProperHousing;

public class CollisionScene {
	[Flags]
	public enum CollisionType: byte {
		None = 0,
		All = 0xFF,
		World = 0b01,
		Furniture = 0b10,
	}
	
	public unsafe struct RaycastResult {
		public bool Hit;
		public CollisionType HitType;
		public Vector3 HitPos;
		public Vector3 HitDir;
		public Furniture* HitObj;
		public int HitObjSubIndex;
		
		public RaycastResult() {
			Hit = false;
			HitPos = new Vector3();
			HitDir = new Vector3();
			HitType = CollisionType.None;
			HitObj = null;
			HitObjSubIndex = -1;
		}
	}
	
	private readonly float epsilon = 0.0000001f;
	private readonly string[] modelAffix = ["", "a", "b", "c", "d"];
	private readonly List<Mesh.MeshType> bannedMeshTypes = [
		Mesh.MeshType.LightShaft, // fuck you lightshafts, your the main reason i made this
	];
	
	private Dictionary<(byte, ushort), List<(List<Vector3[]>, (Vector3, Vector3))>> meshCache;
	private ExcelSheet<HousingFurniture>? houseSheet;
	private ExcelSheet<HousingYardObject>? houseSheetOutdoor;
	private ExcelSheet<TerritoryType>? territoryType;
	
	public CollisionScene() {
		meshCache = [];
		houseSheet = DataManager.GetExcelSheet<HousingFurniture>();
		houseSheetOutdoor = DataManager.GetExcelSheet<HousingYardObject>();
		territoryType = DataManager.GetExcelSheet<TerritoryType>();
	}
	
	public unsafe RaycastResult Raycast(Vector3 origin, Vector3 target, CollisionType collisionTypeWhitelist, nint[] furnitureItemBlacklist) {
		var result = new RaycastResult();
		
		var zone = housing->CurrentZone();
		if(zone == null)
			return result;
		
		if(layout->Manager == null)
			return result;
		
		var dir = Vector3.Normalize(target - origin);
		var distance = Vector3.Distance(origin, target);
		var curobj = IntPtr.Zero;
		if(collisionTypeWhitelist.HasFlag(CollisionType.World)) {
			var house = GetMesh((ushort)layout->HouseLayout->Territory);
			if(house != null)
				for(int i = 0; i < house.Count; i++)
					if(Collides(house[i], Vector3.Zero, Quaternion.Identity, ref origin, ref dir, distance, out var dist, out var hitdir) && dist < distance) {
						distance = dist;
						
						result.Hit = true;
						result.HitType = CollisionType.World;
						result.HitPos = origin + dir * dist;
						result.HitDir = hitdir;
						result.HitObjSubIndex = i;
					}
		}
		
		void CheckFurniture(Furniture* obj) {
			List<(List<Vector3[]>, (Vector3, Vector3))>? objmesh = null;
			try {
				objmesh = GetMesh(obj);
			} catch(Exception e) {
				Logger.Error(e, "Failed getting mesh");
			}
			
			if(objmesh == null)
				return;
			
			var segs = obj->ModelSegments();
			for(int i = 0; i < Math.Min(segs.Length, objmesh.Count); i++)
				if(Collides(objmesh[i], segs[i]->Position, segs[i]->Rotation, ref origin, ref dir, distance, out var dist, out var hitdir) && dist < distance) {
					distance = dist;
					
					result.Hit = true;
					result.HitType = CollisionType.Furniture;
					result.HitPos = origin + dir * dist;
					result.HitDir = hitdir;
					result.HitObj = obj;
					result.HitObjSubIndex = i;
				}
		}
		
		if(collisionTypeWhitelist.HasFlag(CollisionType.Furniture)) {
			var count = housing->IsOutdoor ? 40 : 400;
			for(int i = 0; i < count; i++) {
				var obj = zone->Furniture(i);
				if(obj == null || Array.IndexOf(furnitureItemBlacklist, (nint)obj->Item) >= 0)
					continue;
				
				CheckFurniture(obj);
			}
			
			if(!housing->IsOutdoor && housing->CurrentZone()->IndoorGhostObject != null && housing->CurrentZone()->IndoorActiveObject == null)
				CheckFurniture(housing->CurrentZone()->IndoorGhostObject);
		}
		
		return result;
	}
	
	public unsafe RaycastResult Raycast(Vector3 origin, Vector3 target) {
		return Raycast(origin, target, CollisionType.All, []);
	}
	
	private bool Collides((List<Vector3[]>, (Vector3, Vector3)) mesh, Vector3 pos, Quaternion rot, ref Vector3 origin, ref Vector3 dir, float range, out float distance, out Vector3 hitdir) {
		distance = range;
		hitdir = Vector3.Zero;
		
		var irot = Quaternion.Inverse(rot);
		// var scale = segs[segI]->Scale * obj->Item->Scale;
		var scale = Vector3.One;
		
		var bounds = mesh.Item2;
		var rotatedOrigin = Vector3.Transform(origin - pos, irot);
		var rotatedDir = Vector3.Transform(dir, irot);
		
		if(AABBIntersects(bounds.Item1, bounds.Item2, ref rotatedOrigin, ref rotatedDir, out var dist) && dist < distance)
			foreach(var tri in mesh.Item1)
				if(Intersects(tri[0] * scale, tri[1] * scale, tri[2] * scale, ref rotatedOrigin, ref rotatedDir, out dist, out var hdir) && dist < distance) {
					distance = dist;
					hitdir = Vector3.Transform(hdir, rot);
				}
		
		return distance < range;
	}
	
	private bool AABBIntersects(Vector3 min, Vector3 max, ref Vector3 origin, ref Vector3 dir, out float distance) {
		// https://tavianator.com/2011/ray_box.html
		var t1 = (min.X - origin.X) / dir.X;
		var t2 = (max.X - origin.X) / dir.X;
		var tmin = Math.Min(t1, t2);
		var tmax = Math.Max(t1, t2);
		
		t1 = (min.Y - origin.Y) / dir.Y;
		t2 = (max.Y - origin.Y) / dir.Y;
		tmin = Math.Max(tmin, Math.Min(t1, t2));
		tmax = Math.Min(tmax, Math.Max(t1, t2));
		
		t1 = (min.Z - origin.Z) / dir.Z;
		t2 = (max.Z - origin.Z) / dir.Z;
		tmin = Math.Max(tmin, Math.Min(t1, t2));
		tmax = Math.Min(tmax, Math.Max(t1, t2));
		
		if(tmax < 0 || tmin > tmax) {
			distance = tmax;
			return false;
		}
		
		distance = tmin;
		return true;
	}
	
	private bool Intersects(Vector3 v0, Vector3 v1, Vector3 v2, ref Vector3 origin, ref Vector3 dir, out float distance, out Vector3 hitdir) {
		// https://en.wikipedia.org/wiki/M%C3%B6ller%E2%80%93Trumbore_intersection_algorithm
		var edge1 = v1 - v0;
		var edge2 = v2 - v0;
		var h = Vector3.Cross(dir, edge2);
		var a = Vector3.Dot(edge1, h);
		
		distance = float.MaxValue;
		hitdir = Vector3.Zero;
		
		if(a > -epsilon && a < epsilon)
			return false;
		
		var f = 1f / a;
		var s = origin - v0;
		var u = f * Vector3.Dot(s, h);
		
		if(u < 0f || u > 1f)
			return false;
		
		var q = Vector3.Cross(s, edge1);
		var v = f * Vector3.Dot(dir, q);
		
		if(v < 0f || u + v > 1f)
			return false;
		
		distance = f * Vector3.Dot(edge2, q);
		hitdir = Vector3.Normalize(Vector3.Cross(edge1, edge2));
		return distance > 0;
	}
	
	private List<string> GetModelPaths(string sgbpath) {
		var sgb = DataManager.GetFile<FileResource>(sgbpath);
		if(sgb == null)
			return new();
		
		var r = sgb.Reader;
		
		// https://github.com/TexTools/xivModdingFramework/blob/0d5f74d74a16ffffac3d57b980f71b2f9365f0ce/xivModdingFramework/Items/Categories/Housing.cs#L456
		r.BaseStream.Seek(20, SeekOrigin.Begin);
		var skip = r.ReadInt32() + 20;
		r.BaseStream.Seek(skip + 4, SeekOrigin.Begin);
		var offset = r.ReadInt32();
		r.BaseStream.Seek(skip + offset, SeekOrigin.Begin);
		
		var paths = new List<string>();
		var cur = new StringBuilder(256);
		for(int i = 0; i < 10; i++) {
			byte c;
			while(true) {
				c = r.ReadByte();
				if(c == 0xFF)
					goto exit;
				if(c == 0)
					break;
				
				cur.Append((char)c);
			}
			
			var str = cur.ToString();
			if(str.EndsWith(".sgb"))
				paths = paths.Concat(GetModelPaths(str)).ToList();
			else if(str.EndsWith(".mdl") && !paths.Contains(str))
				paths.Add(str);
			cur.Clear();
		}
		
		exit:{}
		
		return paths;
	}
	
	public unsafe List<(List<Vector3[]>, (Vector3, Vector3))>? GetMesh(Furniture* obj) {
		if(obj == null)
			return null;
		// Logger.Debug($"furniture  {(nint)obj:X}");
		// Logger.Debug($"furniture id {(nint)obj->ID:X}");
		var modelkey = housing->IsOutdoor ? houseSheetOutdoor?.GetRow(obj->ID)?.ModelKey : houseSheet?.GetRow(obj->ID)?.ModelKey;
		if(!modelkey.HasValue)
			return null;
		
		var key = ((byte)(housing->IsIndoor ? 0 : 1), modelkey.Value);
		if(meshCache.ContainsKey(key))
			return meshCache[key];
		
		var paths = GetModelPaths(housing->IsOutdoor ?
			$"bgcommon/hou/outdoor/general/{modelkey:D4}/asset/gar_b0_m{modelkey:D4}.sgb" :
			$"bgcommon/hou/indoor/general/{modelkey:D4}/asset/fun_b0_m{modelkey:D4}.sgb");
		
		if(houseSheet?.GetRow(obj->ID)?.AquariumTier > 0) // used because aquariums are scuffed
			foreach(var affix in modelAffix) {
				var path = $"bgcommon/hou/indoor/general/{modelkey:D4}/bgparts/fun_b0_m{modelkey:D4}{affix}.mdl";
				if(!paths.Contains(path))
					paths.Add(path);
			}
		
		var rtn = new List<(List<Vector3[]>, (Vector3, Vector3))>();
		foreach(var path in paths) {
			var mesh = GetMesh(path);
			if(mesh.HasValue)
				rtn.Add(mesh.Value);
		}
		
		if(rtn.Count == 0)
			return null;
		
		meshCache[key] = rtn;
		
		return rtn;
	}
	
	public unsafe List<(List<Vector3[]>, (Vector3, Vector3))>? GetMesh(ushort territory) {
		var bg = territoryType?.GetRow(territory)?.Bg.ToString();
		if(bg == null)
			return null;
		
		var key = ((byte)2, territory);
		if(meshCache.ContainsKey(key))
			return meshCache[key];
		
		// easier this way
		var paths = new List<(string, Vector3)>();
		var segs = bg.Split('/');
		var reg = segs[1];
		var zone = segs[3];
		var add = $"{zone[0]}{zone[1]}{zone[2]}0";
		
		// cba figuring out where this is in memory, so hardcoded it is
		switch(zone[3]) {
			case '1': // small
				paths = [
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_s_b1_fl_0000.mdl", new Vector3(2, -7, 4)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_s_b1_wl_0000.mdl", new Vector3(2, -7, 4)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_s_b1_rom0000.mdl", new Vector3(2, -7, 4)),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_s_f1_fl_0000.mdl", Vector3.Zero),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_s_f1_wl_0000.mdl", Vector3.Zero),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_s_f1_rom0000.mdl", Vector3.Zero),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_s_lightgard.mdl", Vector3.Zero),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{add}_c0_slp0005.mdl", new Vector3(8, -7, 4)),
				];
				
				break;
			case '2': // medium
				paths = [
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_b1_fl_0000.mdl", new Vector3(0, -7, 0)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_b1_wl_0000.mdl", new Vector3(0, -7, 0)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_b1_rom0000.mdl", new Vector3(0, -7, 0)),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_f1_fl_0000.mdl", Vector3.Zero),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_f1_wl_0000.mdl", Vector3.Zero),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_f1_rom0000.mdl", Vector3.Zero),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_f2_fl_0000.mdl", new Vector3(0, 7, 0)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_f2_wl_0000.mdl", new Vector3(0, 7, 0)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_f2_rom0000.mdl", new Vector3(0, 7, 0)),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_m_lightgard.mdl", Vector3.Zero),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{add}_c0_slp0001.mdl", new Vector3(8, 0, 5)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{add}_c0_slp0002.mdl", new Vector3(-8, -7, 5)),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{add}_c0_ter0001.mdl", new Vector3(8, 7, 5)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{add}_c0_ter0002.mdl", new Vector3(-8, 0, 5)),
				];
				
				break;
			case '3': // large
				paths = [
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_b1_fl_00000.mdl", new Vector3(0, -7, 0)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_b1_wl_00000.mdl", new Vector3(0, -7, 0)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_b1_rom0000.mdl", new Vector3(0, -7, 0)),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_f1_fl_0000.mdl", Vector3.Zero),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_f1_wl_0000.mdl", Vector3.Zero),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_f1_rom0000.mdl", Vector3.Zero),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_f2_fl_0000.mdl", new Vector3(0, 7, 0)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_f2_wl_0000.mdl", new Vector3(0, 7, 0)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_f2_rom0000.mdl", new Vector3(0, 7, 0)),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_l_lightgard.mdl", Vector3.Zero),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{add}_c0_slp0003.mdl", new Vector3(-16, -7, -8)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{add}_c0_slp0004.mdl", new Vector3(0, 0, -8)),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{add}_c0_ter0003.mdl", new Vector3(-15, 0, -7)),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{add}_c0_ter0004.mdl", new Vector3(0, 7, 0)),
				];
				
				break;
			case '4': // apartment
				paths = [
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_p_f1_fl_0000.mdl", Vector3.Zero),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_p_f1_wl_0000.mdl", Vector3.Zero),
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_p_f1_rom0000.mdl", Vector3.Zero),
					
					($"bg/ffxiv/{reg}/ind/common/bgparts/{zone}_p_lightgard.mdl", Vector3.Zero),
				];
				
				break;
		}
		
		var rtn = new List<(List<Vector3[]>, (Vector3, Vector3))>();
		foreach(var path in paths) {
			var offset = path.Item2;
			var meshn = GetMesh(path.Item1);
			if(!meshn.HasValue)
				continue;
			var mesh = meshn.Value;
			
			for(int j = 0; j < mesh.Item1.Count; j++)
				for(int k = 0; k < 3; k++)
					mesh.Item1[j][k] += offset;
			
			mesh.Item2.Item1 += offset;
			mesh.Item2.Item2 += offset;
			
			rtn.Add(mesh);
		}
		
		if(rtn.Count == 0)
			return null;
		
		meshCache[key] = rtn;
		
		return rtn;
	}
	
	public (List<Vector3[]>, (Vector3, Vector3))? GetMesh(string path) {
		var mdl = DataManager.GetFile<MdlFile>(path);
		if(mdl == null)
			return null;
		
		Logger.Info($"Grabbing {path}");
		
		var tris = new List<Vector3[]>();
		var model = new Model(mdl, Model.ModelLod.High);
		foreach(var mesh in model.Meshes) {
			if(!mesh.Types.Any(bannedMeshTypes.Contains))
				for(int i = 0; i <= mesh.Indices.Length - 3; i += 3) {
					try { // ?
						var p1 = mesh.Vertices[mesh.Indices[i    ]].Position;
						var p2 = mesh.Vertices[mesh.Indices[i + 1]].Position;
						var p3 = mesh.Vertices[mesh.Indices[i + 2]].Position;
						
						tris.Add(new Vector3[3] {
							p1 == null ? Vector3.Zero : new Vector3(p1.Value.X, p1.Value.Y, p1.Value.Z), // / p1.Value.W,
							p2 == null ? Vector3.Zero : new Vector3(p2.Value.X, p2.Value.Y, p2.Value.Z), // / p2.Value.W,
							p3 == null ? Vector3.Zero : new Vector3(p3.Value.X, p3.Value.Y, p3.Value.Z), // / p3.Value.W,
						});
					} catch {}
				}
		}
		
		return (tris, (new Vector3(
			mdl.BoundingBoxes.Min[0],
			mdl.BoundingBoxes.Min[1],
			mdl.BoundingBoxes.Min[2]
		), new Vector3(
			mdl.BoundingBoxes.Max[0],
			mdl.BoundingBoxes.Max[1],
			mdl.BoundingBoxes.Max[2]
		)));
	}
}
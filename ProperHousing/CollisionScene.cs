using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Lumina.Models.Models;
using Lumina.Data.Files;
using Lumina.Excel;
using Lumina.Excel.Sheets;
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
		public uint HitObjSubIndex;
		public float Distance;
		
		public RaycastResult() {
			Hit = false;
			HitPos = new Vector3();
			HitDir = new Vector3();
			HitType = CollisionType.None;
			HitObj = null;
			HitObjSubIndex = 0;
		}
	}
	
	private readonly float epsilon = 0.0000001f;
	private readonly List<Mesh.MeshType> bannedMeshTypes = [
		Mesh.MeshType.LightShaft, // fuck you lightshafts, your the main reason i made this
	];
	
	private Dictionary<string, (List<Vector3[]>, (Vector3, Vector3))?> meshCache;
	private Dictionary<(byte, ushort), Dictionary<uint, (List<Vector3[]>, (Vector3, Vector3))>> objCache;
	
	internal ExcelSheet<HousingFurniture>? houseSheet;
	internal ExcelSheet<HousingYardObject>? houseSheetOutdoor;
	private ExcelSheet<TerritoryType>? territoryType;
	
	public CollisionScene() {
		meshCache = [];
		objCache = [];
		
		houseSheet = DataManager.GetExcelSheet<HousingFurniture>();
		houseSheetOutdoor = DataManager.GetExcelSheet<HousingYardObject>();
		territoryType = DataManager.GetExcelSheet<TerritoryType>();
	}
	
	public unsafe RaycastResult Raycast(Vector3 origin, Vector3 target, CollisionType collisionTypeWhitelist = CollisionType.All, nint[]? furnitureItemBlacklist = null, nint[]? furnitureItemWhitelist = null) {
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
					if(Collides(house[(uint)i], Vector3.Zero, Quaternion.Identity, Vector3.One, ref origin, ref dir, distance, out var dist, out var hitdir) && dist < distance) {
						distance = dist;
						
						result.Hit = true;
						result.HitType = CollisionType.World;
						result.HitPos = origin + dir * dist;
						result.HitDir = hitdir;
						result.HitObjSubIndex = (uint)i;
						result.Distance = dist;
					}
		}
		
		void CheckFurniture(Furniture* obj) {
			Dictionary<uint, (List<Vector3[]>, (Vector3, Vector3))>? objmesh = null;
			try {
				objmesh = GetMesh(obj);
			} catch(Exception e) {
				Logger.Error(e, "Failed getting mesh");
			}
			
			if(objmesh == null)
				return;
			// Logger.Debug($"{obj->Name} {(nint)obj:X}");
			foreach(var piece in obj->Item->AllPieces()) {
				// Logger.Debug($"    {(nint)piece:X}");
				var seg = piece->Segment;
				if(objmesh.TryGetValue(piece->Index, out var mesh) && Collides(mesh, seg->Position, seg->Rotation, seg->Scale, ref origin, ref dir, distance, out var dist, out var hitdir) && dist < distance) {
					distance = dist;
					
					result.Hit = true;
					result.HitType = CollisionType.Furniture;
					result.HitPos = origin + dir * dist;
					result.HitDir = hitdir;
					result.HitObj = obj;
					result.HitObjSubIndex = piece->Index;
					result.Distance = dist;
				}
			}
		}
		
		if(collisionTypeWhitelist.HasFlag(CollisionType.Furniture)) {
			// var count = housing->IsOutdoor ? 40 : 400;
			var count = 400;
			for(int i = 0; i < count; i++) {
				var obj = zone->Furniture(i);
				if(obj == null ||
					(furnitureItemBlacklist != null && Array.IndexOf(furnitureItemBlacklist, (nint)obj->Item) >= 0) ||
					(furnitureItemWhitelist != null && Array.IndexOf(furnitureItemWhitelist, (nint)obj->Item) == -1))
					continue;
				
				CheckFurniture(obj);
			}
			
			if(!housing->IsOutdoor && housing->CurrentZone()->IndoorGhostObject != null && housing->CurrentZone()->IndoorActiveObject == null)
				CheckFurniture(housing->CurrentZone()->IndoorGhostObject);
		}
		
		return result;
	}
	
	// public unsafe RaycastResult Raycast(Vector3 origin, Vector3 target) {
	// 	return Raycast(origin, target, CollisionType.All);
	// }
	
	private bool Collides((List<Vector3[]>, (Vector3, Vector3)) mesh, Vector3 pos, Quaternion rot, Vector3 scale, ref Vector3 origin, ref Vector3 dir, float range, out float distance, out Vector3 hitdir) {
		distance = range;
		hitdir = Vector3.Zero;
		
		var irot = Quaternion.Inverse(rot);
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
	
	private Dictionary<uint, string> GetModelPaths(string sgbpath, int level = 0) {
		// if(level == 0)
		// 	Logger.Debug(sgbpath);
		
		var sgb = DataManager.GetFile<SgbFile>(sgbpath);
		if(sgb == null)
			return new();
		
		var indent = new string('\t', level + 1);
		var paths = new Dictionary<uint, string>();
		foreach(var group in sgb.LayerGroups) {
			foreach(var layer in group.Layers) {
				foreach(var obj in layer.InstanceObjects) {
					if(obj.AssetType == Lumina.Data.Parsing.Layer.LayerEntryType.BG) {
						var v = (Lumina.Data.Parsing.Layer.LayerCommon.BGInstanceObject)obj.Object;
						// Logger.Debug($"{indent}{obj.InstanceId} BG: {v.AssetPath}");
						paths.Add(obj.InstanceId << ((3 - level) * 8), v.AssetPath);
					} else if(obj.AssetType == Lumina.Data.Parsing.Layer.LayerEntryType.SharedGroup) {
						var v = (Lumina.Data.Parsing.Layer.LayerCommon.SharedGroupInstanceObject)obj.Object;
						// Logger.Debug($"{indent}{obj.InstanceId} Shared: {v.AssetPath}");
						foreach(var a in GetModelPaths(v.AssetPath, level + 1))
							paths.Add(a.Key | (obj.InstanceId << ((3 - level) * 8)), a.Value);
					}
				}
			}
		}
		
		return paths;
	}
	
	public unsafe Dictionary<uint, (List<Vector3[]>, (Vector3, Vector3))>? GetMesh(Furniture* obj) {
		if(obj == null)
			return null;
		
		var modelkey = housing->IsOutdoor ? houseSheetOutdoor?.GetRow(obj->ID).ModelKey : houseSheet?.GetRow(obj->ID).ModelKey;
		if(!modelkey.HasValue)
			return null;
		
		var key = ((byte)(housing->IsIndoor ? 0 : 1), modelkey.Value);
		if(objCache.ContainsKey(key))
			return objCache[key];
		
		var paths = GetModelPaths(housing->IsOutdoor ?
			$"bgcommon/hou/outdoor/general/{modelkey:D4}/asset/gar_b0_m{modelkey:D4}.sgb" :
			$"bgcommon/hou/indoor/general/{modelkey:D4}/asset/fun_b0_m{modelkey:D4}.sgb");
		
		objCache[key] = paths.Select(v => (v.Key, GetMesh(v.Value))).Where(v => v.Item2 != null).Select(v => (v.Key, v.Item2!.Value)).ToDictionary();
		return objCache[key];
	}
	
	public unsafe Dictionary<uint, (List<Vector3[]>, (Vector3, Vector3))>? GetMesh(ushort territory) {
		var bg = territoryType?.GetRow(territory).Bg.ToString();
		if(bg == null)
			return null;
		
		var key = ((byte)2, territory);
		if(objCache.ContainsKey(key))
			return objCache[key];
		
		var paths = new List<(string, Transform)>();
		
		{
			var segs = bg.Split('/');
			var reg = segs[1];
			var zone = segs[3];
			var lgb = DataManager.GetFile<LgbFile>($"bg/ffxiv/{reg}/ind/{zone}/level/bg.lgb");
			if(lgb != null)
				foreach(var layer in lgb.Layers)
					foreach(var obj in layer.InstanceObjects)
						if(obj.AssetType == Lumina.Data.Parsing.Layer.LayerEntryType.BG) {
							var v = (Lumina.Data.Parsing.Layer.LayerCommon.BGInstanceObject)obj.Object;
							var t = obj.Transform;
							paths.Add((v.AssetPath, new Transform {
								Position = new Vector3(t.Translation.X, t.Translation.Y, t.Translation.Z),
								Rotation = Quaternion.CreateFromYawPitchRoll(t.Rotation.Y, t.Rotation.X, t.Rotation.Z),
								Scale = new Vector3(t.Scale.X, t.Scale.Y, t.Scale.Z),
							}));
						}
		}
		
		var rtn = new Dictionary<uint, (List<Vector3[]>, (Vector3, Vector3))>();
		for(var i = 0; i < paths.Count; i++) {
			var path = paths[i];
			var meshn = GetMesh(path.Item1);
			if(!meshn.HasValue)
				continue;
			var mesh = meshn.Value;
			var transform = path.Item2;
			
			var tris = new List<Vector3[]>();
			var min = new Vector3(float.MaxValue);
			var max = new Vector3(float.MinValue);
			
			for(int j = 0; j < mesh.Item1.Count; j++) {
				tris.Add([Vector3.Zero, Vector3.Zero, Vector3.Zero]);
				for(int k = 0; k < 3; k++) {
					var v = Vector3.Transform(mesh.Item1[j][k] * transform.Scale, transform.Rotation) + transform.Position;
					
					min.X = Math.Min(min.X, v.X);
					max.X = Math.Max(max.X, v.X);
					min.Y = Math.Min(min.Y, v.Y);
					max.Y = Math.Max(max.Y, v.Y);
					min.Z = Math.Min(min.Z, v.Z);
					max.Z = Math.Max(max.Z, v.Z);
					
					tris[j][k] = v;
				}
			}
			
			rtn.Add((uint)i, (tris, (min, max)));
		}
		
		if(rtn.Count == 0)
			return null;
		
		objCache[key] = rtn;
		return rtn;
	}
	
	public (List<Vector3[]>, (Vector3, Vector3))? GetMesh(string path) {
		if(meshCache.TryGetValue(path, out var v))
			return v;
		
		var mdl = DataManager.GetFile<MdlFile>(path);
		if(mdl == null) {
			meshCache.Add(path, null);
			return null;
		}
		
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
						
						tris.Add([
							p1 == null ? Vector3.Zero : new Vector3(p1.Value.X, p1.Value.Y, p1.Value.Z), // / p1.Value.W,
							p2 == null ? Vector3.Zero : new Vector3(p2.Value.X, p2.Value.Y, p2.Value.Z), // / p2.Value.W,
							p3 == null ? Vector3.Zero : new Vector3(p3.Value.X, p3.Value.Y, p3.Value.Z), // / p3.Value.W,
						]);
					} catch {}
				}
		}
		
		var m = (tris, (new Vector3(
			mdl.BoundingBoxes.Min[0],
			mdl.BoundingBoxes.Min[1],
			mdl.BoundingBoxes.Min[2]
		), new Vector3(
			mdl.BoundingBoxes.Max[0],
			mdl.BoundingBoxes.Max[1],
			mdl.BoundingBoxes.Max[2]
		)));
		
		meshCache.Add(path, m);
		
		return m;
	}
}
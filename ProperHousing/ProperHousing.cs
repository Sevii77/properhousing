using System;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using ImGuiNET;
using Dalamud.IoC;
using Dalamud.Game;
using Dalamud.Plugin;
using Dalamud.Logging;
using Dalamud.Data;
using Dalamud.Hooking;
using Dalamud.Game.Gui;

using Lumina.Excel.GeneratedSheets;
using Lumina.Data.Files;
using Lumina.Models.Models;

namespace ProperHousing {
	public class ProperHousing : IDalamudPlugin {
		[PluginService][RequiredVersion("1.0")] public static SigScanner  SigScanner  {get; private set;} = null!;
		[PluginService][RequiredVersion("1.0")] public static DataManager DataManager {get; private set;} = null!;
		[PluginService][RequiredVersion("1.0")] public static GameGui     GameGui     {get; private set;} = null!;
		
		private readonly string[] modelAffix = {"", "a", "b", "c", "d"};
		private readonly float epsilon = 0.0000001f;
		
		public string Name => "Better Housing";
		
		private Dictionary<(bool, ushort), (List<Vector3[]>, (Vector3, Vector3))> meshCache;
		private Lumina.Excel.ExcelSheet<HousingFurniture>? houseSheet;
		private Lumina.Excel.ExcelSheet<HousingYardObject>? houseSheetOutdoor;
		private unsafe Housing* housing;
		private unsafe Camera* camera;
		
		private delegate IntPtr GetHoverObjectDelegate(IntPtr ptr);
		private Hook<GetHoverObjectDelegate> GetHoverObjectHook;
		
		public unsafe ProperHousing(DalamudPluginInterface pluginInterface) {
			meshCache = new();
			
			houseSheet = DataManager.GetExcelSheet<HousingFurniture>();
			houseSheetOutdoor = DataManager.GetExcelSheet<HousingYardObject>();
			
			housing = (Housing*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("40 53 48 83 EC 20 33 DB 48 39 1D ?? ?? ?? ?? 75 2C 45 33 C0 33 D2 B9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 11 48 8B C8 E8 ?? ?? ?? ?? 48 89 05 ?? ?? ?? ?? EB 07", 0xA));
			camera = (Camera*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("48 8D 35 ?? ?? ?? ?? 48 8B 09"));
			
			GetHoverObjectHook = new Hook<GetHoverObjectDelegate>(
				SigScanner.ScanText("40 55 41 55 48 8D 6C 24 ?? 48 81 EC 38 01 00 00"),
				GetHoverObject
			);
			
			GetHoverObjectHook.Enable();
		}
		
		public void Dispose() {
			GetHoverObjectHook.Disable();
		}
		
		private unsafe IntPtr GetHoverObject(IntPtr ptr) {
			var zone = housing->CurrentZone();
			if(zone == null)
				return IntPtr.Zero;
			
			var origin = camera->Pos;
			GameGui.ScreenToWorld(ImGui.GetMousePos(), out var target);
			var dir = Vector3.Normalize(target - origin);
			var range = Vector3.Distance(origin, target);
			
			var curobj = IntPtr.Zero;
			var distance = float.MaxValue;
			
			void CheckFurniture(Furniture* obj) {
				if(Collides(obj, ref origin, ref dir, range, out var dist))
					if(dist < distance) {
						// PluginLog.Log(obj.Value.Name);
						curobj = obj->Item;
						distance = dist;
					}
			}
			
			var count = housing->IsOutdoor ? 40 : 400;
			for(int i = 0; i < count; i++) {
				var obj = zone->Furniture(i);
				if(obj == null)
					continue;
				
				CheckFurniture(obj);
			}
			
			// Placing a object from storeroom
			if(housing->IsOutdoor && housing->CurrentZone()->OutdoorGhostObject != null && housing->CurrentZone()->OutdoorActiveObject == null)
				CheckFurniture(housing->CurrentZone()->OutdoorGhostObject);
			if(!housing->IsOutdoor && housing->CurrentZone()->IndoorGhostObject != null && housing->CurrentZone()->IndoorActiveObject == null)
				CheckFurniture(housing->CurrentZone()->IndoorGhostObject);
			
			if(distance > range)
				return IntPtr.Zero;
			
			return curobj;
			
			// return GetHoverObjectHook.Original(ptr);
		}
		
		private unsafe bool Collides(Furniture* obj, ref Vector3 origin, ref Vector3 dir, float range, out float distance) {
			distance = range;
			
			var objmesh = GetMesh(obj);
			if(objmesh == null)
				return false;
				// ohoh, we cant target this object
				// TODO: aabb or obb check before this
			
			var rot = Quaternion.CreateFromYawPitchRoll(obj->Rotation, 0, 0);
			var irot = Quaternion.Inverse(rot);
			var pos = obj->Pos;
			
			// rotate ray to object space and check aabb
			var bounds = objmesh.Value.Item2;
			var rotatedOrigin = Vector3.Transform(origin - pos, irot) + pos;
			var rotatedDir = Vector3.Transform(dir, irot);
			if(!AABBIntersects(pos + bounds.Item1, pos + bounds.Item2, ref rotatedOrigin, ref rotatedDir, out var dist) || dist > range)
				return false;
			
			// check if we intersect with any triangle
			foreach(var tri in objmesh.Value.Item1) {
				var p1 = pos + Vector3.Transform(tri[0], rot);
				var p2 = pos + Vector3.Transform(tri[1], rot);
				var p3 = pos + Vector3.Transform(tri[2], rot);
				
				if(Intersects(ref p1, ref p2, ref p3, ref origin, ref dir, out dist))
					distance = Math.Min(distance, dist);
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
		
		private bool Intersects(ref Vector3 v0, ref Vector3 v1, ref Vector3 v2, ref Vector3 origin, ref Vector3 dir, out float distance) {
			// https://en.wikipedia.org/wiki/M%C3%B6ller%E2%80%93Trumbore_intersection_algorithm
			var edge1 = v1 - v0;
			var edge2 = v2 - v0;
			var h = Vector3.Cross(dir, edge2);
			var a = Vector3.Dot(edge1, h);
			
			distance = float.MaxValue;
			
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
			return true;
		}
		
		private unsafe (List<Vector3[]>, (Vector3, Vector3))? GetMesh(Furniture* obj) {
			var modelkey = housing->IsOutdoor ? houseSheetOutdoor?.GetRow(obj->ID)?.ModelKey : houseSheet?.GetRow(obj->ID)?.ModelKey;
			if(!modelkey.HasValue)
				return null;
			
			var key = (housing->IsOutdoor, modelkey.Value);
			if(meshCache.ContainsKey(key))
				return meshCache[key];
			
			var rtn = (new List<Vector3[]>(), (Vector3.Zero, Vector3.Zero));
			
			foreach(var affix in modelAffix) {
				var path = housing->IsOutdoor ?
					$"bgcommon/hou/outdoor/general/{modelkey:D4}/bgparts/gar_b0_m{modelkey:D4}{affix}.mdl" :
					$"bgcommon/hou/indoor/general/{modelkey:D4}/bgparts/fun_b0_m{modelkey:D4}{affix}.mdl";
				var mdl = DataManager.GetFile<MdlFile>(path);
				if(mdl == null)
					continue;
				
				PluginLog.Log($"Grabbing {path}");
				
				rtn.Item2.Item1.X = Math.Min(rtn.Item2.Item1.X, mdl.BoundingBoxes.Min[0]);
				rtn.Item2.Item1.Y = Math.Min(rtn.Item2.Item1.Y, mdl.BoundingBoxes.Min[1]);
				rtn.Item2.Item1.Z = Math.Min(rtn.Item2.Item1.Z, mdl.BoundingBoxes.Min[2]);
				rtn.Item2.Item2.X = Math.Max(rtn.Item2.Item2.X, mdl.BoundingBoxes.Max[0]);
				rtn.Item2.Item2.Y = Math.Max(rtn.Item2.Item2.Y, mdl.BoundingBoxes.Max[1]);
				rtn.Item2.Item2.Z = Math.Max(rtn.Item2.Item2.Z, mdl.BoundingBoxes.Max[2]);
				
				var model = new Model(mdl, Model.ModelLod.High);
				
				foreach(var mesh in model.Meshes)
					for(int i = 0; i <= mesh.Indices.Length - 3; i += 3) {
						try { // ?
							var p1 = mesh.Vertices[mesh.Indices[i    ]].Position;
							var p2 = mesh.Vertices[mesh.Indices[i + 1]].Position;
							var p3 = mesh.Vertices[mesh.Indices[i + 2]].Position;
							
							rtn.Item1.Add(new Vector3[3] {
								p1 == null ? Vector3.Zero : new Vector3(p1.Value.X, p1.Value.Y, p1.Value.Z),
								p2 == null ? Vector3.Zero : new Vector3(p2.Value.X, p2.Value.Y, p2.Value.Z),
								p3 == null ? Vector3.Zero : new Vector3(p3.Value.X, p3.Value.Y, p3.Value.Z),
							});
						} catch {}
					}
			}
			
			if(rtn.Item1.Count == 0)
				return null;
			
			meshCache[key] = rtn;
			
			return rtn;
		}
	}
}
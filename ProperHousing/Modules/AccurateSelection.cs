using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;
using Newtonsoft.Json;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Models.Models;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Hooking;
using static ProperHousing.ProperHousing;

namespace ProperHousing;

[JsonObject(MemberSerialization.OptIn)]
public class AccurateSelection : Module {
	public override string Name => "AccurateSelection";
	
	private readonly float epsilon = 0.0000001f;
	private readonly string[] modelAffix = ["", "a", "b", "c", "d"];
	private readonly List<Mesh.MeshType> bannedMeshTypes = [
		Mesh.MeshType.LightShaft, // fuck you lightshafts, your the main reason i made this
	];
	
	[JsonProperty] private bool AccurateSelect;
	
	private Dictionary<(bool, ushort), List<(List<Vector3[]>, (Vector3, Vector3))>> meshCache;
	private Lumina.Excel.ExcelSheet<HousingFurniture>? houseSheet;
	private Lumina.Excel.ExcelSheet<HousingYardObject>? houseSheetOutdoor;
	
	private delegate IntPtr GetHoverObjectDelegate(IntPtr ptr);
	private Hook<GetHoverObjectDelegate> GetHoverObjectHook;
	
	public AccurateSelection() {
		AccurateSelect = true;
		LoadConfig();
		
		meshCache = [];
		houseSheet = DataManager.GetExcelSheet<HousingFurniture>();
		houseSheetOutdoor = DataManager.GetExcelSheet<HousingYardObject>();
		
		GetHoverObjectHook = HookProv.HookFromAddress<GetHoverObjectDelegate>(SigScanner.ScanText(Sigs.GetHoverObject), GetHoverObject);
		if(AccurateSelect)
			GetHoverObjectHook.Enable();
	}
	
	public override void Dispose() {
		GetHoverObjectHook.Dispose();
	}
	
	public override void DrawOption() {
		if(ImGui.Checkbox("Accurate Selection", ref AccurateSelect)) {
			SaveConfig();
			
			if(AccurateSelect)
				GetHoverObjectHook.Enable();
			else
				GetHoverObjectHook.Disable();
		}
	}
	
	// Debug rendering
	public unsafe override void Tick() {
		if(!DebugDraw)
			return;
		
		var zone = housing->CurrentZone();
		if(zone == null)
			return;
		
		var screenpos = ImGui.GetMousePos();
		var origin = camera->Pos;
		
		GameGui.ScreenToWorld(screenpos, out var target);
		var dir = Vector3.Normalize(target - origin);
		var distance = Vector3.Distance(origin, target);
		
		// var obj = housing->IsOutdoor ? zone->OutdoorHoverObject : zone->IndoorHoverObject;
		Furniture* obj = null;
		for(int i = 0; i < 400; i++) {
			var o = zone->Furniture(i);
			if(o == null)
				continue;
			
			if(Collides(o, ref origin, ref dir, distance, out var dist)) {
				obj = o;
				distance = dist;
			}
		}
		
		if(obj == null)
			return;
		
		// draw bb and wireframe
		var draw = ImGui.GetForegroundDrawList();
		
		var objmesh = GetMesh(obj);
		if(objmesh != null) {
			// var segs = obj->ModelSegments(objmesh.Count);
			var segs = obj->ModelSegments();
			for(int segI = 0; segI < Math.Min(segs.Length, objmesh.Count); segI++) {
				var rot = segs[segI]->Rotation;
				var pos = segs[segI]->Position;
				// var scale = segs[segI]->Scale * obj->Item->Scale;
				var scale = Vector3.One;
				
				{ // bounding box
					var bounds = objmesh[segI].Item2;
					var pos1 = bounds.Item1;
					var pos2 = bounds.Item2;
					
					GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos1.X, pos1.Y, pos1.Z), rot) + pos, out var p1);
					GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos1.X, pos1.Y, pos2.Z), rot) + pos, out var p2);
					GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos2.X, pos1.Y, pos2.Z), rot) + pos, out var p3);
					GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos2.X, pos1.Y, pos1.Z), rot) + pos, out var p4);
					GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos1.X, pos2.Y, pos1.Z), rot) + pos, out var p5);
					GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos1.X, pos2.Y, pos2.Z), rot) + pos, out var p6);
					GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos2.X, pos2.Y, pos2.Z), rot) + pos, out var p7);
					GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos2.X, pos2.Y, pos1.Z), rot) + pos, out var p8);
					
					draw.AddLine(p1, p2, 0xFF0000FF);
					draw.AddLine(p2, p3, 0xFF0000FF);
					draw.AddLine(p3, p4, 0xFF0000FF);
					draw.AddLine(p4, p1, 0xFF0000FF);
					draw.AddLine(p5, p6, 0xFF0000FF);
					draw.AddLine(p6, p7, 0xFF0000FF);
					draw.AddLine(p7, p8, 0xFF0000FF);
					draw.AddLine(p8, p5, 0xFF0000FF);
					draw.AddLine(p1, p5, 0xFF0000FF);
					draw.AddLine(p2, p6, 0xFF0000FF);
					draw.AddLine(p3, p7, 0xFF0000FF);
					draw.AddLine(p4, p8, 0xFF0000FF);
				}
				
				foreach(var tri in objmesh[segI].Item1) {
					GameGui.WorldToScreen(Vector3.Transform(tri[0] * scale, rot) + pos, out var p1);
					GameGui.WorldToScreen(Vector3.Transform(tri[1] * scale, rot) + pos, out var p2);
					GameGui.WorldToScreen(Vector3.Transform(tri[2] * scale, rot) + pos, out var p3);
					
					draw.AddLine(p1, p2, 0xFFFFFFFF);
					draw.AddLine(p2, p3, 0xFFFFFFFF);
					draw.AddLine(p3, p1, 0xFFFFFFFF);
				}
			}
			
			for(int i = 0; i < segs.Length; i++) {
				GameGui.WorldToScreen(segs[i]->Position, out var p1);
				draw.AddCircle(p1, 5, 0xFF0000FF);
			}
		}
		
		var objIndex = -1;
		for(int i = 0; i < 400; i++)
			if((ulong)zone->Objects[i] == (ulong)obj) {
				objIndex = i;
				break;
			}
		
		var modelkey = housing->IsOutdoor ? houseSheetOutdoor?.GetRow(obj->ID)?.ModelKey : houseSheet?.GetRow(obj->ID)?.ModelKey;
		var p = ImGui.GetMousePos() - new Vector2(0, ImGui.GetFontSize() * 3);
		var str = $"{obj->Name} (index: {objIndex}) (pieces: {obj->Item->PiecesCount})";
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		str = housing->IsOutdoor ?
			$"bgcommon/hou/outdoor/general/{modelkey:D4}/" :
			$"bgcommon/hou/indoor/general/{modelkey:D4}/";
		p += new Vector2(0, ImGui.GetFontSize());
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		str = ((IntPtr)obj).ToString("X");
		p += new Vector2(0, ImGui.GetFontSize());
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		if(new Bind(true, true, false, Key.C).Pressed()) {
			// linq doesnt like pointers, fucking shit language dont @ me
			var s = new System.Collections.Generic.List<String>();
			foreach(var v in obj->Item->Pieces())
				s.Add(((IntPtr)v).ToString("X"));
			InputHandler.SetClipboard(String.Join(" ", s));
		} else if(new Bind(false, true, false, Key.C).Pressed())
			InputHandler.SetClipboard(str);
	}
	
	private unsafe IntPtr GetHoverObject(IntPtr ptr) {
		if(!AccurateSelect)
			return GetHoverObjectHook.Original(ptr);
		
		var zone = housing->CurrentZone();
		if(zone == null)
			return GetHoverObjectHook.Original(ptr);
		
		// Dont run for outside, we dont know which furniture we own
		if(!housing->IsIndoor)
			return GetHoverObjectHook.Original(ptr);
		
		// Dont run if we are previewing a object
		if(layout->Manager == null || layout->Manager->PreviewMode)
			return GetHoverObjectHook.Original(ptr);
		
		// var origin = camera->Pos;
		var screenpos = ImGui.GetMousePos();
		var origin = camera->Pos;
		
		GameGui.ScreenToWorld(screenpos, out var target);
		var dir = Vector3.Normalize(target - origin);
		
		var curobj = IntPtr.Zero;
		var distance = Vector3.Distance(origin, target) + 0.1f; // makes sure to not block object selection by the object's vanilla collision (happens in remove/storage mod for some)
		
		void CheckFurniture(Furniture* obj) {
			if(Collides(obj, ref origin, ref dir, distance, out var dist)) {
				curobj = (IntPtr)obj->Item;
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
		// if(housing->IsOutdoor && housing->CurrentZone()->OutdoorGhostObject != null && housing->CurrentZone()->OutdoorActiveObject == null)
		// 	CheckFurniture(housing->CurrentZone()->OutdoorGhostObject);
		if(!housing->IsOutdoor && housing->CurrentZone()->IndoorGhostObject != null && housing->CurrentZone()->IndoorActiveObject == null)
			CheckFurniture(housing->CurrentZone()->IndoorGhostObject);
		
		return curobj;
	}
	
	private unsafe bool Collides(Furniture* obj, ref Vector3 origin, ref Vector3 dir, float range, out float distance) {
		distance = range;
		
		var objmesh = GetMesh(obj);
		if(objmesh == null)
			return false;
			// ohoh, we cant target this object
			// TODO: aabb or obb check before this
		
		// var segs = obj->ModelSegments(objmesh.Count);
		var segs = obj->ModelSegments();
		for(int segI = 0; segI < Math.Min(segs.Length, objmesh.Count); segI++) {
			var irot = Quaternion.Inverse(segs[segI]->Rotation);
			var pos = segs[segI]->Position;
			// var scale = segs[segI]->Scale * obj->Item->Scale;
			var scale = Vector3.One;
			
			var bounds = objmesh[segI].Item2;
			var rotatedOrigin = Vector3.Transform(origin - pos, irot);
			var rotatedDir = Vector3.Transform(dir, irot);
			
			if(AABBIntersects(bounds.Item1, bounds.Item2, ref rotatedOrigin, ref rotatedDir, out var dist) && dist <= range)
				foreach(var tri in objmesh[segI].Item1) {
					if(Intersects(tri[0] * scale, tri[1] * scale, tri[2] * scale, ref rotatedOrigin, ref rotatedDir, out dist))
						distance = Math.Min(distance, dist);
				}
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
	
	private bool Intersects(Vector3 v0, Vector3 v1, Vector3 v2, ref Vector3 origin, ref Vector3 dir, out float distance) {
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
	
	private unsafe List<(List<Vector3[]>, (Vector3, Vector3))>? GetMesh(Furniture* obj) {
		var modelkey = housing->IsOutdoor ? houseSheetOutdoor?.GetRow(obj->ID)?.ModelKey : houseSheet?.GetRow(obj->ID)?.ModelKey;
		if(!modelkey.HasValue)
			return null;
		
		var key = (housing->IsOutdoor, modelkey.Value);
		if(meshCache.ContainsKey(key))
			return meshCache[key];
		
		// var affixedPaths = new List<string>();
		var affixedPaths = GetModelPaths(housing->IsOutdoor ?
			$"bgcommon/hou/outdoor/general/{modelkey:D4}/asset/gar_b0_m{modelkey:D4}.sgb" :
			$"bgcommon/hou/indoor/general/{modelkey:D4}/asset/fun_b0_m{modelkey:D4}.sgb")/*.ForEach(path =>
				Array.ForEach(modelAffix, affix =>
					affixedPaths.Add(path.Replace(".mdl", $"{affix}.mdl"))))*/;
		
		if(houseSheet?.GetRow(obj->ID)?.AquariumTier > 0) // used because aquariums are scuffed
			foreach(var affix in modelAffix) {
				var path = $"bgcommon/hou/indoor/general/{modelkey:D4}/bgparts/fun_b0_m{modelkey:D4}{affix}.mdl";
				if(!affixedPaths.Contains(path))
					affixedPaths.Add(path);
			}
		
		var rtn = new List<(List<Vector3[]>, (Vector3, Vector3))>();
		foreach(var path in affixedPaths) {
			var mdl = DataManager.GetFile<MdlFile>(path);
			if(mdl == null)
				continue;
			
			Logger.Info($"Grabbing {path}");
			
			var tris = new List<Vector3[]>();
			var model = new Model(mdl, Model.ModelLod.High);
			foreach(var mesh in model.Meshes) {
				if(!mesh.Types.Any(x => bannedMeshTypes.Contains(x)))
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
			
			if(tris.Count > 0)
				rtn.Add((tris, (new Vector3(
					mdl.BoundingBoxes.Min[0],
					mdl.BoundingBoxes.Min[1],
					mdl.BoundingBoxes.Min[2]
				), new Vector3(
					mdl.BoundingBoxes.Max[0],
					mdl.BoundingBoxes.Max[1],
					mdl.BoundingBoxes.Max[2]
				))));
		}
		
		if(rtn.Count == 0)
			return null;
		
		meshCache[key] = rtn;
		
		return rtn;
	}
}
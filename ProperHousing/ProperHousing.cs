using System;
using System.IO;
using System.Text;
using System.Linq;
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
using Dalamud.Interface;
using Dalamud.Game.Gui;
using Dalamud.Game.Command;

using Lumina.Excel.GeneratedSheets;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Models.Models;
using Newtonsoft.Json;

namespace ProperHousing {
	public partial class ProperHousing : IDalamudPlugin {
		[PluginService][RequiredVersion("1.0")] public static DalamudPluginInterface Interface   {get; private set;} = null!;
		[PluginService][RequiredVersion("1.0")] public static CommandManager         Commands    {get; private set;} = null!;
		[PluginService][RequiredVersion("1.0")] public static SigScanner             SigScanner  {get; private set;} = null!;
		[PluginService][RequiredVersion("1.0")] public static DataManager            DataManager {get; private set;} = null!;
		[PluginService][RequiredVersion("1.0")] public static GameGui                GameGui     {get; private set;} = null!;
		
		private readonly float epsilon = 0.0000001f;
		private readonly string[] modelAffix = {"", "a", "b", "c", "d"};
		private readonly List<Mesh.MeshType> bannedMeshTypes = new List<Mesh.MeshType> {
			Mesh.MeshType.LightShaft, // fuck you lightshafts, your the main reason i made this
		};
		
		public string Name => "Better Housing";
		private const string command = "/betterhousing";
		
		private Dictionary<(bool, ushort), List<(List<Vector3[]>, (Vector3, Vector3))>> meshCache;
		private Lumina.Excel.ExcelSheet<HousingFurniture>? houseSheet;
		private Lumina.Excel.ExcelSheet<HousingYardObject>? houseSheetOutdoor;
		private unsafe Camera* camera;
		private unsafe Housing* housing;
		private unsafe Layout* layout;
		private unsafe int* scroll;
		private int lastScroll;
		private int scrollDelta = 0;
		
		private bool debugDraw = false;
		private bool confDraw = true;
		
		private Config config;
		
		private delegate IntPtr GetHoverObjectDelegate(IntPtr ptr);
		private Hook<GetHoverObjectDelegate> GetHoverObjectHook;
		
		private unsafe delegate void CameraZoomHandlerDelegate(Camera* camera, int unk, int unk2, ulong unk3);
		private Hook<CameraZoomHandlerDelegate> CameraZoomHandlerHook;
		
		// private unsafe delegate void AnimationDelegate(IntPtr ptr, float* transform);
		// private Hook<AnimationDelegate> AnimationHook;
		
		private struct Config {
			public Bind MoveMode;
			public Bind RotateMode;
			public Bind RemoveMode;
			public Bind StoreMode;
			public Bind CounterToggle;
			public Bind GridToggle;
			
			public Config() {
				MoveMode = new();
				RotateMode = new();
				RemoveMode = new();
				StoreMode = new();
				CounterToggle = new();
				GridToggle = new();
			}
			
			public void Save() {
				File.WriteAllText(Interface.ConfigFile.FullName, JsonConvert.SerializeObject(this));
			}
		}
		
		public unsafe ProperHousing(DalamudPluginInterface pluginInterface) {
			config = Interface.ConfigFile.Exists ? JsonConvert.DeserializeObject<Config>(File.ReadAllText(Interface.ConfigFile.FullName)) : new();
			meshCache = new();
			keyStates = new byte[256];
			
			houseSheet = DataManager.GetExcelSheet<HousingFurniture>();
			houseSheetOutdoor = DataManager.GetExcelSheet<HousingYardObject>();
			
			camera = (Camera*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("4C 8D 35 ?? ?? ?? ?? 85 D2"));
			housing = (Housing*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("40 53 48 83 EC 20 33 DB 48 39 1D ?? ?? ?? ?? 75 2C 45 33 C0 33 D2 B9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 11 48 8B C8 E8 ?? ?? ?? ?? 48 89 05 ?? ?? ?? ?? EB 07", 0xA));
			layout = (Layout*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B 49 40 E9 ?? ?? ?? ??", 2));
			
			// Get scroll state int, idk if this is a good idea. dinput8 shouldnt change between updates, right?
			var modules = System.Diagnostics.Process.GetCurrentProcess().Modules;
			for(int i = 0; i < modules.Count; i++)
				if(modules[i].ModuleName == "DINPUT8.dll") {
					scroll = (int*)(modules[i].BaseAddress + 0x3E0E8);
					lastScroll = *scroll;
					break;
				}
			
			PluginLog.Log($"{((IntPtr)(camera)).ToString("X")}");
			PluginLog.Log($"{((IntPtr)(housing)).ToString("X")}");
			PluginLog.Log($"{((IntPtr)(layout)).ToString("X")}");
			
			GetHoverObjectHook = Hook<GetHoverObjectDelegate>.FromAddress(
				SigScanner.ScanText("40 55 41 55 48 8D 6C 24 ?? 48 81 EC 38 01 00 00"),
				GetHoverObject
			);
			GetHoverObjectHook.Enable();
			
			CameraZoomHandlerHook = Hook<CameraZoomHandlerDelegate>.FromAddress(
				SigScanner.ScanText("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 83 B9 ?? ?? ?? ?? 01 41 8B F8"),
				CameraZoomHandler
			);
			CameraZoomHandlerHook.Enable();
			
			// AnimationHook = Hook<AnimationDelegate>.FromAddress(
			// 	SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 60 48 8B D9 48 8B FA"),
			// 	Animator
			// );
			// AnimationHook.Enable();
			
			Interface.UiBuilder.Draw += Draw;
			
			Commands.AddHandler(command, new CommandInfo((cmd, args) => {
				if(cmd != command)
					return;
				
				if(args == "debug")
					debugDraw = !debugDraw;
				else
					confDraw = !confDraw;
			}) {
				ShowInHelp = false
			});
		}
		
		public void Dispose() {
			Commands.RemoveHandler(command);
			Interface.UiBuilder.Draw += Draw;
			GetHoverObjectHook.Disable();
			CameraZoomHandlerHook.Disable();
			// AnimationHook.Disable();
		}
		
		private unsafe void Draw() {
			if(confDraw)
				DrawConf();
			
			if(debugDraw)
				DrawDebug();
			
			scrollDelta = (*scroll - lastScroll) / 120;
			lastScroll = *scroll;
		}
		
		private unsafe void DrawDebug() {
			var zone = housing->CurrentZone();
			if(zone == null)
				return;
			
			var obj = housing->IsOutdoor ? zone->OutdoorHoverObject : zone->IndoorHoverObject;
			if(obj == null)
				return;
			
			// PluginLog.Log($"{((IntPtr)obj).ToString("X")}");
			
			var draw = ImGui.GetForegroundDrawList();
			
			var objmesh = GetMesh(obj);
			if(objmesh != null) {
				var segs = obj->ModelSegments(objmesh.Count);
				for(int segI = 0; segI < segs.Length; segI++) {
					var rot = segs[segI]->Rotation;
					var pos = segs[segI]->Position;
					
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
						GameGui.WorldToScreen(Vector3.Transform(tri[0], rot) + pos, out var p1);
						GameGui.WorldToScreen(Vector3.Transform(tri[1], rot) + pos, out var p2);
						GameGui.WorldToScreen(Vector3.Transform(tri[2], rot) + pos, out var p3);
						
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
			var p = ImGui.GetMousePos() - new Vector2(0, ImGui.GetFontSize() * 2);
			var str = $"(mdl: {modelkey}) (idx: {objIndex}) {obj->Name}";
			draw.AddText(p, 0xFF000000, str);
			draw.AddText(p - Vector2.One, 0xFF0000FF, str);
			
			str = housing->IsOutdoor ?
				$"bgcommon/hou/outdoor/general/{modelkey:D4}/" :
				$"bgcommon/hou/indoor/general/{modelkey:D4}/";
			p += new Vector2(0, ImGui.GetFontSize());
			draw.AddText(p, 0xFF000000, str);
			draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		}
		
		private unsafe void CameraZoomHandler(Camera* camera, int unk, int unk2, ulong unk3) {
			if(layout->Manager->ActiveItem != null && ImGui.IsKeyDown(ImGuiKey.LeftShift)) {
				if(scrollDelta != 0) {
					// layout->Manager->ActiveItem->Rotation *= Quaternion.CreateFromYawPitchRoll(scrollDelta * 15 / 180f * (float)Math.PI, 0, 0);
					var r = &layout->Manager->ActiveItem->Rotation;
					var drag = (360 / 15f);
					var rot = Math.Round((Math.Atan2(r->W, r->Y) / Math.PI * drag + scrollDelta * 15 / drag));
					r->Y = (float)Math.Cos(rot / drag * Math.PI);
					r->W = (float)Math.Sin(rot / drag * Math.PI);
				}
				
				return;
			}
			
			CameraZoomHandlerHook.Original(camera, unk, unk2, unk3);
		}
		
		// private unsafe void Animator(IntPtr ptr, float* transform) {
		// 	AnimationHook.Original(ptr, transform);
		// }
		
		private unsafe IntPtr GetHoverObject(IntPtr ptr) {
			// return GetHoverObjectHook.Original(ptr);
			
			var zone = housing->CurrentZone();
			if(zone == null)
				return IntPtr.Zero;
			
			// Dont run for outside, we dont know which furniture we own
			if(housing->IsOutdoor)
				return GetHoverObjectHook.Original(ptr);
			
			// Dont run if we are previewing a object
			if(layout->Manager->PreviewMode)
				return GetHoverObjectHook.Original(ptr);
			
			// var origin = camera->Pos;
			var screenpos = ImGui.GetMousePos();
			var origin = camera->Pos;
			
			GameGui.ScreenToWorld(screenpos, out var target);
			var dir = Vector3.Normalize(target - origin);
			
			var curobj = IntPtr.Zero;
			var distance = Vector3.Distance(origin, target);
			
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
			if(housing->IsOutdoor && housing->CurrentZone()->OutdoorGhostObject != null && housing->CurrentZone()->OutdoorActiveObject == null)
				CheckFurniture(housing->CurrentZone()->OutdoorGhostObject);
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
			
			var segs = obj->ModelSegments(objmesh.Count);
			for(int segI = 0; segI < segs.Length; segI++) {
				var irot = Quaternion.Inverse(segs[segI]->Rotation);
				var pos = segs[segI]->Position;
				
				var bounds = objmesh[segI].Item2;
				var rotatedOrigin = Vector3.Transform(origin - pos, irot);
				var rotatedDir = Vector3.Transform(dir, irot);
				
				if(AABBIntersects(bounds.Item1, bounds.Item2, ref rotatedOrigin, ref rotatedDir, out var dist) && dist <= range)
					foreach(var tri in objmesh[segI].Item1)
						if(Intersects(tri[0], tri[1], tri[2], ref rotatedOrigin, ref rotatedDir, out dist))
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
				
				PluginLog.Log($"Grabbing {path}");
				
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
									p1 == null ? Vector3.Zero : new Vector3(p1.Value.X, p1.Value.Y, p1.Value.Z),
									p2 == null ? Vector3.Zero : new Vector3(p2.Value.X, p2.Value.Y, p2.Value.Z),
									p3 == null ? Vector3.Zero : new Vector3(p3.Value.X, p3.Value.Y, p3.Value.Z),
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
}
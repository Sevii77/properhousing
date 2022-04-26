// TODO: support bones, should fix the the offsets on certain furniture and animated furniture

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

namespace ProperHousing {
	public class ProperHousing : IDalamudPlugin {
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
		// Some model vertices are offset from what we see, probably some funky bone stuff idk
		// TODO: find a better solution
		private readonly Dictionary<ushort, Vector3> modelOffsets = new Dictionary<ushort, Vector3> {
			{633, new Vector3(0, 0, -0.2f)}, // Stage Panel
			{571, new Vector3(0, 0.4f, 0)}, // Hingan Chochin Lantern
		};
		
		public string Name => "Better Housing";
		private const string command = "/properhousing";
		
		private Dictionary<(bool, ushort), (List<Vector3[]>, (Vector3, Vector3))> meshCache;
		private Lumina.Excel.ExcelSheet<HousingFurniture>? houseSheet;
		private Lumina.Excel.ExcelSheet<HousingYardObject>? houseSheetOutdoor;
		private unsafe Housing* housing;
		private unsafe Layout* layout;
		private bool debugDraw = false;
		
		private delegate IntPtr GetMatrixSingletonDelegate();
		private GetMatrixSingletonDelegate GetMatrixSingleton;
		
		private delegate IntPtr GetHoverObjectDelegate(IntPtr ptr);
		private Hook<GetHoverObjectDelegate> GetHoverObjectHook;
		
		public unsafe ProperHousing(DalamudPluginInterface pluginInterface) {
			meshCache = new();
			
			houseSheet = DataManager.GetExcelSheet<HousingFurniture>();
			houseSheetOutdoor = DataManager.GetExcelSheet<HousingYardObject>();
			
			housing = (Housing*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("40 53 48 83 EC 20 33 DB 48 39 1D ?? ?? ?? ?? 75 2C 45 33 C0 33 D2 B9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 11 48 8B C8 E8 ?? ?? ?? ?? 48 89 05 ?? ?? ?? ?? EB 07", 0xA));
			layout = (Layout*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B 49 40 E9 ?? ?? ?? ??", 2));
			
			GetMatrixSingleton = Marshal.GetDelegateForFunctionPointer<GetMatrixSingletonDelegate>(
				SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? 48 89 4c 24 ?? 4C 8D 4D ?? 4C 8D 44 24 ??"));
			
			GetHoverObjectHook = new Hook<GetHoverObjectDelegate>(
				SigScanner.ScanText("40 55 41 55 48 8D 6C 24 ?? 48 81 EC 38 01 00 00"),
				GetHoverObject
			);
			
			GetHoverObjectHook.Enable();
			
			Interface.UiBuilder.Draw += Draw;
			
			Commands.AddHandler(command, new CommandInfo((cmd, args) => {
				if(cmd == command && args == "debug")
					debugDraw = !debugDraw;
			}) {
				ShowInHelp = false
			});
		}
		
		public void Dispose() {
			Commands.RemoveHandler(command);
			Interface.UiBuilder.Draw += Draw;
			GetHoverObjectHook.Disable();
		}
		
		private unsafe void Draw() {
			if(!debugDraw)
				return;
			
			var zone = housing->CurrentZone();
			if(zone == null)
				return;
			
			var obj = housing->IsOutdoor ? zone->OutdoorHoverObject : zone->IndoorHoverObject;
			if(obj == null)
				return;
			
			var draw = ImGui.GetForegroundDrawList();
			
			var objmesh = GetMesh(obj);
			if(objmesh != null) {
				var rot = Quaternion.CreateFromYawPitchRoll(obj->Rotation, 0, 0);
				var pos = obj->Pos;
				
				foreach(var tri in objmesh.Value.Item1) {
					GameGui.WorldToScreen(Vector3.Transform(tri[0], rot) + pos, out var p1);
					GameGui.WorldToScreen(Vector3.Transform(tri[1], rot) + pos, out var p2);
					GameGui.WorldToScreen(Vector3.Transform(tri[2], rot) + pos, out var p3);
					
					draw.AddLine(p1, p2, 0xFFFFFFFF);
					draw.AddLine(p2, p3, 0xFFFFFFFF);
					draw.AddLine(p3, p1, 0xFFFFFFFF);
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
			var str = $"({modelkey}) ({objIndex}) {obj->Name}";
			draw.AddText(p, 0xFF000000, str);
			draw.AddText(p - Vector2.One, 0xFF0000FF, str);
			
			str = housing->IsOutdoor ?
				$"bgcommon/hou/outdoor/general/{modelkey:D4}/" :
				$"bgcommon/hou/indoor/general/{modelkey:D4}/";
			p += new Vector2(0, ImGui.GetFontSize());
			draw.AddText(p, 0xFF000000, str);
			draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		}
		
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
			var origin = Vector3.Zero;
			{ // Get camera origin
			  // https://github.com/goatcorp/Dalamud/blob/dd0159ae5a2174819c1541644e5cdbd4ddd98a1d/Dalamud/Game/Gui/GameGui.cs#L243
				var windowPos = ImGuiHelpers.MainViewport.Pos;
				var windowSize = ImGuiHelpers.MainViewport.Size;
				
				var matrixSingleton = GetMatrixSingleton();
				
				var viewProjectionMatrix = default(SharpDX.Matrix);
				var rawMatrix = (float*)(matrixSingleton + 0x1b4).ToPointer();
				for(var i = 0; i < 16; i++, rawMatrix++)
					viewProjectionMatrix[i] = *rawMatrix;
				
				var width = *rawMatrix;
				var height = *(rawMatrix + 1);
				
				viewProjectionMatrix.Invert();
				
				var localScreenPos = new SharpDX.Vector2(screenpos.X - windowPos.X, screenpos.Y - windowPos.Y);
				var screenPos3D = new SharpDX.Vector3{
					X = (localScreenPos.X / width * 2.0f) - 1.0f,
					Y = -((localScreenPos.Y / height * 2.0f) - 1.0f),
					Z = 0,
				};
				
				SharpDX.Vector3.TransformCoordinate(ref screenPos3D, ref viewProjectionMatrix, out var p);
				origin.X = p.X;
				origin.Y = p.Y;
				origin.Z = p.Z;
			}
			
			GameGui.ScreenToWorld(screenpos, out var target);
			var dir = Vector3.Normalize(target - origin);
			
			var curobj = IntPtr.Zero;
			var distance = Vector3.Distance(origin, target);
			
			void CheckFurniture(Furniture* obj) {
				if(Collides(obj, ref origin, ref dir, distance, out var dist)) {
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
			
			return curobj;
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
			var rotatedOrigin = Vector3.Transform(origin - pos, irot);
			var rotatedDir = Vector3.Transform(dir, irot);
			if(!AABBIntersects(bounds.Item1, bounds.Item2, ref rotatedOrigin, ref rotatedDir, out var dist) || dist > range)
				return false;
			
			// check if we intersect with any triangle
			foreach(var tri in objmesh.Value.Item1)
				if(Intersects(tri[0], tri[1], tri[2], ref rotatedOrigin, ref rotatedDir, out dist))
					distance = Math.Min(distance, dist);
			
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
				else if(str.EndsWith(".mdl"))
					paths.Add(str);
				cur.Clear();
			}
			
			exit:{}
			
			return paths;
		}
		
		private unsafe (List<Vector3[]>, (Vector3, Vector3))? GetMesh(Furniture* obj) {
			var modelkey = housing->IsOutdoor ? houseSheetOutdoor?.GetRow(obj->ID)?.ModelKey : houseSheet?.GetRow(obj->ID)?.ModelKey;
			if(!modelkey.HasValue)
				return null;
			
			var key = (housing->IsOutdoor, modelkey.Value);
			if(meshCache.ContainsKey(key))
				return meshCache[key];
			
			var offset = modelOffsets.ContainsKey(modelkey.Value) ? modelOffsets[modelkey.Value] : Vector3.Zero;
			
			var affixedPaths = new List<string>();
			GetModelPaths(housing->IsOutdoor ?
				$"bgcommon/hou/outdoor/general/{modelkey:D4}/asset/gar_b0_m{modelkey:D4}.sgb" :
				$"bgcommon/hou/indoor/general/{modelkey:D4}/asset/fun_b0_m{modelkey:D4}.sgb").ForEach(path =>
					Array.ForEach(modelAffix, affix =>
						affixedPaths.Add(path.Replace(".mdl", $"{affix}.mdl"))));
			
			var rtn = (new List<Vector3[]>(), (Vector3.Zero, Vector3.Zero));
			foreach(var path in affixedPaths) {
				var mdl = DataManager.GetFile<MdlFile>(path);
				if(mdl == null)
					continue;
				
				PluginLog.Log($"Grabbing {path}");
				
				rtn.Item2.Item1.X = Math.Min(rtn.Item2.Item1.X, mdl.BoundingBoxes.Min[0] + offset.X);
				rtn.Item2.Item1.Y = Math.Min(rtn.Item2.Item1.Y, mdl.BoundingBoxes.Min[1] + offset.Y);
				rtn.Item2.Item1.Z = Math.Min(rtn.Item2.Item1.Z, mdl.BoundingBoxes.Min[2] + offset.Z);
				rtn.Item2.Item2.X = Math.Max(rtn.Item2.Item2.X, mdl.BoundingBoxes.Max[0] + offset.X);
				rtn.Item2.Item2.Y = Math.Max(rtn.Item2.Item2.Y, mdl.BoundingBoxes.Max[1] + offset.Y);
				rtn.Item2.Item2.Z = Math.Max(rtn.Item2.Item2.Z, mdl.BoundingBoxes.Max[2] + offset.Z);
				
				var model = new Model(mdl, Model.ModelLod.High);
				
				foreach(var mesh in model.Meshes)
					if(!mesh.Types.Any(x => bannedMeshTypes.Contains(x)))
						for(int i = 0; i <= mesh.Indices.Length - 3; i += 3) {
							try { // ?
								var p1 = mesh.Vertices[mesh.Indices[i    ]].Position;
								var p2 = mesh.Vertices[mesh.Indices[i + 1]].Position;
								var p3 = mesh.Vertices[mesh.Indices[i + 2]].Position;
								
								rtn.Item1.Add(new Vector3[3] {
									p1 == null ? Vector3.Zero : new Vector3(p1.Value.X, p1.Value.Y, p1.Value.Z) + offset,
									p2 == null ? Vector3.Zero : new Vector3(p2.Value.X, p2.Value.Y, p2.Value.Z) + offset,
									p3 == null ? Vector3.Zero : new Vector3(p3.Value.X, p3.Value.Y, p3.Value.Z) + offset,
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
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
using Dalamud.Game.Gui;
using Dalamud.Game.Command;

using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Models.Models;
using Lumina.Excel.GeneratedSheets;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Newtonsoft.Json;

namespace ProperHousing;

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
	
	private bool debugDraw = false;
	private bool confDraw = false;
	private bool preventzoom = false;
	
	private Config config;
	
	private delegate IntPtr GetHoverObjectDelegate(IntPtr ptr);
	private Hook<GetHoverObjectDelegate> GetHoverObjectHook;
	
	private unsafe delegate void CameraZoomHandlerDelegate(Camera* camera, int unk, int unk2, ulong unk3);
	private Hook<CameraZoomHandlerDelegate> CameraZoomHandlerHook;
	
	private unsafe delegate IntPtr ReceiveEventDelegate(AtkEventListener* eventListener, ushort evt, uint which, void* eventData, void* inputData);
	
	public class Bind {
		public bool Shift;
		public bool Ctrl;
		public bool Alt;
		public Key Key;
		
		public Bind() {
			Shift = false;
			Ctrl = false;
			Alt = false;
			Key = Key.None;
		}
		
		public Bind(bool s, bool c, bool a, Key k) {
			Shift = s;
			Ctrl = c;
			Alt = a;
			Key = k;
		}
		
		public bool Pressed() {
			return Key != Key.None && ModsPressed() && InputHandler.KeyPressed(Key);
		}
		
		public bool ModsPressed() {
			return (!Shift || ImGui.IsKeyDown(ImGuiKey.ModShift)) && (!Ctrl || ImGui.IsKeyDown(ImGuiKey.ModCtrl)) && (!Alt || ImGui.IsKeyDown(ImGuiKey.ModAlt));
		}
	}
	
	private class Config {
		public bool AccurateSelect;
		public Bind RotateCounter;
		public Bind RotateClockwise;
		public Bind MoveMode;
		public Bind RotateMode;
		public Bind RemoveMode;
		public Bind StoreMode;
		public Bind CounterToggle;
		public Bind GridToggle;
		
		public Config() {
			AccurateSelect = true;
			RotateCounter = new(true, false, false, Key.WheelUp);
			RotateClockwise = new(true, false, false, Key.WheelDown);
			MoveMode = new(true, false, false, Key.Number1);
			RotateMode = new(true, false, false, Key.Number2);
			RemoveMode = new(true, false, false, Key.Number3);
			StoreMode = new(true, false, false, Key.Number4);
			CounterToggle = new(true, false, false, Key.Number5);
			GridToggle = new(true, false, false, Key.Number6);
		}
		
		public void Save() {
			File.WriteAllText(Interface.ConfigFile.FullName, JsonConvert.SerializeObject(this));
		}
	}
	
	public unsafe ProperHousing(DalamudPluginInterface pluginInterface) {
		config = Interface.ConfigFile.Exists ? JsonConvert.DeserializeObject<Config>(File.ReadAllText(Interface.ConfigFile.FullName)) ?? new() : new();
		meshCache = new();
		
		houseSheet = DataManager.GetExcelSheet<HousingFurniture>();
		houseSheetOutdoor = DataManager.GetExcelSheet<HousingYardObject>();
		
		camera = (Camera*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("4C 8D 35 ?? ?? ?? ?? 85 D2"));
		housing = (Housing*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("40 53 48 83 EC 20 33 DB 48 39 1D ?? ?? ?? ?? 75 2C 45 33 C0 33 D2 B9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 11 48 8B C8 E8 ?? ?? ?? ?? 48 89 05 ?? ?? ?? ?? EB 07"));
		layout = (Layout*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B 49 40 E9 ?? ?? ?? ??"));
		
		PluginLog.Log($"{((IntPtr)(camera)).ToString("X")}");
		PluginLog.Log($"{((IntPtr)(housing)).ToString("X")}");
		PluginLog.Log($"{((IntPtr)(layout)).ToString("X")}");
		
		GetHoverObjectHook = Hook<GetHoverObjectDelegate>.FromAddress(
			SigScanner.ScanText("40 55 41 55 48 8D 6C 24 ?? 48 81 EC 38 01 00 00"),
			GetHoverObject
		);
		GetHoverObjectHook.Enable();
		
		CameraZoomHandlerHook = Hook<CameraZoomHandlerDelegate>.FromAddress(
			SigScanner.ScanText("E8 ?? ?? ?? ?? EB ?? F3 0F 10 83 ?? ?? ?? ?? 0F 2F 83 ?? ?? ?? ??"),
			CameraZoomHandler
		);
		CameraZoomHandlerHook.Enable();
		
		Interface.UiBuilder.Draw += Draw;
		Interface.UiBuilder.OpenConfigUi += OpenConf;
		
		Commands.AddHandler(command, new CommandInfo((cmd, args) => {
			if(cmd != command)
				return;
			
			if(args == "debug")
				debugDraw = !debugDraw;
			else if(args == "test collides")
				TestCollides();
			else
				confDraw = !confDraw;
		}) {
			ShowInHelp = true,
			HelpMessage = "Opens the configuration window"
			
		});
	}
	
	public void Dispose() {
		Commands.RemoveHandler(command);
		Interface.UiBuilder.Draw -= Draw;
		Interface.UiBuilder.OpenConfigUi -= OpenConf;
		GetHoverObjectHook.Disable();
		CameraZoomHandlerHook.Disable();
	}
	
	private void OpenConf() {confDraw = true;}
	
	private unsafe void Draw() {
		InputHandler.Update();
		
		if(layout->Manager->HousingMode) {
			HandleBinds();
		}
		
		if(confDraw)
			DrawConf();
		
		if(debugDraw)
			DrawDebug();
	}
	
	private unsafe void HandleBinds() {
		// holy shit is this ugly
		if(((config.RotateCounter.Key == Key.WheelUp || config.RotateCounter.Key == Key.WheelDown) && config.RotateCounter.ModsPressed()) ||
		   ((config.RotateClockwise.Key == Key.WheelUp || config.RotateClockwise.Key == Key.WheelDown) && config.RotateClockwise.ModsPressed()) ||
		   ((config.MoveMode.Key == Key.WheelUp || config.MoveMode.Key == Key.WheelDown) && config.MoveMode.ModsPressed()) ||
		   ((config.RotateMode.Key == Key.WheelUp || config.RotateMode.Key == Key.WheelDown) && config.RotateMode.ModsPressed()) ||
		   ((config.RemoveMode.Key == Key.WheelUp || config.RemoveMode.Key == Key.WheelDown) && config.RemoveMode.ModsPressed()) ||
		   ((config.StoreMode.Key == Key.WheelUp || config.StoreMode.Key == Key.WheelDown) && config.StoreMode.ModsPressed()) ||
		   ((config.CounterToggle.Key == Key.WheelUp || config.CounterToggle.Key == Key.WheelDown) && config.CounterToggle.ModsPressed()) ||
		   ((config.GridToggle.Key == Key.WheelUp || config.GridToggle.Key == Key.WheelDown) && config.GridToggle.ModsPressed()))
			preventzoom = true;
		
		if(layout->Manager->ActiveItem != null) {
			var delta = ((config.RotateCounter.Pressed() ? -1 : 0) + (config.RotateClockwise.Pressed() ? 1 : 0)) * Math.Max(1, Math.Abs(InputHandler.ScrollDelta)) * 15;
			if(delta != 0) {
				var r = &layout->Manager->ActiveItem->Rotation;
				var drag = (360 / 15f);
				var rot = Math.Round((Math.Atan2(r->W, r->Y) / Math.PI * drag + delta / drag));
				r->Y = (float)Math.Cos(rot / drag * Math.PI);
				r->W = (float)Math.Sin(rot / drag * Math.PI);
			}
		}
		
		void ToggleCheckbox(ushort index) {
			if(index != 0)
				ToggleCheckbox(0);
			
			var atk = AtkStage.GetSingleton()->RaptureAtkUnitManager->GetAddonByName("HousingLayout");
			
			var eventData = stackalloc void*[3];
			eventData[0] = null;
			eventData[1] = null;
			eventData[2] = atk;
			
			var inputData = stackalloc void*[8];
			for(var i = 0; i < 8; i++)
				inputData[i] = null;
			
			var receiveEvent = Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>(new IntPtr(atk->AtkEventListener.vfunc[2]))!;
			receiveEvent(&atk->AtkEventListener, 25, index, eventData, inputData);
		}
		
		if(config.MoveMode.Pressed()) ToggleCheckbox(1);
		if(config.RotateMode.Pressed()) ToggleCheckbox(2);
		if(!layout->Manager->PreviewMode) {
			if(config.RemoveMode.Pressed()) ToggleCheckbox(3);
			if(config.StoreMode.Pressed()) ToggleCheckbox(4);
		}
		if(config.CounterToggle.Pressed()) ToggleCheckbox(6);
		if(config.GridToggle.Pressed()) ToggleCheckbox(7);
	}
	
	private unsafe void CameraZoomHandler(Camera* camera, int unk, int unk2, ulong unk3) {
		if(preventzoom) {
			preventzoom = false;
			
			return;
		}
		
		CameraZoomHandlerHook.Original(camera, unk, unk2, unk3);
	}
	
	private unsafe IntPtr GetHoverObject(IntPtr ptr) {
		if(!config.AccurateSelect)
			return GetHoverObjectHook.Original(ptr);
		
		var zone = housing->CurrentZone();
		if(zone == null)
			return GetHoverObjectHook.Original(ptr);
		
		// Dont run for outside, we dont know which furniture we own
		if(!housing->IsIndoor)
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
		
		var segs = obj->ModelSegments(objmesh.Count);
		for(int segI = 0; segI < segs.Length; segI++) {
			var irot = Quaternion.Inverse(segs[segI]->Rotation);
			var pos = segs[segI]->Position;
			// var scale = segs[segI]->Scale * obj->Item->Scale;
			var scale = Vector3.One;
			
			var bounds = objmesh[segI].Item2;
			var rotatedOrigin = Vector3.Transform(origin - pos, irot);
			var rotatedDir = Vector3.Transform(dir, irot);
			
			if(AABBIntersects(bounds.Item1, bounds.Item2, ref rotatedOrigin, ref rotatedDir, out var dist) && dist <= range)
				foreach(var tri in objmesh[segI].Item1)
					if(Intersects(tri[0] * scale, tri[1] * scale, tri[2] * scale, ref rotatedOrigin, ref rotatedDir, out dist))
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
			
			PluginLog.Log($"Grabbing {path}; element count: {mdl.ElementIds.Length}");
			
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
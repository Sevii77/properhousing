using System;
using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;
using Newtonsoft.Json;
using Dalamud.Hooking;
using static ProperHousing.ProperHousing;

namespace ProperHousing;

[JsonObject(MemberSerialization.OptIn)]
public class AccurateSelection: Module {
	public override string Name => "AccurateSelection";
	public override bool DoDrawOption => true;
	
	[JsonProperty] private bool AccurateSelect;
	
	private unsafe delegate FurnitureItem* GetHoverObjectDelegate(nint unk1);
	private Hook<GetHoverObjectDelegate> GetHoverObjectHook;
	
	public unsafe AccurateSelection() {
		AccurateSelect = true;
		LoadConfig();
		
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
	
	public unsafe override void DrawDebug() {
		var zone = housing->CurrentZone();
		if(zone == null)
			return;
		
		var draw = ImGui.GetBackgroundDrawList();
		void DrawMesh((List<Vector3[]>, (Vector3, Vector3)) mesh, Vector3 pos, Quaternion rot, uint bboxColor = 0xFF0000FF, uint meshColor = 0xFFFFFFFF) {
			{ // bounding box
				var pos1 = mesh.Item2.Item1;
				var pos2 = mesh.Item2.Item2;
				
				var c1 = GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos1.X, pos1.Y, pos1.Z), rot) + pos, out var p1);
				var c2 = GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos1.X, pos1.Y, pos2.Z), rot) + pos, out var p2);
				var c3 = GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos2.X, pos1.Y, pos2.Z), rot) + pos, out var p3);
				var c4 = GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos2.X, pos1.Y, pos1.Z), rot) + pos, out var p4);
				var c5 = GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos1.X, pos2.Y, pos1.Z), rot) + pos, out var p5);
				var c6 = GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos1.X, pos2.Y, pos2.Z), rot) + pos, out var p6);
				var c7 = GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos2.X, pos2.Y, pos2.Z), rot) + pos, out var p7);
				var c8 = GameGui.WorldToScreen(Vector3.Transform(new Vector3(pos2.X, pos2.Y, pos1.Z), rot) + pos, out var p8);
				
				if(c1 || c2) draw.AddLine(p1, p2, bboxColor);
				if(c2 || c3) draw.AddLine(p2, p3, bboxColor);
				if(c3 || c4) draw.AddLine(p3, p4, bboxColor);
				if(c4 || c1) draw.AddLine(p4, p1, bboxColor);
				if(c5 || c6) draw.AddLine(p5, p6, bboxColor);
				if(c6 || c7) draw.AddLine(p6, p7, bboxColor);
				if(c7 || c8) draw.AddLine(p7, p8, bboxColor);
				if(c8 || c5) draw.AddLine(p8, p5, bboxColor);
				if(c1 || c5) draw.AddLine(p1, p5, bboxColor);
				if(c2 || c6) draw.AddLine(p2, p6, bboxColor);
				if(c3 || c7) draw.AddLine(p3, p7, bboxColor);
				if(c4 || c8) draw.AddLine(p4, p8, bboxColor);
			}
			
			foreach(var tri in mesh.Item1) {
				var c1 = GameGui.WorldToScreen(Vector3.Transform(tri[0], rot) + pos, out var p1);
				var c2 = GameGui.WorldToScreen(Vector3.Transform(tri[1], rot) + pos, out var p2);
				var c3 = GameGui.WorldToScreen(Vector3.Transform(tri[2], rot) + pos, out var p3);
				
				if(c1 || c2) draw.AddLine(p1, p2, meshColor);
				if(c2 || c3) draw.AddLine(p2, p3, meshColor);
				if(c3 || c1) draw.AddLine(p3, p1, meshColor);
			}
		}
		
		var screenpos = ImGui.GetMousePos();
		var ray = Project2D(screenpos);
		var hit = collisionScene.Raycast(ray.Item1, ray.Item1 + ray.Item2 * 999999);
		
		if(hit.Hit && hit.HitType == CollisionScene.CollisionType.World) {
			var house = collisionScene.GetMesh((ushort)layout->HouseLayout->Territory);
			DrawMesh(house![hit.HitObjSubIndex], Vector3.Zero, Quaternion.Identity);
			return;
		} else if(hit.Hit && hit.HitType == CollisionScene.CollisionType.Furniture) {
			var objmesh = collisionScene.GetMesh(hit.HitObj);
			var segs = hit.HitObj->ModelSegments();
			for(int i = 0; i < Math.Min(segs.Length, objmesh!.Count); i++) {
				var pos = segs[i]->Position;
				var rot = segs[i]->Rotation;
				
				DrawMesh(objmesh![i], pos, rot, hit.HitObjSubIndex == i ? 0xFF0000FF : 0x400000FF, hit.HitObjSubIndex == i ? 0xFFFFFFFF : 0x40FFFFFF);
			}
			
			for(int i = 0; i < segs.Length; i++) {
				GameGui.WorldToScreen(segs[i]->Position, out var p1);
				draw.AddCircle(p1, 5, 0xFF0000FF);
			}
		}
		
		var obj = hit.HitObj;
		var objIndex = -1;
		for(int i = 0; i < 400; i++)
			if(zone->Objects[i] == (ulong)obj) {
				objIndex = i;
				break;
			}
		
		var p = ImGui.GetMousePos() - new Vector2(0, ImGui.GetFontSize() * 3);
		var str = $"{obj->Name} (index: {objIndex}) (pieces: {obj->Item->PiecesCount})";
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		// var modelkey = housing->IsOutdoor ? houseSheetOutdoor?.GetRow(obj->ID)?.ModelKey : houseSheet?.GetRow(obj->ID)?.ModelKey;
		// str = housing->IsOutdoor ?
		// 	$"bgcommon/hou/outdoor/general/{modelkey:D4}/" :
		// 	$"bgcommon/hou/indoor/general/{modelkey:D4}/";
		// p += new Vector2(0, ImGui.GetFontSize());
		// draw.AddText(p, 0xFF000000, str);
		// draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		str = ((nint)obj).ToString("X");
		p += new Vector2(0, ImGui.GetFontSize());
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		if(new Bind(true, true, false, Key.C).Pressed()) {
			// linq doesnt like pointers, fucking shit language dont @ me
			var s = new List<string>();
			foreach(var v in obj->Item->Pieces())
				s.Add(((nint)v).ToString("X"));
			InputHandler.SetClipboard(String.Join(" ", s));
		} else if(new Bind(false, true, false, Key.C).Pressed())
			InputHandler.SetClipboard(str);
	}
	
	private unsafe FurnitureItem* GetHoverObject(nint unk1) {
		try {
			if(!AccurateSelect)
				return GetHoverObjectHook.Original(unk1);
			
			var zone = housing->CurrentZone();
			if(zone == null)
				return GetHoverObjectHook.Original(unk1);
			
			// Dont run for outside, we dont know which furniture we own
			if(!housing->IsIndoor)
				return GetHoverObjectHook.Original(unk1);
			
			// Dont run if we are previewing a object
			if(layout->Manager == null || layout->Manager->PreviewMode)
				return GetHoverObjectHook.Original(unk1);
			
			var screenpos = ImGui.GetMousePos();
			var ray = Project2D(screenpos);
			var hit = collisionScene.Raycast(ray.Item1, ray.Item1 + ray.Item2 * 999999);
			
			if(hit.Hit) {
				if(hit.HitType == CollisionScene.CollisionType.Furniture)
					return hit.HitObj->Item;
				
				// if(!layout->Manager->PreviewMode && hit.HitType == CollisionScene.CollisionType.Furniture)
				// 	return hit.HitObj->Item;
				// 
				// if(layout->Manager->PreviewMode && hit.HitObj->Item == layout->Manager->PlaceItem)
				// 	return hit.HitObj->Item;
			}
			
			return null;
		} catch(Exception e) {
			Logger.Error(e, "GetHoverObject failed");
			return null;
		}
	}
}
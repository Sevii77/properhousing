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
		void DrawMesh((List<Vector3[]>, (Vector3, Vector3)) mesh, Vector3 pos, Quaternion rot, Vector3 scale, uint bboxColor = 0xFF0000FF, uint meshColor = 0xFFFFFFFF) {
			{ // bounding box
				var pos1 = mesh.Item2.Item1 * scale;
				var pos2 = mesh.Item2.Item2 * scale;
				
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
				var c1 = GameGui.WorldToScreen(Vector3.Transform(tri[0] * scale, rot) + pos, out var p1);
				var c2 = GameGui.WorldToScreen(Vector3.Transform(tri[1] * scale, rot) + pos, out var p2);
				var c3 = GameGui.WorldToScreen(Vector3.Transform(tri[2] * scale, rot) + pos, out var p3);
				
				if(c1 || c2) draw.AddLine(p1, p2, meshColor);
				if(c2 || c3) draw.AddLine(p2, p3, meshColor);
				if(c3 || c1) draw.AddLine(p3, p1, meshColor);
			}
		}
		
		var screenpos = ImGui.GetMousePos();
		var ray = Project2D(screenpos);
		var hit = collisionScene.Raycast(ray.Item1, ray.Item1 + ray.Item2 * 999999);
		if(!hit.Hit)
			return;
		
		if(hit.HitType == CollisionScene.CollisionType.World) {
			var house = collisionScene.GetMesh((ushort)layout->HouseLayout->Territory)!;
			DrawMesh(house[hit.HitObjSubIndex], Vector3.Zero, Quaternion.Identity, Vector3.One);
		} else if(hit.HitType == CollisionScene.CollisionType.Furniture) {
			var objmesh = collisionScene.GetMesh(hit.HitObj)!;
			
			foreach(var piece in hit.HitObj->Item->AllPieces()) {
				var seg = piece->Segment;
				if(objmesh.TryGetValue(piece->Index, out var mesh)) {
					DrawMesh(mesh, seg->Position, seg->Rotation, seg->Scale, hit.HitObjSubIndex == piece->Index ? 0xFF0000FF : 0x400000FF, hit.HitObjSubIndex == piece->Index ? 0xFFFFFFFF : 0x40FFFFFF);
					
					GameGui.WorldToScreen(seg->Position, out var p1);
					draw.AddCircle(p1, 5, 0xFF0000FF);
				}
			}
		}
		
		{
			GameGui.WorldToScreen(hit.HitPos, out var p1);
			draw.AddCircleFilled(p1, 4, 0xFFFF0000);
			
			GameGui.WorldToScreen(hit.HitPos + hit.HitDir, out var p2);
			draw.AddLine(p1, p2, 0xFFFF0000);
		}
		
		var obj = hit.HitObj;
		if(obj == null)
			return;
		
		var objIndex = -1;
		for(int i = 0; i < 400; i++)
			if(zone->Objects[i] == (ulong)obj) {
				objIndex = i;
				break;
			}
		
		var p = ImGui.GetMousePos() - new Vector2(0, ImGui.GetFontSize() * 3);
		// for(int i = 0; i < 400; i++) {
		// objIndex = i;
		// obj = zone->Furniture(i);
		// if(obj == null)
		// 	continue;
		// 
		// if(!GameGui.WorldToScreen(obj->Position, out var p))
		// 	continue;
		
		var str = $"{obj->Name} (index: {objIndex}) (pieces: {obj->Item->PiecesCount})";
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		var modelkey = housing->IsOutdoor ? collisionScene.houseSheetOutdoor?.GetRow(obj->ID)?.ModelKey : collisionScene.houseSheet?.GetRow(obj->ID)?.ModelKey;
		str = housing->IsOutdoor ?
			$"bgcommon/hou/outdoor/general/{modelkey:D4}/" :
			$"bgcommon/hou/indoor/general/{modelkey:D4}/";
		p += new Vector2(0, ImGui.GetFontSize());
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		str = ((nint)obj).ToString("X");
		p += new Vector2(0, ImGui.GetFontSize());
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		if(new Bind(true, true, false, Key.C).Pressed()) {
			// linq doesnt like pointers, fucking shit language dont @ me
			var s = new List<string>();
			foreach(var v in obj->Item->Pieces())
				s.Add(((nint)v).ToString("X"));
			InputHandler.SetClipboard(string.Join(" ", s));
		} else if(new Bind(false, true, false, Key.C).Pressed())
			InputHandler.SetClipboard(str);
		
		void DrawPiece(FurnitureModelPiece* piece, int level = 0) {
			var index = (piece->Index >> ((3 - level) * 8)) & 0xFF;
			str = $"{new string('\t', level + 1)}[{index}] {(Lumina.Data.Parsing.Layer.LayerEntryType)piece->Type}";
			p += new Vector2(0, ImGui.GetFontSize());
			draw.AddText(p, 0xFF000000, str);
			draw.AddText(p - Vector2.One, 0xFF0000FF, str);
			
			if(piece->Type == 6)
				foreach(var p in piece->AsItem->Pieces())
					DrawPiece(p, level + 1);
		}
		
		foreach(var piece in obj->Item->Pieces())
			DrawPiece(piece);
		
		// }
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
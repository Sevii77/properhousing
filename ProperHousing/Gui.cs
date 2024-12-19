using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using static ProperHousing.ProperHousing;

namespace ProperHousing;

public class Gui {
	private static Bind? selectBind;
	public static bool DrawKeybind(string label, Bind bind) {
		var changed = false;
		var shift = bind.Shift;
		var ctrl = bind.Ctrl;
		var alt = bind.Alt;
		var key = bind.Key.ToString();
		
		if (selectBind == bind) {
			shift = ImGui.IsKeyDown(ImGuiKey.ModShift);
			ctrl = ImGui.IsKeyDown(ImGuiKey.ModCtrl);
			alt = ImGui.IsKeyDown(ImGuiKey.ModAlt);
			key = "";
			
			if(ImGui.IsKeyDown(ImGuiKey.Escape))
				selectBind = null;
			else
				foreach(var k in Enum.GetValues(typeof(Key)).Cast<Key>())
					if(InputHandler.KeyPressed(k)) {
						bind.Shift = shift;
						bind.Ctrl = ctrl;
						bind.Alt = alt;
						bind.Key = k;
						selectBind = null;
						changed = true;
						break;
					}
		}
		
		var s = $"{(shift ? "Shift+" : "")}{(ctrl ? "Ctrl+" : "")}{(alt ? "Alt+" : "")}{key}";
		ImGui.SetNextItemWidth(175);
		ImGui.InputText(label, ref s, 64, ImGuiInputTextFlags.ReadOnly);
		if(ImGui.IsItemClicked(ImGuiMouseButton.Left))
			selectBind = bind;
		else if(ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
			bind.Shift = false;
			bind.Ctrl = false;
			bind.Alt = false;
			bind.Key = Key.None;
			changed = true;
		}
		
		return changed;
	}
	
	public void DrawConf(ref bool enabled, Module[] modules) {
		ImGui.Begin("Better Housing Config", ref enabled, ImGuiWindowFlags.AlwaysAutoResize);
		
		for(var i = 0; i < modules.Length; i++) {
			if(modules[i].DoDrawOption) {
				modules[i].DrawOption();
				ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8.0f);
			}
		}
		
		ImGui.PushStyleColor(ImGuiCol.Button, 0xFF00D2FE);
		ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xFF000000 | (uint)(0x0000D2FE * 0.6));
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF000000 | (uint)(0x0000D2FE * 0.8));
		ImGui.PushStyleColor(ImGuiCol.Text, 0xFF000000);
		if(ImGui.Button("Support me on Buy Me a Coffee"))
			Process.Start("https://buymeacoffee.com/sevii77");
		ImGui.PopStyleColor(4);
		
		ImGui.End();
	}
	
	public void DrawQuick(ref bool enabled, Module[] modules) {
		ImGui.Begin("Better Housing", ref enabled, ImGuiWindowFlags.AlwaysAutoResize);
		
		var first = true;
		for(var i = 0; i < modules.Length; i++) {
			if(modules[i].DoDrawQuick) {
				if(!first)
					ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8.0f);
				first = false;
				
				modules[i].DrawQuick();
			}
		}
		
		ImGui.End();
	}
	
	public unsafe void DrawDebug() {
		ImGui.Begin("Better Housing Debug", ImGuiWindowFlags.AlwaysAutoResize);
		
		var house = ProperHousing.layout->HouseLayout;
		if(house == null)
			goto end;
		
		var layout = house->Layout;
		if(layout == null)
			goto end;
		
		void drawFloor(ref FloorLayout floor) {
			for(var i = 0; i < 5; i++)
				ImGui.Text($"\t{floor.Fixtures[i]}");
				
		}
		
		ImGui.Text("Floor 1");
		drawFloor(ref layout->Floor1);
		
		ImGui.Text("Floor 2");
		drawFloor(ref layout->Floor2);
		
		ImGui.Text("Floor 3");
		drawFloor(ref layout->Floor3);
		
		ImGui.Text("Floor 4");
		drawFloor(ref layout->Floor4);
		
		end:
		ImGui.End();
	}
	
	public unsafe void DrawDebugOverlay() {
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
			// foreach(var (i, piece) in house)
			// 	DrawMesh(piece, Vector3.Zero, Quaternion.Identity, Vector3.One, hit.HitObjSubIndex == i ? 0xFF0000FF : 0x400000FF, hit.HitObjSubIndex == i ? 0xFFFFFFFF : 0x40FFFFFF);
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
		
		foreach(var c in obj->Item->FurnitureItemExtras->Children) {
			var child = (FurnitureItemChild*)c;
			GameGui.WorldToScreen(obj->Item->Position, out var p1);
			GameGui.WorldToScreen(child->Item->Position, out var p2);
			draw.AddLine(p1, p2, 0xFF00FF00);
		}
		
		var nl = new Vector2(0, ImGui.GetFontSize());
		var p = ImGui.GetMousePos() + new Vector2(ImGui.GetFontSize() * 2, 0);
		var str = $"{obj->Name} (index: {objIndex}) (pieces: {obj->Item->PiecesCount})";
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		p += nl;
		var modelkey = housing->IsOutdoor ? collisionScene.houseSheetOutdoor?.GetRow(obj->ID).ModelKey : collisionScene.houseSheet?.GetRow(obj->ID).ModelKey;
		str = housing->IsOutdoor ?
			$"bgcommon/hou/outdoor/general/{modelkey:D4}/" :
			$"bgcommon/hou/indoor/general/{modelkey:D4}/";
		draw.AddText(p, 0xFF000000, str);
		draw.AddText(p - Vector2.One, 0xFF0000FF, str);
		
		p += nl;
		str = ((nint)obj).ToString("X");
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
			p += nl;
			var index = (piece->Index >> ((3 - level) * 8)) & 0xFF;
			str = $"{new string('\t', level + 1)}[{index}] {(Lumina.Data.Parsing.Layer.LayerEntryType)piece->Type}";
			draw.AddText(p, 0xFF000000, str);
			draw.AddText(p - Vector2.One, 0xFF0000FF, str);
			
			if(piece->Type == 6)
				foreach(var p in piece->AsItem->Pieces())
					DrawPiece(p, level + 1);
		}
		
		foreach(var piece in obj->Item->Pieces())
			DrawPiece(piece);
	}
}
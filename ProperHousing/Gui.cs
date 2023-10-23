using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace ProperHousing;

public partial class ProperHousing {
	private Bind? selectBind;
	
	private void DrawConf() {
		void DrawKeybind(string label, Bind bind) {
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
							config.Save();
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
			}
		}
		
		ImGui.Begin("Better Housing", ref confDraw, ImGuiWindowFlags.AlwaysAutoResize);
		
		if(ImGui.Checkbox("Accurate Selection", ref config.AccurateSelect))
			config.Save();
		
		ImGui.Text($"Keybinds");
		
		DrawKeybind("Rotate Counterclockwise", config.RotateCounter);
		DrawKeybind("Rotate Clockwise", config.RotateClockwise);
		DrawKeybind("Move Mode", config.MoveMode);
		DrawKeybind("Rotate Mode", config.RotateMode);
		DrawKeybind("Remove Mode", config.RemoveMode);
		DrawKeybind("Store Mode", config.StoreMode);
		DrawKeybind("Toggle Counter Placement", config.CounterToggle);
		DrawKeybind("Toggle Grid Snap", config.GridToggle);
		
		ImGui.End();
	}
	
	private unsafe void DrawDebug() {
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
}
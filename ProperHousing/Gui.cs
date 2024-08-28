using System;
using System.Linq;
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
		
		var first = true;
		for(var i = 0; i < modules.Length; i++) {
			if(modules[i].DoDrawOption) {
				if(!first)
					ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8.0f);
				first = false;
				
				modules[i].DrawOption();
			}
		}
		
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
}
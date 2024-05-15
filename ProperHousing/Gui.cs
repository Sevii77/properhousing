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
		ImGui.Begin("Better Housing", ref enabled, ImGuiWindowFlags.AlwaysAutoResize);
		
		for(var i = 0; i < modules.Length; i++) {
			modules[i].DrawOption();
			if(i < modules.Length - 1)
				ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8.0f);
		}
		
		ImGui.End();
	}
}
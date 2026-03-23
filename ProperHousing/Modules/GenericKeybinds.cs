using System;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ProperHousing.ProperHousing;

namespace ProperHousing;

[JsonObject(MemberSerialization.OptIn)]
public class GenericKeybinds: Module {
	public override string Name => "GenericKeybinds";
	public override bool DoDrawOption => true;
	
	[JsonProperty] private Bind RotateCounter;
	[JsonProperty] private Bind RotateClockwise;
	[JsonProperty] private Bind MoveMode;
	[JsonProperty] private Bind RotateMode;
	[JsonProperty] private Bind RemoveMode;
	[JsonProperty] private Bind StoreMode;
	[JsonProperty] private Bind CounterToggle;
	[JsonProperty] private Bind GridToggle;
	
	public GenericKeybinds() {
		RotateCounter = new(true, false, false, Key.WheelUp);
		RotateClockwise = new(true, false, false, Key.WheelDown);
		MoveMode = new(true, false, false, Key.Number1);
		RotateMode = new(true, false, false, Key.Number2);
		RemoveMode = new(true, false, false, Key.Number3);
		StoreMode = new(true, false, false, Key.Number4);
		CounterToggle = new(true, false, false, Key.Number5);
		GridToggle = new(true, false, false, Key.Number6);
		LoadConfig();
	}
	
	public override void DrawOption() {
		var changed = false;
		
		ImGui.Text($"Keybinds (?)");
		if(ImGui.IsItemHovered())
			ImGui.SetTooltip("In order to set a scrollwheel keybind you have to hover over the game and not any window");
		
		changed |= Gui.DrawKeybind("Rotate Counterclockwise", RotateCounter);
		changed |= Gui.DrawKeybind("Rotate Clockwise", RotateClockwise);
		changed |= Gui.DrawKeybind("Move Mode", MoveMode);
		changed |= Gui.DrawKeybind("Rotate Mode", RotateMode);
		changed |= Gui.DrawKeybind("Remove Mode", RemoveMode);
		changed |= Gui.DrawKeybind("Store Mode", StoreMode);
		changed |= Gui.DrawKeybind("Toggle Counter Placement", CounterToggle);
		changed |= Gui.DrawKeybind("Toggle Grid Snap", GridToggle);
		
		if(changed)
			SaveConfig();
	}
	
	public unsafe override void Tick() {
		if(layout->Manager->ActiveItem != null) {
			var delta = ((RotateCounter.Pressed() ? -1 : 0) + (RotateClockwise.Pressed() ? 1 : 0)) * Math.Max(1, Math.Abs(InputHandler.ScrollDelta)) * 15;
			if(delta != 0) {
				var r = &layout->Manager->ActiveItem->Rotation;
				var drag = 360 / 15f;
				var rot = Math.Round(Math.Atan2(r->W, r->Y) / Math.PI * drag + delta / drag);
				r->Y = (float)Math.Cos(rot / drag * Math.PI);
				r->W = (float)Math.Sin(rot / drag * Math.PI);
			}
		}
		
		void ToggleCheckbox(int nodeindex) {
			var addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName("HousingLayout");
			if(addon == null)
				return;
			
			var node = addon->UldManager.NodeList[nodeindex];
			if(node == null)
				return;
			
			var checkbox = node->GetAsAtkComponentCheckBox();
			
			ECommons.Automation.UIInput.ClickHelperExtensions.ClickCheckBox(*checkbox, addon);
		}
		
		if(MoveMode.Pressed()) ToggleCheckbox(9);
		if(RotateMode.Pressed()) ToggleCheckbox(8);
		if(!layout->Manager->PreviewMode) {
			if(RemoveMode.Pressed()) ToggleCheckbox(7);
			if(StoreMode.Pressed()) ToggleCheckbox(6);
		}
		if(CounterToggle.Pressed()) ToggleCheckbox(3);
		if(GridToggle.Pressed()) ToggleCheckbox(2);
	}
}
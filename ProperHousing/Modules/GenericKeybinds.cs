using System;
using ImGuiNET;
using Newtonsoft.Json;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ProperHousing.ProperHousing;

namespace ProperHousing;

[JsonObject(MemberSerialization.OptIn)]
public class GenericKeybinds: Module {
	public override string Name => "GenericKeybinds";
	
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
	
	public override bool DrawOption() {
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
		
		return true;
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
		
		void ToggleCheckbox(ushort index, int nodeindex) {
			var addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName("HousingLayout");
			
			var eventData = stackalloc void*[3];
			eventData[0] = null;
			eventData[1] = addon->UldManager.NodeList[nodeindex];
			eventData[2] = addon;
			
			var inputData = stackalloc void*[8];
			for(var i = 0; i < 8; i++)
				inputData[i] = null;
			
			addon->AtkEventListener.VirtualTable->ReceiveEvent(&addon->AtkEventListener, (AtkEventType)25, index, (AtkEvent*)eventData, (AtkEventData*)inputData);
		}
		
		if(MoveMode.Pressed()) ToggleCheckbox(1, 9);
		if(RotateMode.Pressed()) ToggleCheckbox(2, 8);
		if(!layout->Manager->PreviewMode) {
			if(RemoveMode.Pressed()) ToggleCheckbox(3, 7);
			if(StoreMode.Pressed()) ToggleCheckbox(4, 6);
		}
		if(CounterToggle.Pressed()) ToggleCheckbox(6, 3);
		if(GridToggle.Pressed()) ToggleCheckbox(7, 2);
	}
}
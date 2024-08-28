using System;
using ImGuiNET;
using Newtonsoft.Json;
using Dalamud.Hooking;
using static ProperHousing.ProperHousing;
using System.Numerics;

namespace ProperHousing;

[JsonObject(MemberSerialization.OptIn)]
public class ImprovedPlacement: Module {
	public override string Name => "ImprovedPlacement";
	
	[JsonProperty] private bool Enabled;
	[JsonProperty] private float GridSizeIncrement;
	[JsonProperty] private Bind GridSizeIncrease;
	[JsonProperty] private Bind GridSizeDecrease;
	[JsonProperty] private float GridSize;
	
	private unsafe delegate void SetFurniturePosDelegate(FurnitureItem* obj, Vector3* pos);
	private Hook<SetFurniturePosDelegate> SetFurniturePosHook;
	
	public unsafe ImprovedPlacement() {
		Enabled = true;
		GridSizeIncrement = 0.1f;
		GridSizeIncrease = new(false, true, false, Key.WheelUp);
		GridSizeDecrease = new(false, true, false, Key.WheelDown);
		GridSize = 0.2f;
		LoadConfig();
		
		SetFurniturePosHook = HookProv.HookFromAddress<SetFurniturePosDelegate>(SigScanner.ScanText(Sigs.SetFurniturePos), SetFurniturePos);
		if(Enabled)
			SetFurniturePosHook.Enable();
	}
	
	public override void Dispose() {
		SetFurniturePosHook.Dispose();
	}
	
	public override bool DrawOption() {
		var changed = false;
		
		if(ImGui.Checkbox("Improved Placement", ref Enabled)) {
			changed =  true;
			
			if(Enabled)
				SetFurniturePosHook.Enable();
			else
				SetFurniturePosHook.Disable();
		}
		
		changed |= ImGui.InputFloat("Grid Size Increment", ref GridSizeIncrement, 0.001f, 0.1f);
		changed |= Gui.DrawKeybind("Grid Size Increase", GridSizeIncrease);
		changed |= Gui.DrawKeybind("Grid Size Decrease", GridSizeDecrease);
		
		if(changed)
			SaveConfig();
		
		return true;
	}
	
	public override bool DrawQuick() {
		var changed = false;
		
		changed |= ImGui.InputFloat("Grid Size", ref GridSize, 0.001f, 0.1f);
		
		if(changed)
			SaveConfig();
		
		return true;
	}
	
	public override void Tick() {
		if(GridSizeIncrease.Pressed()) {
			GridSize = MathF.Min(GridSize + GridSizeIncrement, 10f);
			SaveConfig();
		}
		
		if(GridSizeDecrease.Pressed()) {
			GridSize = MathF.Max(GridSize - GridSizeIncrement, 0.001f);
			SaveConfig();
		}
	}
	
	private unsafe void SetFurniturePos(FurnitureItem* obj, Vector3* pos) {
		var manager = layout->Manager;
		if(obj == manager->ActiveItem && manager->Mode == LayoutMode.Move) {
			var screenpos = ImGui.GetMousePos();
			var ray = Project2D(screenpos);
			var hit = collisionScene.Raycast(
				ray.Item1,
				ray.Item1 + ray.Item2 * 999999,
				manager->Counter ? CollisionScene.CollisionType.All : CollisionScene.CollisionType.World,
				[(nint)obj]
			);
			
			if(hit.Hit) {
				*pos = hit.HitPos;
				
				if(manager->GridSnap) {
					var s = 1f / GridSize;
					pos->X = MathF.Round(pos->X * s) / s;
					pos->Z = MathF.Round(pos->Z * s) / s;
				}
			}
		}
		
		SetFurniturePosHook.Original(obj, pos);
	}
}
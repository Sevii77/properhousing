using System;
using ImGuiNET;
using Newtonsoft.Json;
using Dalamud.Hooking;
using static ProperHousing.ProperHousing;
using System.Numerics;
using System.Linq.Expressions;

namespace ProperHousing;

[JsonObject(MemberSerialization.OptIn)]
public class ImprovedPlacement: Module {
	public override string Name => "ImprovedPlacement";
	public override bool DoDrawOption => true;
	public override bool DoDrawQuick => true;
	
	[JsonProperty] private bool Enabled;
	[JsonProperty] private float GridSizeIncrement;
	[JsonProperty] private Bind GridSizeIncrease;
	[JsonProperty] private Bind GridSizeDecrease;
	[JsonProperty] private float GridSize;
	
	private unsafe delegate void SetFurniturePosDelegate(FurnitureItem* obj, Vector3* pos);
	private Hook<SetFurniturePosDelegate> SetFurniturePosHook;
	
	// private unsafe delegate void SwitchModeDelegate(LayoutManager* manager, LayoutMode mode, FurnitureItem* placeItem);
	// private Hook<SwitchModeDelegate> SwitchModeHook;
	
	public unsafe ImprovedPlacement() {
		Enabled = true;
		GridSizeIncrement = 0.1f;
		GridSizeIncrease = new(false, true, false, Key.WheelUp);
		GridSizeDecrease = new(false, true, false, Key.WheelDown);
		GridSize = 0.2f;
		LoadConfig();
		
		SetFurniturePosHook = HookProv.HookFromAddress<SetFurniturePosDelegate>(SigScanner.ScanText(Sigs.SetFurniturePos), SetFurniturePos);
		// SwitchModeHook = HookProv.HookFromAddress<SwitchModeDelegate>(SigScanner.ScanText(Sigs.SwitchMode), SwitchMode);
		
		if(Enabled) {
			SetFurniturePosHook.Enable();
			// SwitchModeHook.Enable();
		}
	}
	
	public override void Dispose() {
		SetFurniturePosHook.Dispose();
		// SwitchModeHook.Dispose();
	}
	
	public override void DrawOption() {
		var changed = false;
		
		if(ImGui.Checkbox("Improved Placement", ref Enabled)) {
			changed =  true;
			
			if(Enabled) {
				SetFurniturePosHook.Enable();
				// SwitchModeHook.Enable();
			} else {
				SetFurniturePosHook.Disable();
				// SwitchModeHook.Disable();
			}
		}
		
		changed |= ImGui.InputFloat("Grid Size Increment", ref GridSizeIncrement, 0.001f, 0.1f);
		changed |= Gui.DrawKeybind("Grid Size Increase", GridSizeIncrease);
		changed |= Gui.DrawKeybind("Grid Size Decrease", GridSizeDecrease);
		
		if(changed)
			SaveConfig();
	}
	
	public override void DrawQuick() {
		var changed = false;
		
		changed |= ImGui.InputFloat("Grid Size", ref GridSize, 0.001f, 0.1f);
		
		if(changed)
			SaveConfig();
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
		if(manager->Mode == LayoutMode.Place || (manager->Mode == LayoutMode.Move && obj == manager->ActiveItem)) {
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
		
		// possible way to fake visual position, so that the furniture can be placed
		// at 0,0,0 and then moved to where the user actually wants it. since the game
		// doesn't allow you to place floating furniture. (no clue if its a serverside
		// check or clientside and can be bypassed) TODO: look into this further
		// ps: we'll have to hook w/e sets the cursor since that gets set to the obj pos
		
		// if(manager->Mode == LayoutMode.Place) {
		// 	var screenpos = ImGui.GetMousePos();
		// 	var ray = Project2D(screenpos);
		// 	var hit = collisionScene.Raycast(
		// 		ray.Item1,
		// 		ray.Item1 + ray.Item2 * 999999,
		// 		manager->Counter ? CollisionScene.CollisionType.All : CollisionScene.CollisionType.World,
		// 		[(nint)obj]
		// 	);
		// 	
		// 	if(hit.Hit) {
		// 		// *pos = *pos + new Vector3(0, 1, 0);
		// 		try {
		// 			var segs = obj->ModelSegments();
		// 			for(var i = 0; i < segs.Length; i++) {
		// 				// Logger.Debug($"{i}: {(nint)segs[i]:X}");
		// 				segs[i]->Position += new Vector3(0, 1, 0);
		// 			}
		// 		} catch {}
		// 	}
		// }
	}
	
	// private unsafe void SwitchMode(LayoutManager* manager, LayoutMode mode, FurnitureItem* placeItem) {
	// 	Logger.Debug($"switch {mode} {(nint)placeItem:X} {(nint)manager->PlaceItem:X}");
	// 	
	// 	SwitchModeHook.Original(manager, mode, placeItem);
	// }
}
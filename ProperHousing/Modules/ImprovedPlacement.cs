using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImGuiNET;
using Newtonsoft.Json;
using Dalamud.Hooking;
using static ProperHousing.ProperHousing;

// TODO: cache raytrace results for the frame, pos gets called multiple times for the same object per tick (can also be used in rot then)
// TODO: just check how tabletop items are handled and get updated, try to use that system for multi select

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
	
	[JsonProperty] private bool MultiSelection;
	[JsonProperty] private bool StickyTabletop;
	
	private Dictionary<nint, Vector3> selected;
	// private Vector3 selectedCenter;
	
	private delegate void UpdateFurnitureModelDelegate(nint item);
	private UpdateFurnitureModelDelegate UpdateFurnitureModel;
	
	public unsafe ImprovedPlacement() {
		Enabled = true;
		GridSizeIncrement = 0.1f;
		GridSizeIncrease = new(false, true, false, Key.WheelUp);
		GridSizeDecrease = new(false, true, false, Key.WheelDown);
		GridSize = 0.2f;
		MultiSelection = true;
		StickyTabletop = true;
		LoadConfig();
		
		selected = [];
		
		SetFurniturePosHook = HookProv.HookFromAddress<SetFurniturePosDelegate>(SigScanner.ScanText(Sigs.SetFurniturePos), SetFurniturePos);
		SetFurnitureRotHook = HookProv.HookFromAddress<SetFurnitureRotDelegate>(SigScanner.ScanText(Sigs.SetFurnitureRot), SetFurnitureRot);
		SetFurnitureTransformHook = HookProv.HookFromAddress<SetFurnitureTransformDelegate>(SigScanner.ScanText(Sigs.SetFurnitureTransform), SetFurnitureTransform);
		// SwitchModeHook = HookProv.HookFromAddress<SwitchModeDelegate>(SigScanner.ScanText(Sigs.SwitchMode), SwitchMode);
		if(Enabled) {
			SetFurniturePosHook.Enable();
			SetFurnitureRotHook.Enable();
			SetFurnitureTransformHook.Enable();
			// SwitchModeHook.Enable();
		}
		
		SetFurnitureHaloHook = HookProv.HookFromAddress<SetFurnitureHaloDelegate>(SigScanner.ScanText(Sigs.SetFurnitureHalo), SetFurnitureHalo);
		SetActiveObjectHook = HookProv.HookFromAddress<SetActiveObjectDelegate>(SigScanner.ScanText(Sigs.SetActiveObject), SetActiveObject);
		UpdateGizmoHook = HookProv.HookFromAddress<UpdateGizmoDelegate>(SigScanner.ScanText(Sigs.UpdateGizmo), UpdateGizmo);
		// if(MultiSelection) {
		// 	SetFurnitureHaloHook.Enable();
		// 	SetActiveObjectHook.Enable();
		// 	UpdateGizmoHook.Enable();
		// }
		
		// UpdateFurnitureTransformHook = HookProv.HookFromAddress<UpdateFurnitureTransformDelegate>(SigScanner.ScanText(Sigs.UpdateFurnitureTransform), UpdateFurnitureTransform);
		// if(!StickyTabletop)
		// 	UpdateFurnitureTransformHook.Enable();
		
		UpdateFurnitureModel = Marshal.GetDelegateForFunctionPointer<UpdateFurnitureModelDelegate>(SigScanner.ScanText(Sigs.UpdateFurnitureModel));
	}
	
	public override void Dispose() {
		SetFurniturePosHook.Dispose();
		SetFurnitureRotHook.Dispose();
		SetFurnitureTransformHook.Dispose();
		SetFurnitureHaloHook.Dispose();
		SetActiveObjectHook.Dispose();
		UpdateGizmoHook.Dispose();
		// UpdateFurnitureTransformHook.Dispose();
		// SwitchModeHook.Dispose();
	}
	
	public override void DrawOption() {
		var changed = false;
		
		if(ImGui.Checkbox("Improved Placement", ref Enabled)) {
			changed = true;
			
			if(Enabled) {
				SetFurniturePosHook.Enable();
				SetFurnitureRotHook.Enable();
				SetFurnitureTransformHook.Enable();
				// SwitchModeHook.Enable();
			} else {
				SetFurniturePosHook.Disable();
				SetFurnitureRotHook.Disable();
				SetFurnitureTransformHook.Disable();
				// SwitchModeHook.Disable();
			}
		}
		
		changed |= ImGui.InputFloat("Grid Size Increment", ref GridSizeIncrement, 0.001f, 0.1f);
		changed |= Gui.DrawKeybind("Grid Size Increase", GridSizeIncrease);
		changed |= Gui.DrawKeybind("Grid Size Decrease", GridSizeDecrease);
		
		// if(ImGui.Checkbox("Multi Selection", ref MultiSelection)) {
		// 	changed = true;
		// 	
		// 	if(MultiSelection) {
		// 		SetFurnitureHaloHook.Enable();
		// 		SetActiveObjectHook.Enable();
		// 		UpdateGizmoHook.Enable();
		// 	} else {
		// 		SetFurnitureHaloHook.Disable();
		// 		SetActiveObjectHook.Disable();
		// 		UpdateGizmoHook.Disable();
		// 	}
		// }
		
		StickyTabletop = true;
		// if(ImGui.Checkbox("Sticky Tabletop", ref StickyTabletop)) {
		// 	changed = true;
		// 	
		// 	// if(StickyTabletop)
		// 	// 	UpdateFurnitureTransformHook.Disable();
		// 	// else
		// 	// 	UpdateFurnitureTransformHook.Enable();
		// }
		
		if(changed)
			SaveConfig();
	}
	
	public override void DrawQuick() {
		var changed = false;
		
		changed |= ImGui.InputFloat("Grid Size", ref GridSize, 0.001f, 0.1f);
		
		if(changed)
			SaveConfig();
	}
	
	public unsafe override void Tick() {
		if(GridSizeIncrease.Pressed()) {
			GridSize = MathF.Min(GridSize + GridSizeIncrement, 10f);
			SaveConfig();
		}
		
		if(GridSizeDecrease.Pressed()) {
			GridSize = MathF.Max(GridSize - GridSizeIncrement, 0.001f);
			SaveConfig();
		}
		
		var manager = layout->Manager;
		if(MultiSelection && manager != null) {
			if(InputHandler.KeyPressed(Key.Escape))
				selected.Clear();
			
			if(InputHandler.KeyPressed(Key.LeftButton) && ((!ImGui.IsKeyDown(ImGuiKey.ModShift) && manager->HoverItem == null) || (manager->Mode == LayoutMode.Move && manager->HoverItem != null)))
				selected.Clear();
			
			// used to avoid closing housing on escape. TODO: hook the function that closes it on esc
			// if(selected.Count > 0 && manager->ActiveItem == null)
			// 	manager->ActiveItem = (FurnitureItem*)selected.Keys.First();
			
			if(manager->Mode == LayoutMode.Move) {
				var screenpos = ImGui.GetMousePos();
				var ray = Project2D(screenpos);
				var hit = collisionScene.Raycast(
					ray.Item1,
					ray.Item1 + ray.Item2 * 999999,
					manager->Counter ? CollisionScene.CollisionType.All : CollisionScene.CollisionType.World,
					selected.Select(v => v.Key).ToArray()
				);
				
				if(hit.Hit) {
					if(manager->GridSnap) {
						var s = 1f / GridSize;
						hit.HitPos.X = MathF.Round(hit.HitPos.X * s) / s;
						hit.HitPos.Z = MathF.Round(hit.HitPos.Z * s) / s;
					}
					
					var selectedCenter = Vector3.Zero;
					foreach(var v in selected)
						selectedCenter += v.Value / selected.Count;
					
					foreach(var v in selected) {
						// Logger.Debug($"move {v.Key:X}");
						((FurnitureItem*)v.Key)->Position = hit.HitPos + (v.Value - selectedCenter);
						UpdateFurnitureModel(v.Key + 0x80);
					}
				}
			}
		}
	}
	
	private unsafe void UpdateFurnitureModel2(FurnitureItem* obj) {
		UpdateFurnitureModel((nint)obj + 0x80);
		if(StickyTabletop && obj->FurnitureItemExtras != null && obj->FurnitureItemExtras->Children.Count > 0)
			UpdateFurnitureModel((nint)obj->FurnitureItemExtras + 0x18);
	}
	
	private unsafe delegate void SetFurniturePosDelegate(FurnitureItem* obj, Vector3* pos);
	private Hook<SetFurniturePosDelegate> SetFurniturePosHook;
	private unsafe void SetFurniturePos(FurnitureItem* obj, Vector3* pos) {
		var manager = layout->Manager;
		if(!(manager->Mode == LayoutMode.Place || (manager->Mode == LayoutMode.Move && obj == manager->ActiveItem))) {
			SetFurniturePosHook.Original(obj, pos);
			return;
		}
		
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
		
		obj->Position = *pos;
		UpdateFurnitureModel2(obj);
	}
	
	private unsafe delegate void SetFurnitureRotDelegate(FurnitureItem* obj, Quaternion* rot);
	private Hook<SetFurnitureRotDelegate> SetFurnitureRotHook;
	private unsafe void SetFurnitureRot(FurnitureItem* obj, Quaternion* rot) {
		// var manager = layout->Manager;
		// if(manager->Mode == LayoutMode.Place || (manager->Mode == LayoutMode.Move && obj == manager->ActiveItem)) {
		// 	var screenpos = ImGui.GetMousePos();
		// 	var ray = Project2D(screenpos);
		// 	var hit = collisionScene.Raycast(
		// 		ray.Item1,
		// 		ray.Item1 + ray.Item2 * 999999,
		// 		manager->Counter ? CollisionScene.CollisionType.All : CollisionScene.CollisionType.World,
		// 		[(nint)obj]
		// 	);
		// 	
		// 	if(hit.Hit && hit.HitDir.Y < 0.5) {
		// 		var ang = MathF.Atan2(hit.HitDir.X, hit.HitDir.Z);
		// 		*rot = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), ang);
		// 	} else {
		// 		var ang = MathF.Atan2(rot->Y, rot->W);
		// 		*rot = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), ang);
		// 	}
		// }
		
		SetFurnitureRotHook.Original(obj, rot);
	}
	
	private unsafe delegate void SetFurnitureTransformDelegate(FurnitureItem* obj, Transform* transform);
	private Hook<SetFurnitureTransformDelegate> SetFurnitureTransformHook;
	private unsafe void SetFurnitureTransform(FurnitureItem* obj, Transform* transform) {
		SetFurnitureTransformHook.Original(obj, transform);
		
		// if(StickyTabletop) {
		// 	SetFurnitureTransformHook.Original(obj, transform);
		// 	return;
		// }
		// 
		// Logger.Debug("transform");
		// obj->Position = transform->Position;
		// obj->Rotation = transform->Rotation;
		// obj->Scale = transform->Scale;
		// UpdateFurnitureModel2(obj);
		
		// doesnt have this (goto skips updating its own visual model) but seems to work fine so w/e i guess
		// if (*(longlong **)(param_1 + 0x110) != (longlong *)0x0) {
		// 	cVar5 = (**(code **)(**(longlong **)(param_1 + 0x110) + 0x28))();
		// 	if (cVar5 != '\0') {
		// 		FUN_140734a30(*(undefined8 *)(param_1 + 0x110));
		// 		goto LAB_1406dcfb4;
		// 	}
		// }
	}
	
	// private unsafe delegate void SwitchModeDelegate(LayoutManager* manager, LayoutMode mode, FurnitureItem* placeItem);
	// private Hook<SwitchModeDelegate> SwitchModeHook;
	// private unsafe void SwitchMode(LayoutManager* manager, LayoutMode mode, FurnitureItem* placeItem) {
	// 	Logger.Debug($"switch {mode} {(nint)placeItem:X} {(nint)manager->PlaceItem:X}");
	// 	
	// 	SwitchModeHook.Original(manager, mode, placeItem);
	// }
	
	private unsafe delegate void SetFurnitureHaloDelegate(FurnitureItem* obj, HaloColor halo);
	private Hook<SetFurnitureHaloDelegate> SetFurnitureHaloHook;
	private unsafe void SetFurnitureHalo(FurnitureItem* obj, HaloColor halo) {
		if(selected.ContainsKey((nint)obj))
			halo = HaloColor.Selected;
		
		SetFurnitureHaloHook.Original(obj, halo);
	}
	
	private unsafe delegate void SetActiveObjectDelegate(LayoutManager* manager, FurnitureItem* obj);
	private Hook<SetActiveObjectDelegate> SetActiveObjectHook;
	private unsafe void SetActiveObject(LayoutManager* manager, FurnitureItem* obj) {
		if(!MultiSelection) {
			SetActiveObjectHook.Original(manager, obj);
			return;
		}
		
		if(manager->Mode == LayoutMode.Move && selected.Count > 0)
			return;
		
		if(manager->Mode != LayoutMode.Rotate) {
			SetActiveObjectHook.Original(manager, obj);
			return;
		}
		
		if(ImGui.IsKeyDown(ImGuiKey.ModShift)) {
			if(!selected.Remove((nint)obj))
				selected.Add((nint)obj, obj->Position);
		} else {
			selected.Clear();
			selected.Add((nint)obj, obj->Position);
		}
	}
	
	private unsafe delegate void UpdateGizmoDelegate(LayoutManagerSub* submanager, Transform* transform);
	private Hook<UpdateGizmoDelegate> UpdateGizmoHook;
	private unsafe void UpdateGizmo(LayoutManagerSub* submanager, Transform* transform) {
		UpdateGizmoHook.Original(submanager, transform);
		
		// if(!MultiSelection || selected.Count == 0)
		// 	return;
		// 
		// var gizmo = submanager->Gizmo;
		// if(gizmo == null)
		// 	return;
		// 
		// gizmo->Position = Vector3.Zero;
	}
	
	// /// ptr = FurnitureItemExtras* + 0x18
	// private unsafe delegate void UpdateFurnitureTransformDelegate(FurnitureItem* obj, Transform* transform);
	// private Hook<UpdateFurnitureTransformDelegate> UpdateFurnitureTransformHook;
	// private unsafe void SetFurnitureTransform(FurnitureItem* obj, Transform* transform) {
	// 	obj->Position = transform->Position;
	// 	obj->Rotation = transform->Rotation;
	// 	obj->Scale = transform->Scale;
	// 	UpdateFurnitureModel((nint)obj + 0x80);
	// 	
	// 	// var org = *(nint*)(ptr + 0x18);
	// 	// *(nint*)(ptr + 0x18) = *(nint*)(ptr + 0x10);
	// 	// UpdateFurnitureChildrenHook.Original(ptr);
	// 	// *(nint*)(ptr + 0x18) = org;
	// }
}
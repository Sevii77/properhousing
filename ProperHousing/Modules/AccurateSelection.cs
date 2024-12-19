using System;
using System.Numerics;
using System.Collections.Generic;
using ImGuiNET;
using Newtonsoft.Json;
using Dalamud.Hooking;
using static ProperHousing.ProperHousing;
using Microsoft.VisualBasic;

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
using System;
using System.Runtime.InteropServices;
using ImGuiNET;
using Newtonsoft.Json;
using Dalamud.Hooking;
using static ProperHousing.ProperHousing;

namespace ProperHousing;

[JsonObject(MemberSerialization.OptIn)]
public class AccurateSelection: Module {
	public override string Name => "AccurateSelection";
	public override bool DoDrawOption => true;
	
	[JsonProperty] private bool AccurateSelect;
	
	private unsafe delegate FurnitureItem* GetHoverObjectDelegate(nint unk1);
	private Hook<GetHoverObjectDelegate> GetHoverObjectHook;
	
	// private unsafe delegate ulong IdkDelegate(ulong unk1, ulong unk2);
	// private Hook<IdkDelegate> IdkHook;
	
	private delegate ushort GetOutdoorIndexDelegate(byte plot, byte index);
	private GetOutdoorIndexDelegate GetOutdoorIndex;
	
	public unsafe AccurateSelection() {
		AccurateSelect = true;
		LoadConfig();
		
		GetHoverObjectHook = HookProv.HookFromAddress<GetHoverObjectDelegate>(SigScanner.ScanText(Sigs.GetHoverObject), GetHoverObject);
		if(AccurateSelect)
			GetHoverObjectHook.Enable();
		
		// IdkHook = HookProv.HookFromAddress<IdkDelegate>(SigScanner.ScanText("48 83 EC 28 48 8B 02 48 8B CA FF 50 ?? 83 F8 0C"), Idk);
		// IdkHook.Enable();
		
		// Logger.Debug($"{(nint)housing->Outdoor:X} - {(nint)housing->Outdoor + 0x8980:X} - {(nint)housing->Outdoor + 0x8980 + 0x8 * 400:X}");
		
		GetOutdoorIndex = Marshal.GetDelegateForFunctionPointer<GetOutdoorIndexDelegate>(SigScanner.ScanText(Sigs.GetOutdoorIndex));
	}
	
	public override void Dispose() {
		GetHoverObjectHook.Dispose();
		// IdkHook.Dispose();
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
			nint[]? whitelist = null;
			// if(!housing->IsIndoor) {
			// 	whitelist = [];
			// 	
			// }
			var hit = collisionScene.Raycast(ray.Item1, ray.Item1 + ray.Item2 * 999999, CollisionScene.CollisionType.All, null, whitelist);
			
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
	
	// return value is 0/1 if it is owned by the current plot(?)
	// private unsafe ulong Idk(ulong idk1, ulong furnitureItem) {
	// 	var v = IdkHook.Original(idk1, idk2);
	// 	Logger.Debug($"Idk: {idk1:X} {idk2:X} {v:X}");
	// 	return v;
	// }
}
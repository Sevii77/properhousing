using System;
using ImGuiNET;
using Newtonsoft.Json;
using Dalamud.Hooking;
using static ProperHousing.ProperHousing;
using System.Numerics;

namespace ProperHousing;

[JsonObject(MemberSerialization.OptIn)]
public class CameraFollow: Module {
	public override string Name => "CameraFollow";
	
	[JsonProperty] private bool Enabled;
	
	private unsafe delegate void CameraHandleDelegate(nint a);
	private Hook<CameraHandleDelegate> CameraHandleHook;
	
	public unsafe CameraFollow() {
		Enabled = false;
		LoadConfig();
		
		CameraHandleHook = HookProv.HookFromAddress<CameraHandleDelegate>(SigScanner.ScanText(Sigs.CameraHandle), SetCameraOrigin);
		if(Enabled)
			CameraHandleHook.Enable();
	}
	
	public override void Dispose() {
		CameraHandleHook.Dispose();
	}
	
	public override bool DrawOption() {
		if(ImGui.Checkbox("Camera Follow", ref Enabled)) {
			SaveConfig();
			
			if(Enabled)
				CameraHandleHook.Enable();
			else
				CameraHandleHook.Disable();
		}
		
		return true;
	}
	
	private unsafe void SetCameraOrigin(nint a) {
		if(!Enabled)
			goto og;
		
		var manager = layout->Manager;
		if(manager == null)
			goto og;
		
		if(manager->Mode != LayoutMode.Rotate)
			goto og;
		
		var active = manager->ActiveItem;
		if(active == null)
			goto og;
		
		// Logger.Debug("set pos");
		CameraHandleHook.Original(a);
		camera->LookAt = active->Position;
		camera->Pos = active->Position + new Vector3(MathF.Cos(camera->HRotation), 1, MathF.Sin(camera->HRotation)) * camera->Zoom;
		var b = Quaternion.Identity;
		camera->Angle = new Vector4(b.X, b.Y, b.Z, b.W);
		
		return;
		
		og:
		CameraHandleHook.Original(a);
	}
}
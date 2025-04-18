namespace ProperHousing;

public class Sigs {
	public static readonly string CameraZoom = "F3 0F 11 4C 24 ?? 4C 8B C9";
	public static readonly string CameraHandle = "E8 ?? ?? ?? ?? 48 83 3D ?? ?? ?? ?? ?? 74 0C";
	public static readonly string HousingStruct = "48 8B 05 ?? ?? ?? ?? 8B 52 ?? 48 83 78 ?? 00 74 ?? 48 8D 8E ?? ?? ?? ??";
	public static readonly string LayoutStruct = "48 8B 0D ?? ?? ?? ?? 85 C0 74 15";
	public static readonly string GetHoverObject = "E8 ?? ?? ?? ?? 45 33 E4 48 89 45 ?? 48 8B BD ?? ?? ?? ??";
	public static readonly string SetActiveObject = "48 85 D2 0F 84 ?? ?? ?? ?? 53 41 56 48 83 EC 48 48 89 6C 24 ??";
	public static readonly string SetFurniturePos = "40 53 48 83 EC 20 8B 02 48 8B D9 89 41 50 8B 42 04 89 41 54 8b 42 08 89 41 58 48";
	public static readonly string SetFurnitureRot = "40 53 48 83 EC 20 0F 28 02 48 8B D9";
	public static readonly string SetFurnitureTransform = "40 53 48 83 EC 20 8B 02 48 8B D9 89 41 ?? 8B 42 ?? 89 41 ?? 8B 42 ?? 89 41 ?? 0F 10 42 ??";
	public static readonly string SwitchMode = "40 55 56 41 56 48 83 EC 20 48 63 EA";
	public static readonly string SetFurnitureHalo = "48 89 6C 24 ?? 56 48 83 EC 20 48 8B 81 ?? ?? ?? ?? 8B EA";
	public static readonly string UpdateGizmo = "40 53 56 48 81 EC C8 00 00 00 0F 29 B4 24 ?? ?? ?? ??";
	
	public static readonly string UpdateFurnitureModel = "48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 50 48 8B E9 48 8B 49 ??";
	public static readonly string GetOutdoorIndex = "48 89 5C 24 ?? 57 48 83 EC 20 0F B6 D9 0F B6 FA";
}

// of interest, seems to update visual models i think
// 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 50 48 8B E9 48 8B 49 ??
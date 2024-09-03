namespace ProperHousing;

public class Sigs {
	public static readonly string CameraZoom = "F3 0F 11 4C 24 ?? 4C 8B C9";
	public static readonly string CameraHandle = "E8 ?? ?? ?? ?? 48 83 3D ?? ?? ?? ?? ?? 74 0C";
	public static readonly string HousingStruct = "48 8B 05 ?? ?? ?? ?? 8B 52 ?? 48 83 78 ?? 00 74 ?? 48 8D 8E ?? ?? ?? ??";
	public static readonly string LayoutStruct = "48 8B 0D ?? ?? ?? ?? 85 C0 74 15";
	public static readonly string GetHoverObject = "E8 ?? ?? ?? ?? 45 33 E4 48 89 45 ?? 48 8B BD ?? ?? ?? ??";
	public static readonly string SetFurniturePos = "40 53 48 83 EC 20 8B 02 48 8B D9 89 41 50 8B 42 04 89 41 54 8b 42 08 89 41 58 48";
	public static readonly string SwitchMode = "40 55 56 41 56 48 83 EC 20 48 63 EA";
}
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ProperHousing {
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Camera {
		[FieldOffset(0x1B0)] public float X;
		[FieldOffset(0x1B4)] public float Y;
		[FieldOffset(0x1B8)] public float Z;
		
		public Vector3 Pos => new Vector3(X, Y, Z);
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Housing {
		[FieldOffset(0x08)] public HousingManager* Outdoor;
		[FieldOffset(0x10)] public HousingManager* Indoor;
		
		public bool IsOutdoor => Outdoor != null;
		
		public HousingManager* CurrentZone() {
			if(Outdoor != null)
				return Outdoor;
				
			if(Indoor != null)
				return Indoor;
			
			return null;
		}
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct HousingManager {
		[FieldOffset(0x8980)] public fixed ulong Objects[400];
		[FieldOffset(0x96E8)] public Furniture* IndoorGhostObject;
		[FieldOffset(0x96F0)] public Furniture* IndoorHoverObject;
		[FieldOffset(0x96F8)] public Furniture* IndoorActiveObject;
		[FieldOffset(0x9AB8)] public Furniture* OutdoorGhostObject;
		[FieldOffset(0x9AC0)] public Furniture* OutdoorHoverObject;
		[FieldOffset(0x9AC8)] public Furniture* OutdoorActiveObject;
		
		public Furniture* Furniture(int i) {
			if(Objects[i] == 0)
				return null;
			
			return (Furniture*)Objects[i];
		}
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Furniture {
		[FieldOffset(0x30)] private fixed byte name[64];
		[FieldOffset(0x80)] public uint ID;
		[FieldOffset(0xA0)] public float X;
		[FieldOffset(0xA4)] public float Y;
		[FieldOffset(0xA8)] public float Z;
		[FieldOffset(0xB0)] public float Rotation;
		[FieldOffset(0xF8)] public IntPtr Item;
		
		public Vector3 Pos => new Vector3(X, Y, Z);
		public string Name {
			get {
				unsafe {
					fixed(byte* a = &name[0]) {
						var nam = (sbyte*)a;
						return new String(nam, 0, 64);
					}
				}
			}
		}
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Layout {
		[FieldOffset(0x40)] public LayoutManager* Manager;
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct LayoutManager {
		[FieldOffset(0x10)] public IntPtr HoverItem;
		[FieldOffset(0x70)] public bool PreviewMode;
	}
}
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
		
		public Furniture? Furniture(int i) {
			if(Objects[i] == 0)
				return null;
			
			return Marshal.PtrToStructure<Furniture>((IntPtr)Objects[i]);
		}
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Furniture {
		[FieldOffset(0x80)] public uint ID;
		[FieldOffset(0xA0)] public float X;
		[FieldOffset(0xA4)] public float Y;
		[FieldOffset(0xA8)] public float Z;
		[FieldOffset(0xB0)] public float Rotation;
		[FieldOffset(0xF8)] public IntPtr Item;
		
		public Vector3 Pos => new Vector3(X, Y, Z);
	}
}
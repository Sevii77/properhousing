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
		[FieldOffset(0xF8)] public FurnitureItem* Item;
		
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
		
		// I've given up, this mess will have to do
		public FurnitureItemSegment*[] ModelSegments(int len) {
			if(this.Item->Idk == IntPtr.Zero)
				return new FurnitureItemSegment*[0];
			
			var l = new FurnitureItemSegment*[len];
			for(var i = 0; i < len; i++) {
				try {
					l[i] = ((FurnitureItemIdk1*)Marshal.ReadIntPtr(this.Item->Idk + i * 8))->Idk->Idk->Idk->Piece->Segment;
				} catch {
					l[i] = ((FurnitureItemIdk4*)Marshal.ReadIntPtr(this.Item->Idk + i * 8))->Piece->Segment;
				}
				
				try { // Wondrous Parfait breaks, TODO: fix that
					var _ = l[i]->Pos;
				} catch {
					l[i] = (FurnitureItemSegment*)this.Item;
				}
			}
			
			return l;
		}
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct FurnitureItem {
		[FieldOffset(0x90)] public IntPtr Idk;
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct FurnitureItemIdk1 {
		[FieldOffset(0x10)] public FurnitureItemIdk2* Idk;
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct FurnitureItemIdk2 {
		[FieldOffset(0x90)] public FurnitureItemIdk3* Idk;
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct FurnitureItemIdk3 {
		[FieldOffset(0x00)] public FurnitureItemIdk4* Idk;
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct FurnitureItemIdk4 {
		[FieldOffset(0x10)] public FurnitureItemPiece* Piece;
	}
	
	// first param of (48 89 5C 24 ?? 57 48 83 EC 60 48 8B D9 48 8B FA)
	[StructLayout(LayoutKind.Explicit, Size = 0x110)]
	public unsafe struct FurnitureItemPiece {
		[FieldOffset(0x30)] public FurnitureItemSegment* Segment;
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct FurnitureItemSegment {
		[FieldOffset(0x18)] public FurnitureItemSegment* LinkedRoot; // idk if root, but i assume so since its always the same address
		[FieldOffset(0x20)] public FurnitureItemSegment* LinkedPrev;
		[FieldOffset(0x28)] public FurnitureItemSegment* LinkedNext;
		
		[FieldOffset(0x50)] public Vector3 Pos;
		[FieldOffset(0x60)] public Quaternion Rotation;
		[FieldOffset(0x70)] public Vector3 Scale;
		[FieldOffset(0x80)] public IntPtr Segments; // (8,ptr,8)[]
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Layout {
		[FieldOffset(0x40)] public LayoutManager* Manager;
	}
	
	public enum LayoutMode: uint {
		None = 0,
		Move = 1,
		Rotate = 2,
		Remove = 3,
		Place = 4,
		Dye = 5,
		Store = 6,
	}
	
	[Flags]
	public enum LayoutToggles: uint {
		InHousingMode = 0x1,
		Snap = 0x001,
		Counter = 0x00001,
	}
	
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct LayoutManager {
		[FieldOffset(0x000)] public LayoutMode Mode;
		[FieldOffset(0x010)] public IntPtr HoverItem;
		[FieldOffset(0x070)] public bool PreviewMode;
		[FieldOffset(0x170)] public LayoutToggles Toggles;
	}
}
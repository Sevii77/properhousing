using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ProperHousing;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct Camera {
	[FieldOffset(0x1B0)] public float X;
	[FieldOffset(0x1B4)] public float Y;
	[FieldOffset(0x1B8)] public float Z;
	
	public Vector3 Pos => new Vector3(X, Y, Z);
}

// TODO: figure out what island sanctuary is doing so that can be supported
[StructLayout(LayoutKind.Explicit)]
public unsafe struct Housing {
	[FieldOffset(0x08)] public HousingManager* Outdoor;
	[FieldOffset(0x10)] public HousingManager* Indoor;
	
	public bool IsOutdoor => Outdoor != null;
	public bool IsIndoor => Indoor != null;
	
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
	[FieldOffset(0x96F0)] public Furniture* IndoorGhostObject;
	[FieldOffset(0x96F8)] public Furniture* IndoorHoverObject;
	[FieldOffset(0x9700)] public Furniture* IndoorActiveObject;
	// TODO: update these if i ever support outside
	// [FieldOffset(0x9AB8)] public Furniture* OutdoorGhostObject;
	// [FieldOffset(0x9AC0)] public Furniture* OutdoorHoverObject;
	// [FieldOffset(0x9AC8)] public Furniture* OutdoorActiveObject;
	
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
	// [FieldOffset(0xA0)] public float X;
	// [FieldOffset(0xA4)] public float Y;
	// [FieldOffset(0xA8)] public float Z;
	// [FieldOffset(0xB0)] public float Rotation;
	// [FieldOffset(0xF8)] public FurnitureItem* Item;
	[FieldOffset(0xB0)] public float X;
	[FieldOffset(0xB4)] public float Y;
	[FieldOffset(0xB8)] public float Z;
	[FieldOffset(0xC0)] public float Rotation;
	[FieldOffset(0x108)] public FurnitureItem* Item;
	
	public Vector3 Pos => new Vector3(X, Y, Z);
	public string Name {
		get {
			unsafe {
				fixed(byte* a = &name[0]) {
					var nam = (sbyte*)a;
					return new String(nam, 0, 64).Split('\0')[0];
				}
			}
		}
	}
	
	// I've given up, this mess will have to do
	public FurnitureModelSegment*[] ModelSegments(int len) {
		if(this.Item->Model == null || this.Item->Model->Pieces == IntPtr.Zero)
			return new FurnitureModelSegment*[0];
		
		var l = new FurnitureModelSegment*[len];
		for(var i = 0; i < len; i++) {
			var ptr = ((FurnitureModelIdk*)Marshal.ReadIntPtr(this.Item->Model->Pieces + i * 8));
			
			// Super nasty hack since i dont want to figure out how to properly solve it
			// TODO: properly solve it
			try {
				l[i] = ptr->Piece->Segment;
				var _ = l[i]->Position;
				// Dalamud.Logging.PluginLog.Log($"- Success: {((IntPtr)ptr->Piece->Segment).ToString("X")}");
			} catch {
				// Dalamud.Logging.PluginLog.Log($"- Failed: {((IntPtr)ptr->Piece->Segment).ToString("X")}");
				l[i] = (FurnitureModelSegment*)this.Item;
			}
		}
		
		return l;
	}
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FurnitureItem {
	[FieldOffset(0x50)] public Vector3 Position;
	[FieldOffset(0x60)] public Quaternion Rotation;
	[FieldOffset(0x70)] public Vector3 Scale;
	[FieldOffset(0x88)] public FurnitureModel* Model;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FurnitureModel {
	[FieldOffset(0x90)] public IntPtr Pieces;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FurnitureModelIdk {
	[FieldOffset(0x10)] public FurnitureModelPiece* Piece;
}

// first param of (48 89 5C 24 ?? 57 48 83 EC 60 48 8B D9 48 8B FA)
[StructLayout(LayoutKind.Explicit, Size = 0x110)]
public unsafe struct FurnitureModelPiece {
	[FieldOffset(0x30)] public FurnitureModelSegment* Segment;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FurnitureModelSegment {
	[FieldOffset(0x18)] public FurnitureModelSegment* LinkedRoot; // idk if root, but i assume so since its always the same address
	[FieldOffset(0x20)] public FurnitureModelSegment* LinkedPrev;
	[FieldOffset(0x28)] public FurnitureModelSegment* LinkedNext;
	
	[FieldOffset(0x50)] public Vector3 Position;
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

[StructLayout(LayoutKind.Explicit)]
public unsafe struct LayoutManager {
	[FieldOffset(0x000)] public LayoutMode Mode;
	[FieldOffset(0x004)] public LayoutMode LastMode;
	[FieldOffset(0x010)] public FurnitureItem* HoverItem;
	[FieldOffset(0x018)] public FurnitureItem* ActiveItem;
	[FieldOffset(0x070)] public bool PreviewMode;
	[FieldOffset(0x170)] public bool HousingMode;
	[FieldOffset(0x171)] public bool GridSnap;
	[FieldOffset(0x172)] public bool Counter;
}
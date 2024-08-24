using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ProperHousing;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct Camera {
	[FieldOffset(0x1A0)] public float AngleX;
	[FieldOffset(0x1A4)] public float AngleY;
	[FieldOffset(0x1A8)] public float AngleZ;
	[FieldOffset(0x1BC)] public float AngleW;
	
	[FieldOffset(0x1B0)] public float X;
	[FieldOffset(0x1B4)] public float Y;
	[FieldOffset(0x1B8)] public float Z;
	
	public Quaternion Angle => new Quaternion(AngleX, AngleY, AngleZ, AngleW);
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
	
	public FurnitureModelSegment*[] ModelSegments() => Item->ModelSegments();
	// public FurnitureModelSegment*[] ModelSegments() {
	// 	// PluginLog.Log($"{Name}");
	// 	return Item->ModelSegments();
	// }
	
	// // I've given up, this mess will have to do
	// public FurnitureModelSegment*[] ModelSegments(int len) {
	// 	if(this.Item->Model == null || this.Item->Model->Pieces == IntPtr.Zero)
	// 		return new FurnitureModelSegment*[0];
	// 	
	// 	var l = new FurnitureModelSegment*[len];
	// 	for(var i = 0; i < len; i++) {
	// 		var ptr = ((FurnitureModelIdk*)Marshal.ReadIntPtr(this.Item->Model->Pieces + i * 8));
	// 		
	// 		// Super nasty hack since i dont want to figure out how to properly solve it
	// 		// TODO: properly solve it
	// 		try {
	// 			l[i] = ptr->Piece->Segment;
	// 			var _ = l[i]->Position;
	// 			// PluginLog.Log($"- Success: {((IntPtr)ptr->Piece->Segment).ToString("X")} ({Name})");
	// 		} catch {
	// 			// PluginLog.Log($"- Failed: {((IntPtr)ptr->Piece->Segment).ToString("X")} ({Name})");
	// 			l[i] = (FurnitureModelSegment*)this.Item;
	// 		}
	// 	}
	// 	
	// 	return l;
	// }
}

// function called to update furniture visual models, param_1 is FurnitureItem* + 0x80
// void FUN_14059e890(longlong param_1)
// {
//   longlong lVar1;
//   longlong *plVar2;
//   undefined8 uVar3;
//   ulonglong uVar4;
//   ulonglong uVar5;
//   undefined local_38 [48];
//   
//   uVar3 = (**(code **)(**(longlong **)(param_1 + 8) + 0x230))();
//   uVar5 = 0;
//   uVar4 = *(longlong *)(param_1 + 0x18) - *(longlong *)(param_1 + 0x10) >> 3;
//   if (uVar4 != 0) {
//     do {
//       lVar1 = *(longlong *)(*(longlong *)(param_1 + 0x10) + uVar5 * 8);
//       FUN_140625480(local_38,lVar1 + 0x20,uVar3);
//       plVar2 = *(longlong **)(lVar1 + 0x10);
//       (**(code **)(*plVar2 + 0x238))(plVar2,local_38);
//       uVar5 = uVar5 + 1;
//     } while (uVar5 < uVar4);
//   }
//   return;
// }
[StructLayout(LayoutKind.Explicit)]
public unsafe struct FurnitureItem {
	[FieldOffset(0x50)] public Vector3 Position;
	[FieldOffset(0x60)] public Quaternion Rotation;
	[FieldOffset(0x70)] public Vector3 Scale;
	// [FieldOffset(0x88)] public FurnitureModel* Model;
	[FieldOffset(0x90)] public ulong PiecesA;
	[FieldOffset(0x98)] public ulong PiecesB;
	
	// uVar4
	public uint PiecesCount {
		get {
			return (uint)((PiecesB - PiecesA) >> 3);
		}
	}
	
	public FurnitureModelPiece*[] Pieces() {
		var l = new FurnitureModelPiece*[PiecesCount];
		// var minIndex = int.MaxValue;
		// for(var i = 0; i < PiecesCount; i++)
		// 	minIndex = Math.Min(minIndex, ((FurnitureModelIdk*)Marshal.ReadIntPtr((IntPtr)this.PiecesA + i * 8))->Piece->Index);
		
		for(var i = 0; i < PiecesCount; i++) {
			var ptr = (FurnitureModelIdk*)Marshal.ReadIntPtr((IntPtr)this.PiecesA + i * 8);
			
			l[i] = ptr->Piece;
			// try {
			// 	l[ptr->Piece->Index - minIndex] = ptr->Piece;
			// } catch {
			// 	// PluginLog.Log($"len: {PiecesCount}; index: {ptr->Piece->Index - minIndex}");
			// }
		}
		
		return l;
	}
	
	// only gets pieces with type 1 (model)
	public FurnitureModelSegment*[] ModelSegments() {
		var pieces = Pieces();
		var len = 0;
		foreach(var v in pieces)
			if(v->Type == 1)
				len += 1;
		
		var l = new FurnitureModelSegment*[len];
		var i = 0;
		foreach(var v in pieces)
			if(v->Type == 1) {
				l[i] = v->Segment;
				i++;
			}
		
		return l;
	}
}

// [StructLayout(LayoutKind.Explicit)]
// public unsafe struct FurnitureModel {
// 	[FieldOffset(0x90)] public IntPtr Pieces;
// }

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FurnitureModelIdk {
	[FieldOffset(0x10)] public FurnitureModelPiece* Piece;
}

// first param of (48 89 5C 24 ?? 57 48 83 EC 60 48 8B D9 48 8B FA)
// all comments are for model 1268 (Starry Sky Phasmascape), it may be different for others
[StructLayout(LayoutKind.Explicit, Size = 0x110)]
public unsafe struct FurnitureModelPiece {
	[FieldOffset(0x00)] public IntPtr Unk1; // all pieces with the same type point to the same thing, probably vtbl for rendering the piece or smth idfk
	[FieldOffset(0x08)] public IntPtr Unk2; // all pieces point to the same object
	[FieldOffset(0x10)] public IntPtr Unk3; // all pieces point to the same different object
	
	[FieldOffset(0x18)] public byte UnkIncr; // starts at 2 and increases with 4 every ArrayIndex (2, 6, 10, 14...)
	
	// 1: model
	// 3: light (for windows)
	// 68: unk (doesnt have Segment)
	// 70: unk (doesnt have Segment)
	[FieldOffset(0x19)] public byte Type;
	[FieldOffset(0x23)] public byte Index; // not in same order as array index and 1 indexed most of the time but not always (3, 1, 2, 4, 5, 6, 7, 8, 9, 10)
	[FieldOffset(0x28)] public byte ArrayIndex; // same order as array and 0 indexed
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
	[FieldOffset(0x20)] public HouseLayout* HouseLayout;
	[FieldOffset(0x40)] public LayoutManager* Manager;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct HouseLayout {
	[FieldOffset(0x20)] public uint Territory;
	[FieldOffset(0x90)] public IndoorLayout* Layout;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct IndoorLayout {
	[FieldOffset(0x28)] public FloorLayout Floor1;
	[FieldOffset(0x3C)] public FloorLayout Floor2;
	[FieldOffset(0x50)] public FloorLayout Floor3;
	[FieldOffset(0x64)] public FloorLayout Floor4;
	[FieldOffset(0x78)] public fixed byte Unk[8];
	[FieldOffset(0x80)] public float Lightlevel;
}

[StructLayout(LayoutKind.Explicit, Size = 0x14)]
public unsafe struct FloorLayout {
	[FieldOffset(0x0)] public fixed int Fixtures[5];
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
	[FieldOffset(0x088)] public bool PreviewMode;
	[FieldOffset(0x180)] public bool HousingMode;
	[FieldOffset(0x181)] public bool GridSnap;
	[FieldOffset(0x182)] public bool Counter;
}
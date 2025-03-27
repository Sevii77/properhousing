using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.STD;

namespace ProperHousing;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct Camera {
	[FieldOffset(0x010)] public FFXIVClientStructs.FFXIV.Common.Math.Matrix4x4 ProjMatrix;
	
	[FieldOffset(0x060)] public FFXIVClientStructs.FFXIV.Common.Math.Vector3 Pos;
	[FieldOffset(0x090)] public FFXIVClientStructs.FFXIV.Common.Math.Vector3 LookAt;
	[FieldOffset(0x114)] public float Zoom;
	[FieldOffset(0x130)] public float HRotation;
	[FieldOffset(0x134)] public float VRotation;
	[FieldOffset(0x1A0)] public FFXIVClientStructs.FFXIV.Common.Math.Vector4 Angle;
	// [FieldOffset(0x1B0)] public FFXIVClientStructs.FFXIV.Common.Math.Vector3 Pos;
	
	[FieldOffset(0x1F0)] public float NearPlane;
	[FieldOffset(0x1F4)] public float FarPlane;
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
	
	[FieldOffset(0x96A2)] public byte Ward;
	[FieldOffset(0x96A8)] public byte Plot;
	
	[FieldOffset(0x96F0)] public Furniture* IndoorGhostObject;
	[FieldOffset(0x96F8)] public Furniture* IndoorHoverObject;
	[FieldOffset(0x9700)] public Furniture* IndoorActiveObject;
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
	[FieldOffset(0xB0)] public Vector3 Position;
	[FieldOffset(0xC0)] public float Rotation;
	[FieldOffset(0xF8)] public FurnitureItem* Item;
	
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
	// [FieldOffset(0x50)] public Transform Transform;
	[FieldOffset(0x50)] public Vector3 Position;
	[FieldOffset(0x60)] public Quaternion Rotation;
	[FieldOffset(0x70)] public Vector3 Scale;
	// [FieldOffset(0x88)] public FurnitureModel* Model;
	[FieldOffset(0x90)] public nint PiecesStart;
	[FieldOffset(0x98)] public nint PiecesEnd;
	
	[FieldOffset(0xF8)] public FurnitureItemExtras* FurnitureItemExtras;
	
	// uVar4
	public uint PiecesCount {
		get {
			return (uint)((PiecesEnd - PiecesStart) >> 3);
		}
	}
	
	public FurnitureModelPiece*[] Pieces() {
		var l = new FurnitureModelPiece*[PiecesCount];
		for(var i = 0; i < PiecesCount; i++) {
			var ptr = (FurnitureModelIdk*)Marshal.ReadIntPtr(PiecesStart + i * 8);
			l[i] = ptr->Piece;
		}
		
		return l;
	}
	
	public FurnitureModelPiece*[] AllPieces() {
		var l = new List<nint>();
		void AddPiece(FurnitureModelPiece* piece) {
			l.Add((nint)piece);
			
			if(piece->Type == 6)
				foreach(var p in piece->AsItem->Pieces())
					AddPiece(p);
		}
		
		foreach(var piece in Pieces())
			AddPiece(piece);
		
		var l2 = new FurnitureModelPiece*[l.Count];
		for(var i = 0; i < l.Count; i++)
			l2[i] = (FurnitureModelPiece*)l[i];
		
		return l2;
	}
}

public enum HaloColor: byte {
	None = 0,
	Red = 1,
	Green = 2,
	Blue = 3,
	Yellow = 4,
	Hover = 4,
	Orange = 5,
	Selected = 5,
	Purple = 6,
	Invalid = 6,
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FurnitureItemExtras {
	[FieldOffset(0x28)] public StdVector<nint> Children;
	[FieldOffset(0xc0)] public HaloColor HaloColor;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FurnitureItemChild {
	[FieldOffset(0x10)] public FurnitureItem* Item;
	// [FieldOffset(0x20)] public Transform Offset;
	[FieldOffset(0x20)] public Vector3 Position;
	[FieldOffset(0x30)] public Quaternion Rotation;
	[FieldOffset(0x40)] public Vector3 Scale;
}

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
	
	/// <summary>Lumina.Data.Parsing.Layer.LayerEntryType</summary>
	[FieldOffset(0x19)] public byte Type;
	[FieldOffset(0x20)] public uint Index;
	// [FieldOffset(0x23)] public byte Index;
	[FieldOffset(0x28)] public byte ArrayIndex; // same order as array and 0 indexed
	[FieldOffset(0x30)] public FurnitureModelSegment* Segment;
	
	/// <summary>Only valid for Type 6</summary>
	[FieldOffset(0x88)] public FurnitureItem* AsItem;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FurnitureModelSegment {
	[FieldOffset(0x18)] public FurnitureModelSegment* LinkedRoot; // idk if root, but i assume so since its always the same address
	[FieldOffset(0x20)] public FurnitureModelSegment* LinkedPrev;
	[FieldOffset(0x28)] public FurnitureModelSegment* LinkedNext;
	
	// [FieldOffset(0x50)] public Transform Transform;
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
	[FieldOffset(0x038)] public FurnitureItem* PlaceItem;
	[FieldOffset(0x088)] public bool PreviewMode;
	[FieldOffset(0x090)] public LayoutManagerSub Sub;
	// seems to be true when noclipped with props
	// [FieldOffset(0x180)] public bool HousingMode;
	public bool HousingMode => Mode != LayoutMode.None;
	[FieldOffset(0x181)] public bool GridSnap;
	[FieldOffset(0x182)] public bool Counter;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct LayoutManagerSub {
	[FieldOffset(0x00)] public LayoutManager* Manager;
	[FieldOffset(0x08)] public Gizmo* Gizmo;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct Gizmo {
	// [FieldOffset(0x050)] public Transform Transorm;
	[FieldOffset(0x50)] public Vector3 Position;
	[FieldOffset(0x60)] public Quaternion Rotation;
	[FieldOffset(0x70)] public Vector3 Scale;
	[FieldOffset(0x260)] public Color Color;
}

[StructLayout(LayoutKind.Explicit, Size = 0x10)]
public unsafe struct Color {
	[FieldOffset(0x0)] public float R;
	[FieldOffset(0x4)] public float G;
	[FieldOffset(0x8)] public float B;
	[FieldOffset(0xC)] public float A;
}

// TODO: convert all the pos, rot, scale to a transform
[StructLayout(LayoutKind.Explicit, Size = 0x30)]
public unsafe struct Transform {
	[FieldOffset(0x00)] public Vector3 Position;
	[FieldOffset(0x10)] public Quaternion Rotation;
	[FieldOffset(0x20)] public Vector3 Scale;
}
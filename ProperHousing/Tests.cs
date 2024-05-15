// using System;
// using System.Runtime.InteropServices;
// using Dalamud.Logging;
// 
// namespace ProperHousing;
// 
// public partial class ProperHousing {
// 	private unsafe void TestCollides() {
// 		var zone = housing->CurrentZone();
// 		if(zone == null)
// 			return;
// 		
// 		for(int i = 0; i < 400; i++) {
// 			var obj = zone->Furniture(i);
// 			if(obj == null) {continue;}
// 			
// 			var objmesh = GetMesh(obj);
// 			if(objmesh == null) {continue;}
// 			
// 			// 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 50 48 8B E9 48 8B 49 ??
// 			var mdloffsetidk = ((IntPtr)obj->Item + 0x80);
// 			var count = (Marshal.PtrToStructure<ulong>(mdloffsetidk + 0x18) - Marshal.PtrToStructure<ulong>(mdloffsetidk + 0x10)) >> 3;
// 			var idk = Marshal.ReadIntPtr(Marshal.ReadIntPtr(Marshal.ReadIntPtr(mdloffsetidk + 0x10) + 0) + 0x10) + 0x238;
// 			ProperHousing.Logger.Info($"Testing {obj->Name} ({((IntPtr)obj).ToString("X")}) ({count}) ({idk.ToString("X")})");
// 			
// 			// var segs = obj->ModelSegments(objmesh.Count);
// 			var segs = obj->ModelSegments();
// 			foreach(var seg in segs) {
// 				// PluginLog.Log($"- {((IntPtr)seg).ToString("X")}");
// 				var rot = seg->Rotation;
// 				var pos = seg->Position;
// 			}
// 		}
// 	}
// }
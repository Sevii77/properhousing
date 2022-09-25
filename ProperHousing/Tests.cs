using System;
using Dalamud.Logging;

namespace ProperHousing;

public partial class ProperHousing {
	private unsafe void TestCollides() {
		var zone = housing->CurrentZone();
		if(zone == null)
			return;
		
		for(int i = 0; i < 400; i++) {
			var obj = zone->Furniture(i);
			if(obj == null) {continue;}
			
			var objmesh = GetMesh(obj);
			if(objmesh == null) {continue;}
			
			PluginLog.Log($"Testing {obj->Name} ({((IntPtr)obj).ToString("X")})");
			
			var segs = obj->ModelSegments(objmesh.Count);
			foreach(var seg in segs) {
				PluginLog.Log($"{((IntPtr)seg).ToString("X")}");
				var rot = seg->Rotation;
				var pos = seg->Position;
			}
		}
	}
}
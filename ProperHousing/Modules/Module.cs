using System.IO;
using Newtonsoft.Json;

namespace ProperHousing;

public abstract class Module {
	public abstract string Name {get;}
	private string configPath => ProperHousing.Interface.ConfigDirectory.FullName + "/" + Name + ".json";
	
	public abstract bool DoDrawOption {get;}
	public virtual bool DoDrawQuick => false;
	
	public abstract void DrawOption();
	public virtual void DrawDebug() {}
	public virtual void DrawQuick() {}
	public virtual void Tick() {}
	public virtual void Dispose() {}
	
	public void LoadConfig() {
		try {
			JsonConvert.PopulateObject(File.ReadAllText(configPath), this);
		} catch {
			// legacy supports
			if(Name == "AccurateSelection" || Name == "GenericKeybinds") {
				JsonConvert.PopulateObject(File.ReadAllText(Path.Join(ProperHousing.Interface.ConfigFile.DirectoryName!, "ProperHousing.json")), this);
			}
		}
	}
	
	public void SaveConfig() {
		File.WriteAllText(configPath, JsonConvert.SerializeObject(this));
	}
}
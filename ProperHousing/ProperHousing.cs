using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImGuiNET;
using Dalamud.IoC;
using Dalamud.Game;
using Dalamud.Plugin;
using Dalamud.Hooking;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace ProperHousing;

public class ProperHousing: IDalamudPlugin {
	[PluginService] public static IDalamudPluginInterface Interface   {get; private set;} = null!;
	[PluginService] public static ICommandManager         Commands    {get; private set;} = null!;
	[PluginService] public static ISigScanner             SigScanner  {get; private set;} = null!;
	[PluginService] public static IDataManager            DataManager {get; private set;} = null!;
	[PluginService] public static IGameGui                GameGui     {get; private set;} = null!;
	[PluginService] public static IGameInteropProvider    HookProv    {get; private set;} = null!;
	[PluginService] public static IPluginLog              Logger      {get; private set;} = null!;
	
	public string Name => "Better Housing";
	private readonly string command = "/betterhousing";
	private readonly string commandalt = "/bh";
	
	private bool debugDraw = false;
	private bool confDraw = false;
	private bool quickDraw = true;
	private bool preventzoom = false;
	
	// public static Config config = null!;
	private Gui gui;
	private Module[] modules;
	
	public static CollisionScene collisionScene = null!;
	public static unsafe Camera* camera;
	public static unsafe Housing* housing;
	public static unsafe Layout* layout;
	
	public class Bind {
		public static HashSet<Bind> RegisteredBinds = [];
		
		public bool Shift;
		public bool Ctrl;
		public bool Alt;
		public Key Key;
		
		public Bind() {
			Shift = false;
			Ctrl = false;
			Alt = false;
			Key = Key.None;
		}
		
		public Bind(bool shift, bool ctrl, bool alt, Key key) {
			Shift = shift;
			Ctrl = ctrl;
			Alt = alt;
			Key = key;
			
			RegisteredBinds.Add(this);
		}
		
		~Bind() {
			RegisteredBinds.Remove(this);
		}
		
		public bool Pressed() {
			return Key != Key.None && ModsPressed() && InputHandler.KeyPressed(Key);
		}
		
		public bool ModsPressed() {
			return (!Shift || ImGui.IsKeyDown(ImGuiKey.ModShift)) && (!Ctrl || ImGui.IsKeyDown(ImGuiKey.ModCtrl)) && (!Alt || ImGui.IsKeyDown(ImGuiKey.ModAlt));
		}
	}
	
	public unsafe ProperHousing() {
		collisionScene = new();
		camera = (Camera*)FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->Camera;
		housing = (Housing*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig(Sigs.HousingStruct));
		layout = (Layout*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig(Sigs.LayoutStruct));
		
		Logger.Debug($"Camera  {((IntPtr)camera).ToString("X")}");
		Logger.Debug($"Housing {((IntPtr)housing).ToString("X")}");
		Logger.Debug($"Layout  {((IntPtr)layout).ToString("X")}");
		
		// FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->Camera->CameraBase.SceneCamera.Object.Position
		// TODO: find alternative. this wont work over windows, making it annoying to set scrollwheel keybinds
		CameraZoomHandlerHook = HookProv.HookFromAddress<CameraZoomHandlerDelegate>(SigScanner.ScanText(Sigs.CameraZoom), CameraZoomHandler);
		CameraZoomHandlerHook.Enable();
		
		gui = new();
		modules = [
			new AccurateSelection(),
			new GenericKeybinds(),
			new ImprovedPlacement(),
			// new CameraFollow(),
		];
		
		Interface.UiBuilder.Draw += Draw;
		Interface.UiBuilder.OpenConfigUi += OpenConf;
		
		Commands.AddHandler(command, new CommandInfo(HandleCommand) {
			ShowInHelp = true,
			HelpMessage = ""
		});
		Commands.AddHandler(commandalt, new CommandInfo(HandleCommand) {
			ShowInHelp = true,
			HelpMessage = "Opens the main window (TODO)\n\t\tconfig → Opens the configuration window"
		});
	}
	
	public void Dispose() {
		Interface.UiBuilder.Draw -= Draw;
		Interface.UiBuilder.OpenConfigUi -= OpenConf;
		Commands.RemoveHandler(command);
		Commands.RemoveHandler(commandalt);
		
		CameraZoomHandlerHook.Dispose();
		
		foreach(var module in modules)
			module.Dispose();
	}
	
	public static (Vector3, Vector3) Project2D(Vector2 pos) {
		unsafe {
			var ray = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager.Instance()->CurrentCamera->ScreenPointToRay(pos);
			return (ray.Origin, ray.Direction);
		}
	}
	
	private void HandleCommand(string cmd, string args) {
		if(cmd != command && cmd != commandalt)
			return;
		
		if(args == "debug")
			debugDraw = !debugDraw;
		// else if(args == "test collides")
		// 	TestCollides();
		else if(args == "config")
			confDraw = !confDraw;
	}
	
	private void OpenConf() {confDraw = true;}
	
	private unsafe void Draw() {
		var inHousing = layout->Manager != null && layout->Manager->HousingMode;
		
		InputHandler.Update();
		
		if(confDraw)
			gui.DrawConf(ref confDraw, modules);
		
		if(quickDraw && inHousing)
			gui.DrawQuick(ref quickDraw, modules);
		
		if(debugDraw) {
			for(var i = 0; i < modules.Length; i++)
				modules[i].DrawDebug();
			
			// gui.DrawDebug();
			gui.DrawDebugOverlay();
		}
		
		if(inHousing)
			Tick();
	}
	
	private void Tick() {
		foreach(var bind in Bind.RegisteredBinds)
			if((bind.Key == Key.WheelUp || bind.Key == Key.WheelDown) && bind.ModsPressed()) {
				preventzoom = true;
				break;
			}
		
		foreach(var module in modules)
			module.Tick();
	}
	
	private unsafe delegate void CameraZoomHandlerDelegate(Camera* camera, float a, long b, long c, int d);
	private Hook<CameraZoomHandlerDelegate> CameraZoomHandlerHook;
	private unsafe void CameraZoomHandler(Camera* camera, float a, long b, long c, int d) {
		if(preventzoom) {
			preventzoom = false;
			return;
		}
		
		CameraZoomHandlerHook.Original(camera, a, b, c, d);
	}
}
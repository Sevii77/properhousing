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

namespace ProperHousing;

public partial class ProperHousing: IDalamudPlugin {
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
	
	public static bool DebugDraw {get; private set;} = false;
	private bool confDraw = false;
	private bool preventzoom = false;
	
	// public static Config config = null!;
	private Gui gui;
	private Module[] modules;
	
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
		
		public Bind(bool s, bool c, bool a, Key k) {
			Shift = s;
			Ctrl = c;
			Alt = a;
			Key = k;
			
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
		camera = (Camera*)FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->Camera;
		housing = (Housing*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig(Sigs.HousingStruct));
		layout = (Layout*)Marshal.ReadIntPtr(SigScanner.GetStaticAddressFromSig(Sigs.LayoutStruct));
		
		Logger.Debug($"Camera  {((IntPtr)camera).ToString("X")}");
		Logger.Debug($"Housing {((IntPtr)housing).ToString("X")}");
		Logger.Debug($"Layout  {((IntPtr)layout).ToString("X")}");
		
		// TODO: find alternative. this wont work over windows, making it annoying to set scrollwheel keybinds
		CameraZoomHandlerHook = HookProv.HookFromAddress<CameraZoomHandlerDelegate>(SigScanner.ScanText(Sigs.CameraZoom), CameraZoomHandler);
		CameraZoomHandlerHook.Enable();
		
		gui = new();
		modules = [
			new AccurateSelection(),
			new GenericKeybinds(),
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
	
	private void HandleCommand(string cmd, string args) {
		if(cmd != command && cmd != commandalt)
			return;
		
		if(args == "debug")
			DebugDraw = !DebugDraw;
		// else if(args == "test collides")
		// 	TestCollides();
		else if(args == "config")
			confDraw = !confDraw;
	}
	
	private void OpenConf() {confDraw = true;}
	
	private unsafe void Draw() {
		InputHandler.Update();
		
		if(confDraw)
			gui.DrawConf(ref confDraw, modules);
		
		// if(DebugDraw)
		// 	gui.DrawDebug();
		
		if(layout->Manager != null && layout->Manager->HousingMode)
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
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Logging;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;

namespace ProperHousing {
	public enum Key {
		A = 65,
		Accept = 30,
		Add = 107,
		Application = 93,
		B = 66,
		Back = 8,
		C = 67,
		Cancel = 3,
		CapitalLock = 20,
		Clear = 12,
		// Control = 17,
		Convert = 28,
		D = 68,
		Decimal = 110,
		Delete = 46,
		Divide = 111,
		Down = 40,
		E = 69,
		End = 35,
		Enter = 13,
		Escape = 27,
		Execute = 43,
		F = 70,
		F1 = 112,
		F10 = 121,
		F11 = 122,
		F12 = 123,
		F13 = 124,
		F14 = 125,
		F15 = 126,
		F16 = 127,
		F17 = 128,
		F18 = 129,
		F19 = 130,
		F2 = 113,
		F20 = 131,
		F21 = 132,
		F22 = 133,
		F23 = 134,
		F24 = 135,
		F3 = 114,
		F4 = 115,
		F5 = 116,
		F6 = 117,
		F7 = 118,
		F8 = 119,
		F9 = 120,
		Favorites = 171,
		Final = 24,
		G = 71,
		GamepadA = 195,
		GamepadB = 196,
		GamepadDPadDown = 204,
		GamepadDPadLeft = 205,
		GamepadDPadRight = 206,
		GamepadDPadUp = 203,
		GamepadLeftShoulder = 200,
		GamepadLeftThumbstickButton = 209,
		GamepadLeftThumbstickDown = 212,
		GamepadLeftThumbstickLeft = 214,
		GamepadLeftThumbstickRight = 213,
		GamepadLeftThumbstickUp = 211,
		GamepadLeftTrigger = 201,
		GamepadMenu = 207,
		GamepadRightShoulder = 199,
		GamepadRightThumbstickButton = 210,
		GamepadRightThumbstickDown = 216,
		GamepadRightThumbstickLeft = 218,
		GamepadRightThumbstickRight = 217,
		GamepadRightThumbstickUp = 215,
		GamepadRightTrigger = 202,
		GamepadView = 208,
		GamepadX = 197,
		GamepadY = 198,
		GoBack = 166,
		GoForward = 167,
		GoHome = 172,
		H = 72,
		Help = 47,
		Home = 36,
		I = 73,
		ImeOff = 26,
		ImeOn = 22,
		Insert = 45,
		J = 74,
		K = 75,
		L = 76,
		Left = 37,
		LeftButton = 1,
		// LeftControl = 162,
		// LeftMenu = 164,
		// LeftShift = 160,
		// LeftWindows = 91,
		M = 77,
		// Menu = 18,
		MiddleButton = 4,
		ModeChange = 31,
		Multiply = 106,
		N = 78,
		NavigationAccept = 142,
		NavigationCancel = 143,
		NavigationDown = 139,
		NavigationLeft = 140,
		NavigationMenu = 137,
		NavigationRight = 141,
		NavigationUp = 138,
		NavigationView = 136,
		NonConvert = 29,
		None = 0,
		Number0 = 48,
		Number1 = 49,
		Number2 = 50,
		Number3 = 51,
		Number4 = 52,
		Number5 = 53,
		Number6 = 54,
		Number7 = 55,
		Number8 = 56,
		Number9 = 57,
		NumberKeyLock = 144,
		NumberPad0 = 96,
		NumberPad1 = 97,
		NumberPad2 = 98,
		NumberPad3 = 99,
		NumberPad4 = 100,
		NumberPad5 = 101,
		NumberPad6 = 102,
		NumberPad7 = 103,
		NumberPad8 = 104,
		NumberPad9 = 105,
		O = 79,
		P = 80,
		PageDown = 34,
		PageUp = 33,
		Pause = 19,
		Print = 42,
		Q = 81,
		R = 82,
		Refresh = 168,
		Right = 39,
		RightButton = 2,
		// RightControl = 163,
		// RightMenu = 165,
		// RightShift = 161,
		// RightWindows = 92,
		S = 83,
		Scroll = 145,
		Search = 170,
		Select = 41,
		Separator = 108,
		// Shift = 16,
		Sleep = 95,
		Snapshot = 44,
		Space = 32,
		Stop = 169,
		Subtract = 109,
		T = 84,
		Tab = 9,
		U = 85,
		Up = 38,
		V = 86,
		W = 87,
		X = 88,
		XButton1 = 5,
		XButton2 = 6,
		Y = 89,
		Z = 90
	}
	
	public partial class ProperHousing {
		private Bind? selectBind;
		private byte[] keyStates;
		private byte[] keyStatesLast = null!;
		
		public class Bind {
			public bool Shift;
			public bool Ctrl;
			public bool Alt;
			public Key Key;
		}
		
		private bool KeyPressed(Key key) {
			return keyStates[(int)key] > 1 && keyStates[(int)key] != keyStatesLast[(int)key];
		}
		
		private bool BindPressed(Bind bind) {
			return bind.Key != Key.None && (!bind.Shift || ImGui.IsKeyDown(ImGuiKey.ModShift)) && (!bind.Ctrl || ImGui.IsKeyDown(ImGuiKey.ModCtrl)) && (!bind.Alt || ImGui.IsKeyDown(ImGuiKey.ModAlt)) && KeyPressed(bind.Key);
		}
		
		private unsafe void DrawConf() {
			keyStatesLast = (byte[])keyStates.Clone();
			GetKeyboardState(keyStates);
			
			void DrawKeybind(string label, Bind bind) {
				var shift = bind.Shift;
				var ctrl = bind.Ctrl;
				var alt = bind.Alt;
				var key = bind.Key.ToString();
				
				if (selectBind == bind) {
					shift = ImGui.IsKeyDown(ImGuiKey.ModShift);
					ctrl = ImGui.IsKeyDown(ImGuiKey.ModCtrl);
					alt = ImGui.IsKeyDown(ImGuiKey.ModAlt);
					key = "";
					
					if(ImGui.IsKeyDown(ImGuiKey.Escape))
						selectBind = null;
					else
						foreach(var k in Enum.GetValues(typeof(Key)).Cast<Key>())
							if(KeyPressed(k)) {
								bind.Shift = shift;
								bind.Ctrl = ctrl;
								bind.Alt = alt;
								bind.Key = k;
								selectBind = null;
								config.Save();
								break;
							}
				}
				
				var s = $"{(shift ? "Shift+" : "")}{(ctrl ? "Ctrl+" : "")}{(alt ? "Alt+" : "")}{key}";
				ImGui.SetNextItemWidth(175);
				ImGui.InputText(label, ref s, 64, ImGuiInputTextFlags.ReadOnly);
				if(ImGui.IsItemClicked(ImGuiMouseButton.Left))
					selectBind = bind;
				else if(ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
					bind.Shift = false;
					bind.Ctrl = false;
					bind.Alt = false;
					bind.Key = Key.None;
				}
			}
			
			if(layout->Manager->HousingMode) {
				var atk = AtkStage.GetSingleton()->RaptureAtkUnitManager->GetAddonByName("HousingLayout");
				void setImg(uint id, bool state) {
					var n = atk->GetNodeById(id)->GetComponent()->UldManager;
					n.SearchNodeById(3)->ToggleVisibility(!state);
					n.SearchNodeById(2)->ToggleVisibility(state);
				}
				
				void SetMode(LayoutMode mode) {
					layout->Manager->Mode = mode;
					
					setImg(9,  mode == LayoutMode.Move);
					setImg(10, mode == LayoutMode.Rotate);
					setImg(11, mode == LayoutMode.Remove);
					setImg(12, mode == LayoutMode.Store);
					
					atk->GetNodeById(4)->ToggleVisibility(mode == LayoutMode.Move);
					atk->GetNodeById(5)->ToggleVisibility(mode == LayoutMode.Rotate);
					atk->GetNodeById(6)->ToggleVisibility(mode == LayoutMode.Remove);
					atk->GetNodeById(7)->ToggleVisibility(mode == LayoutMode.Store);
				}
				
				if(BindPressed(config.MoveMode)) SetMode(LayoutMode.Move);
				if(BindPressed(config.RotateMode)) SetMode(LayoutMode.Rotate);
				if(BindPressed(config.RemoveMode)) SetMode(LayoutMode.Remove);
				if(BindPressed(config.StoreMode)) SetMode(LayoutMode.Store);
				
				// this doesnt take effect until you (re)select the furniture, TODO: fix that
				if(BindPressed(config.CounterToggle)) {
					var s = &layout->Manager->Counter;
					*s = !*s;
					setImg(13, *s);
				}
				
				if(BindPressed(config.GridToggle)) {
					var s = &layout->Manager->GridSnap;
					*s = !*s;
					setImg(14, *s);
				}
			}
			
			ImGui.Begin("Better Housing", ref confDraw, ImGuiWindowFlags.AlwaysAutoResize);
			
			// TODO: just make this a bind aswell
			ImGui.Text("Rotate objects while moving with shift+scroll");
			
			ImGui.Text($"Keybinds (?)");
			if(ImGui.IsItemHovered())
				ImGui.SetTooltip("Keybinds to switch mode and toggle counter placement and grid snap.\nNOTE: buttons will become invisible if manually clicked,\nthey can still be clicked. This is a known issue");
			
			DrawKeybind("Move Mode", config.MoveMode);
			DrawKeybind("Rotate Mode", config.RotateMode);
			DrawKeybind("Remove Mode", config.RemoveMode);
			DrawKeybind("Store Mode", config.StoreMode);
			DrawKeybind("Toggle Counter Placement", config.CounterToggle);
			DrawKeybind("Toggle Grid Snap", config.GridToggle);
			
			ImGui.End();
		}
		
		[DllImport("user32.dll")]
		private static extern byte GetKeyboardState(byte[] keyStates);
	}
}
using System.Collections.Generic;
using UnityEngine;

class Hotkeys {
    private static Hotkeys m_instance = null;
    public static Hotkeys Instance {
        get {
            if (m_instance == null) {
                m_instance = new Hotkeys();
            }
            return m_instance;
        }
    }

    public enum HotKeyIndex : int
	{
		HOTKEY_MODIFIER = 0,
		HOTKEY_AURA_ON,
		HOTKEY_AURA_TYPE,
		HOTKEY_HEAL,
		HOTKEY_HARPOON,
		HOTKEY_TRANQ,
		HOTKEY_NET,
        HOTKEY_SNIPE,
		HOTKEY_WEAP_UP,
		HOTKEY_WEAP_DOWN,


		HOTKEY_COUNT
	};

    private static Dictionary<int, List<KeyCode>> m_hotkeys = null;

    public static void load() {
        m_hotkeys = new Dictionary<int, List<KeyCode>>();
        set_hotkey( Settings.m_hotkey_modifier.Value, ( int )HotKeyIndex.HOTKEY_MODIFIER );
        set_hotkey(Settings.m_hotkey_toggle_aura_on.Value, ( int )HotKeyIndex.HOTKEY_AURA_ON );
		set_hotkey( Settings.m_hotkey_change_aura_type.Value, ( int )HotKeyIndex.HOTKEY_AURA_TYPE );
		set_hotkey( Settings.m_hotkey_Heal.Value, ( int )HotKeyIndex.HOTKEY_HEAL );
		set_hotkey( Settings.m_hotkey_Harpoon.Value, ( int )HotKeyIndex.HOTKEY_HARPOON );
		set_hotkey( Settings.m_hotkey_Tranq.Value, ( int )HotKeyIndex.HOTKEY_TRANQ );
		set_hotkey( Settings.m_hotkey_Net.Value, ( int )HotKeyIndex.HOTKEY_NET );
		set_hotkey( Settings.m_hotkey_Snipe.Value, ( int )HotKeyIndex.HOTKEY_SNIPE );
		set_hotkey( Settings.m_hotkey_Weapon_Up.Value, ( int )HotKeyIndex.HOTKEY_WEAP_UP );
		set_hotkey( Settings.m_hotkey_Weapon_Down.Value, ( int )HotKeyIndex.HOTKEY_WEAP_DOWN );
		PluginUpdater.Instance.register("keypress", 0f, Updaters.keypress_update);
    }

    private static void set_hotkey(string keys_string, int key_index) {
        m_hotkeys[key_index] = new List<KeyCode>();
        foreach (string key in keys_string.Split(',')) {
            string trimmed_key = key.Trim();
            if (trimmed_key != "") {
                m_hotkeys[key_index].Add((KeyCode) System.Enum.Parse(typeof(KeyCode), trimmed_key));
            }
        }
    }

    private static bool is_modifier_hotkey_down() {
        if (m_hotkeys[ ( int )HotKeyIndex.HOTKEY_MODIFIER ].Count == 0) {
            return true;
        }
        foreach (KeyCode key in m_hotkeys[ ( int )HotKeyIndex.HOTKEY_MODIFIER ] ) {
            if (Input.GetKey(key)) {
                return true;
            }
        }
        return false;
    }

    public static bool is_hotkey_down(int key_index) {
        foreach (KeyCode key in m_hotkeys[key_index]) {
            if (Input.GetKeyDown(key)) {
                return true;
            }
        }
        return false;
    }

    public class Updaters {
        public static void keypress_update() {
            if (!is_modifier_hotkey_down()) {
                return;
            }
            Diving.Updaters.keypress_update();
        }
    }
}

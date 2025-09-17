using DR;
using EvilFactory;
using HarmonyLib;
using Il2CppSystem.Globalization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

class Diving {
    private static Diving m_instance = null;
    public static Diving Instance {
        get {
            if (m_instance == null) {
                m_instance = new Diving();
            }
            return m_instance;
        }
    }
    private static DDPlugin m_plugin = null;
    private bool m_is_initialized = false;
    private bool m_is_data_initialized = false;
    private bool m_is_inventory_initialized = false;
    private bool m_toxic_aura_enabled_by_hotkey = true;
    private bool m_toxic_aura_sleep_by_hotkey = true;
    private const int SLEEP_BUFF_ID = 14080415;
    private const float SLEEP_BUFF_VALUE = 9999999999f;
    private static bool m_did_set_sleep_buff_value = false;
    private static int m_weapon_level = 0;
    private Dictionary<GunItemType, List<GunSpecData>> gun_specs = new Dictionary<GunItemType, List<GunSpecData>>();
    private Dictionary<HarpoonHeadItemType, List<HarpoonHeadSpecData>> harpoon_heads = new Dictionary<HarpoonHeadItemType, List<HarpoonHeadSpecData>>();
    private Dictionary<HarpoonItemType, HarpoonSpecData> harpoon_specs = new Dictionary<HarpoonItemType, HarpoonSpecData>();
    private static List<GameObject> m_next_frame_destroy_objects = new List<GameObject>();
    private static List<IntegratedItemType> m_pickup_item_types = new List<IntegratedItemType>();
    private static List<string> m_all_pickup_item_names = new List<string>();
    private static Dictionary<string, bool> m_enabled_pickup_items = new Dictionary<string, bool>();


    private void initialize() {
        if (this.m_is_initialized) {
            return;
        }

        Init_data();

        this.load_enabled_pickup_items();
        this.m_is_initialized = true;
    }

    private void Init_data()
    {
        if(this.m_is_data_initialized) { 
            return;
        }

        this.m_toxic_aura_enabled_by_hotkey = Settings.m_toxic_aura_enabled.Value;
        this.m_toxic_aura_sleep_by_hotkey = Settings.m_toxic_aura_sleep.Value;

        // Get all gun types
        foreach (GunSpecData spec in Resources.FindObjectsOfTypeAll<GunSpecData>())
        {
            if (!this.gun_specs.ContainsKey(spec.GunType))
            {
                this.gun_specs[spec.GunType] = new List<GunSpecData>();
            }
            this.gun_specs[spec.GunType].Add(spec);
        }

        // Sort the guns
		foreach ( GunItemType type in gun_specs.Keys )
		{
			gun_specs [ type ].Sort( ( x, y ) =>
			{
				int res = x.Damage.CompareTo(y.Damage);
                if(res != 0)
                    return res;

                if(x.buffDatas.Count > 0)
                    res = x.buffDatas[0].Level.CompareTo(y.buffDatas[0].Level);

				if ( res != 0 )
					return res;

				switch ( x.GunRootType )
				{
					case GunRootType.BasicRifle:
						res = x.Name.CompareTo( y.Name );
                        if(res == 0 && x.GunType == GunItemType.GrenadeLauncher)
                            res = x.ExplosionSplashDamage.CompareTo( y.ExplosionSplashDamage );
						break;

					case GunRootType.SleepGun:
						res = x.Name.CompareTo( y.Name );
						break;

					case GunRootType.NetGun:
						res = x.CaptureCount.CompareTo( y.CaptureCount );
						break;

					case GunRootType.StickyBombGun:
						if ( x.GunType == GunItemType.MineBombGun || x.GunType == GunItemType.MineBombGun02 || x.GunType == GunItemType.SleepBombGun )
						{
							res = x.Name.CompareTo( y.Name );
							break;
						}
						res = x.ExplosionSplashDamage.CompareTo( y.ExplosionSplashDamage );
						break;

					case GunRootType.GrenadeLauncher:
						res = x.ExplosionSplashDamage.CompareTo( y.ExplosionSplashDamage );
						break;

					case GunRootType.IceGun:
						res = x.buffEffects[ 0 ].Level.CompareTo( y.buffEffects[ 0 ].Level );
						break;

					default:
						break;
				}

				if ( res == 0 )
				{
					DDPlugin._warn_log( $"FIX GUN COMPARE FOR {x.GunType}" );
					DDPlugin._debug_log( $"X:{x.GunType}:{x.Name} - Damage:{x.Damage} Burst:{x.BurstCount} Splash:{x.ExplosionSplashDamage} Power:{x.Power} UIDam:{x.UIDisplayDamage}" + ( x.buffDatas.Count > 0 ? $" BufD.Level:{x.buffDatas.Count,-5}" : "" ) );
					DDPlugin._debug_log( $"Y:{y.GunType}:{y.Name} - Damage:{y.Damage} Burst:{y.BurstCount} Splash:{y.ExplosionSplashDamage} Power:{y.Power} UIDam:{x.UIDisplayDamage}" + ( y.buffDatas.Count > 0 ? $" BufD.Level:{y.buffDatas.Count,-5}\n" : "\n" ) );
				}

				return res;
			} );
		}

        // Get all harpoons
		foreach (HarpoonSpecData spec in Resources.FindObjectsOfTypeAll<HarpoonSpecData>())
        {
            this.harpoon_specs[spec.HarpoonType] = spec;
        }

        // Get all Harpoon Heads
        foreach (HarpoonHeadSpecData spec in Resources.FindObjectsOfTypeAll<HarpoonHeadSpecData>())
        {
            if (!this.harpoon_heads.ContainsKey( spec.HarpoonHeadType) )
            {
                this.harpoon_heads[ spec.HarpoonHeadType ] = new List<HarpoonHeadSpecData>();
            }
            this.harpoon_heads[ spec.HarpoonHeadType ].Add(spec);
        }
        // Sort harpoon heads
        foreach (List<HarpoonHeadSpecData> specs in this.harpoon_heads.Values)
        {
            specs.OrderBy(e => e.Damage).ThenBy(e => e.buffDatas[0].Level);
        }


#if DEBUG
		// Print all the guns
		foreach ( GunItemType type in gun_specs.Keys )
		{
			foreach ( GunSpecData spec in gun_specs[ type ] )
			{
				DDPlugin._info_log( $"GunRootType:{spec.GunRootType,-20} GunType:{spec.GunType,-30} Name:{spec.Name,-30} Damage:{spec.Damage.ToString( "D3" ),-5} Splash:{spec.ExplosionSplashDamage.ToString( "D3" ),-5}" + ( spec.buffDatas.Count > 0 ? $"Level:{spec.buffDatas[ 0 ].Level.ToString("D3"),-5}" : "" ) );
			}
		}

		// Print all the Harpoons
		foreach ( HarpoonHeadItemType type in harpoon_heads.Keys )
		{
			foreach ( HarpoonHeadSpecData spec in harpoon_heads[ type ] )
			{
				DDPlugin._info_log( $"Type:{spec.HarpoonHeadType,-20} Damage:{spec.Damage.ToString( "D3" ),-5}" + ( spec.buffDatas.Count > 0 ? $"Level:{spec.buffDatas[ 0 ].Level.ToString( "D3" ),-5}" : "" ) );
			}
		}
#endif


		this.m_is_data_initialized = true;
    }

    public static void load(DDPlugin plugin) {
        m_plugin = plugin;
        PluginUpdater.Instance.register("Diving.auto_pickup_update", Settings.m_auto_pickup_frequency.Value, Updaters.auto_pickup_update);
        PluginUpdater.Instance.register("Diving.general_update", 1f, Updaters.general_update);
        PluginUpdater.Instance.register("Diving.toxic_aura_update", Settings.m_aura_update_frequency.Value, Updaters.toxic_aura_update);
    }

    public void load_enabled_pickup_items() {
        int counter = 1;
        bool did_error = false;
        string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "config", "auto-pickup-items.txt"));
        try {
            m_all_pickup_item_names.Sort();
            foreach (string key in m_all_pickup_item_names) {
                m_enabled_pickup_items[key] = DefaultAutoPickupItems.enabled_items.Contains(key);
            }
            DDPlugin._info_log($"Loading enabled auto-pickup item info from '{path}'.");
            if (File.Exists(path)) {
                foreach (string _line in File.ReadAllText(path).Split('\n')) {
                    counter++;
                    string line = _line.Trim();
                    if (string.IsNullOrEmpty(line) || line[0] == '#' || !m_all_pickup_item_names.Contains(line)) {
                        continue;
                    }
                    m_enabled_pickup_items[line] = true;
                }
            } else {
                DDPlugin._info_log("File does not exist.");
            }
        } catch (Exception e) {
            did_error = true;
            DDPlugin._warn_log($"* load_enabled_pickup_items WARNING - error parsing auto-pickup-items.txt file (line: {counter}); auto-pickup of items will be disabled until this issue is resolved.  Error = " + e);
            foreach (string key in m_all_pickup_item_names) {
                m_enabled_pickup_items[key] = false;
            }
        }
        try {
            if (did_error && File.Exists(path)) {
                return;
            }
            string output = @"

# Notes on this file:
#
# - Each line in this file represents an item name key in Dave the Diver.
# - This is a list of *EVERY* item in the game, and not all
#   items will be something that can be picked up.
# - Fish are picked up using separate logic, so fish names can be ignored.
# - Set 'Auto-pickup: Debug Mode' to true to have the mod print debug statements to
#   LogOutput.log (and the console) when the mod does not pick up an item.  Use the
#   'name_key' info in those log statements to find the item to enable in this file.
# - Un-comment the line (remove the #) to enable pickup of the item.
# - Empty lines or lines beginning with # will be ignored.
# - Be sure to set 'Auto-pickup: Debug Mode' to false when not debugging, as it can
#   clutter up the log file.

";
            foreach (string key in m_all_pickup_item_names) {
                output += $"{(m_enabled_pickup_items[key] ? "" : "# ")}{key}\n";
            }
            File.WriteAllText(path, output);
        } catch (Exception e) {
            DDPlugin._warn_log($"* load_enabled_pickup_items WARNING - unable to write item info to file.  Error = " + e);
        }
    }

    /*
    public void set_harpoon_item(InstanceItemInventory inventory) {
        HarpoonItemType type;
        if (string.IsNullOrEmpty(m_harpoon_type.Value)) {
            return;
        }
        if (!Enum.TryParse<HarpoonItemType>(m_harpoon_type.Value + "Harpoon", out type)) {
            DDPlugin._error_log($"* set_harpoon_item WARNING - '{m_harpoon_type.Value}' is not a recognized type (see help info in config file).");
            return;
        }
        inventory.currentEquipInInventory[EquipmentType.Harpoon] = this.harpoon_specs[type];
        _debug_log($"{inventory.GetEquipedItem(EquipmentType.Harpoon).Name}");
    }
    */

    public void set_harpoon_head() {
        if (this.m_is_inventory_initialized)
        {
            return;
        }

		PlayerCharacter player;
		if ( !Settings.m_enabled.Value || ( player = Singletons.Player ) == null || !player.isActiveAndEnabled )
		{
			return;
		}

		HarpoonHeadItemType type;
        DDPlugin._info_log("Reading Harpoon Head type");
        if (string.IsNullOrEmpty(Settings.m_harpoon_head_type.Value)) {
            DDPlugin._error_log("Harpoon Head setting is empty");
            return;
        }
        if (!Enum.TryParse<HarpoonHeadItemType>(Settings.m_harpoon_head_type.Value + "Head", out type)) {
            DDPlugin._error_log($"* set_harpoon_head WARNING - '{Settings.m_harpoon_head_type.Value}' is not a recognized type (see help info in config file).");
            return;
        }

        DDPlugin._info_log($"Attempting to set Harpoon Head type {type}");
        HarpoonHeadSpecData target_head = harpoon_heads[type][m_weapon_level];

		player.CurrentInstanceItemInventory.harpoonHandler.EquipItem( target_head, true );

		m_is_inventory_initialized = true;
    }

    public void HealPlayer ()
	{
		DDPlugin._info_log( "HealPlayer" );
		PlayerCharacter player;
		if ( !Settings.m_enabled.Value || ( player = Singletons.Player ) == null || !player.isActiveAndEnabled )
		{
			return;
		}

		player.BreathHandler.HealHP( player.BreathHandler.MaxHP - player.BreathHandler.HP );
	}

	public void IncreasePlayerWeapon ()
	{
		DDPlugin._info_log( "IncreasePlayerWeapon" );
		PlayerCharacter player;
		if ( !Settings.m_enabled.Value || ( player = Singletons.Player ) == null || !player.isActiveAndEnabled )
		{
			return;
		}

        // Levels cap at 5
		if ( 4 > m_weapon_level )
            ++m_weapon_level;

		HarpoonHeadItemType targetHarpoonType = player.CurrentInstanceItemInventory.harpoonHandler.m_HeadSpec.HarpoonHeadType;
		HarpoonHeadSpecData newHarpSpec = harpoon_heads[targetHarpoonType][m_weapon_level];
        player.CurrentInstanceItemInventory.harpoonHandler.EquipItem( newHarpSpec, true );

		GunItemType targetGunType = player.CurrentInstanceItemInventory.gunHandler.m_GunSpec.GunType;
		GunSpecData newSpec = gun_specs[targetGunType][m_weapon_level];
		player.CurrentInstanceItemInventory.gunHandler.EquipItem( newSpec, true );
	}

	public void DecreasePlayerWeapon ()
	{
		DDPlugin._info_log( "IncreasePlayerWeapon" );
		PlayerCharacter player;
		if ( !Settings.m_enabled.Value || ( player = Singletons.Player ) == null || !player.isActiveAndEnabled )
		{
			return;
		}

		if ( m_weapon_level > 0)
			--m_weapon_level;

		HarpoonHeadItemType targetHarpoonType = player.CurrentInstanceItemInventory.harpoonHandler.m_HeadSpec.HarpoonHeadType;
		HarpoonHeadSpecData newHarpSpec = harpoon_heads[targetHarpoonType][m_weapon_level];
		player.CurrentInstanceItemInventory.harpoonHandler.EquipItem( newHarpSpec, true );

		GunItemType targetGunType = player.CurrentInstanceItemInventory.gunHandler.m_GunSpec.GunType;
		GunSpecData newSpec = gun_specs[targetGunType][m_weapon_level];
		player.CurrentInstanceItemInventory.gunHandler.EquipItem( newSpec, true );
	}

	public void GivePlayerHarpoon()
	{
		DDPlugin._info_log( "GivePlayerTranq" );
		PlayerCharacter player;
		if ( !Settings.m_enabled.Value || ( player = Singletons.Player ) == null || !player.isActiveAndEnabled )
		{
			return;
		}

		HarpoonHeadItemType type;
		DDPlugin._info_log( "Reading Harpoon Head type" );
		if ( string.IsNullOrEmpty( Settings.m_harpoon_head_type.Value ) )
		{
			DDPlugin._error_log( "Harpoon Head setting is empty" );
			return;
		}
		if ( !Enum.TryParse<HarpoonHeadItemType>( Settings.m_harpoon_head_type.Value + "Head", out type ) )
		{
			DDPlugin._error_log( $"* set_harpoon_head WARNING - '{Settings.m_harpoon_head_type.Value}' is not a recognized type (see help info in config file)." );
			return;
		}
		HarpoonHeadSpecData target_head = harpoon_heads[type][m_weapon_level];

		player.CurrentInstanceItemInventory.harpoonHandler.EquipItem( target_head, true );
	}

	public void GivePlayerTranq ()
	{
		DDPlugin._info_log( "GivePlayerTranq" );
		PlayerCharacter player;
		if ( !Settings.m_enabled.Value || ( player = Singletons.Player ) == null || !player.isActiveAndEnabled )
		{
			return;
		}

		GunSpecData targetSpec = gun_specs[GunItemType.SleepGun02][m_weapon_level];

		player?.CurrentInstanceItemInventory?.gunHandler.EquipItem( targetSpec, true );
	}

	public void GivePlayerNet ()
	{
		DDPlugin._info_log( "GivePlayerNet" );
		PlayerCharacter player;
		if ( !Settings.m_enabled.Value || ( player = Singletons.Player ) == null || !player.isActiveAndEnabled )
		{
			return;
		}

		GunSpecData targetSpec = gun_specs[GunItemType.L_NetGun][m_weapon_level];

		player?.CurrentInstanceItemInventory?.gunHandler.EquipItem( targetSpec, true );
	}

	public void GivePlayerSnipe ()
	{
		DDPlugin._info_log( "GivePlayerSnipe" );
		PlayerCharacter player;
		if ( !Settings.m_enabled.Value || ( player = Singletons.Player ) == null || !player.isActiveAndEnabled )
		{
			return;
		}

		GunSpecData targetSpec = gun_specs[GunItemType.Sleep_SniperGun02][m_weapon_level];

		player?.CurrentInstanceItemInventory?.gunHandler.EquipItem( targetSpec, true );
	}

	class Patches {
        [HarmonyPatch(typeof(BuffHandler), "Start")]
        class HarmonyPatch_BuffHandler_Start {
            private static void Postfix(BuffHandler __instance) {
                try {
                    if (Settings.m_enabled.Value && Settings.m_speed_boost.Value > 0 && __instance.gameObject.name == "DaveCharacter") {
                        __instance.GetBuffComponents.AddMoveSpeedParam(1234567, Settings.m_speed_boost.Value);
                    }
                } catch (Exception e) {
                    DDPlugin._error_log("** HarmonyPatch_BuffHandler_Start.Postfix ERROR - " + e);
                }
            }
        }

        [HarmonyPatch(typeof(GetInfoPanelUI), "WaitOnPopup")]
        class HarmonyPatch_GetInfoPanelUI_WaitOnPopup {
            private static void Postfix(GetInfoPanelUI __instance, GetInfoPanelUI.GetItemInfo info) {
                if (Settings.m_enabled.Value && Settings.m_disable_item_info_popup.Value) {
                    __instance.gameObject.SetActive(false);
                }
            }
        }

        [HarmonyPatch(typeof(IntegratedItem), "BuildItem")]
        class HarmonyPatch_IntegratedItem_BuildItem {
            private static bool Prefix(Items itemBase) {
                try {
                    if (Settings.m_enabled.Value && Settings.m_weightless_items.Value) {
                        ReflectionUtils.get_property(itemBase, "ItemWeight").SetValue(itemBase, 0);
                    }
                    if (!string.IsNullOrEmpty(itemBase.ItemTextID) && !m_all_pickup_item_names.Contains(itemBase.ItemTextID)) {
                        m_all_pickup_item_names.Add(itemBase.ItemTextID);
                    }
                    return true;
                } catch (Exception e) {
                    DDPlugin._error_log("** HarmonyPatch_IntegratedItem_BuildItem.Prefix ERROR - " + e);
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerBreathHandler), "Update")]
        class HarmonyPatch_PlayerBreathHandler_Update {
            private static bool Prefix(PlayerBreathHandler __instance) {
                if (Settings.m_enabled.Value && Settings.m_infinite_oxygen.Value) {
                    __instance.SetBreathInvincible(true);
                }
                if (Settings.m_enabled.Value && Settings.m_invincible.Value) {
                    __instance.SetDamageInvicible(true);
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerCharacter), "IsCrabTrapAvailable", MethodType.Getter)]
        class HarmonyPatch_PlayerCharacter_IsCrabTrapAvailable {

            private static void Postfix(PlayerCharacter __instance, ref bool __result) {
                try {
                    if (Settings.m_enabled.Value && Settings.m_infinite_crab_traps.Value) {
                        __result = true;
                    }
                } catch (Exception e) {
                    DDPlugin._error_log("** HarmonyPatch_PlayerCharacter_IsCrabTrapAvailable.Prefix ERROR - " + e);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerCharacter), "SetHPDamage")]
        class HarmonyPatch_PlayerCharacter_SetHPDamage {
            private static bool Prefix() {
                try {
                    return !(Settings.m_enabled.Value && Settings.m_invincible.Value);
                } catch (Exception e) {
                    DDPlugin._error_log("** HarmonyPatch_PlayerCharacter_SetHPDamage.Prefix ERROR - " + e);
                }
                return true;
            }
        }
    }


    public static string[] BannedFromPickup = { "ShellFish004", "Chest_O2" };

    public class Updaters {
        private static void auto_pickup<T>(bool enabled, PlayerCharacter player, Vector3 player_pos, Func<T, bool> callback) where T : MonoBehaviour {
            if (!enabled) {
                return;
            }
            foreach (T item in Resources.FindObjectsOfTypeAll<T>()) {
                if (item.transform.position == Vector3.zero || Vector3.Distance(player_pos, item.transform.position) > Settings.m_auto_pickup_radius.Value || m_next_frame_destroy_objects.Contains(item.gameObject)) {
                    continue;
                }
                if (item is FishInteractionBody fish) {
                    if (fish.CheckAvailableInteraction(player) && fish.InteractionType == FishInteractionBody.FishInteractionType.Pickup && (callback == null || callback(item))) {
                        fish.SuccessInteract(player);
                        m_next_frame_destroy_objects.Add(fish.gameObject);
                    }
                } else if (item is PickupInstanceItem pickup && (callback == null || callback(item))) {
                    try {
                        if (pickup.CheckAvailableInteraction(player)) {
                            pickup.SuccessInteract(player);
                            m_next_frame_destroy_objects.Add(pickup.gameObject);
                        }
                    } catch { }
                } else if (item is InstanceItemChest chest && (callback == null || callback(item))) {
                    DDPlugin._info_log($"^^ Auto-Pickup Item DEBUG - name_key: {chest.name}");
                    if (!BannedFromPickup.Any(chest.name.Contains))
                    {
                        chest.SuccessInteract(player);
                        m_next_frame_destroy_objects.Add(chest.gameObject);
                    }
                } else if (item is BreakableLootObject breakable && (callback == null || callback(item))) {
                    breakable.OnTakeDamage(new AttackData() {
                        damage = 99999,
                        attackType = AttackType.Player_Melee
                    }, new DefenseData() {
                    });
                    if (breakable.IsDead()) {
                        m_next_frame_destroy_objects.Add(breakable.gameObject);
                    }
                } else if (item is CrabTrapZone crab_trap_zone && !item.transform.Find("CrabTrap(Clone)") && player.AvailableCrabTrapCount > 0 && (callback == null || callback(item))) {
                    if (crab_trap_zone.CheckAvailableInteraction(player)) {
                        crab_trap_zone.SetUpCrabTrap(9);
                    }
                }
            }
        }

        private static List<int> m_debug_echoed_item_hashes = new List<int>();

        public static bool auto_pickup_callback_PickupInstanceItem(PickupInstanceItem _item) {
            try {
                if (_item == null) {
                    return false;
                }
                string text_id = "";
                int hash = 0;
                try {
                    IntegratedItem item = DataManager.Instance.GetIntegratedItem((_item.usePreset ? _item.presetItemID : _item.GetItemID()));
                    text_id = item.ItemTextID;
                    hash = item.GetHashCode();
                } catch {
                    return false;
                }
                if (!m_enabled_pickup_items[text_id] && Settings.m_auto_pickup_debug_mode.Value && !m_debug_echoed_item_hashes.Contains(hash)) {
                    DDPlugin._debug_log($"^^ Auto-Pickup Item DEBUG - name_key: {text_id}; not enabled for pickup.");
                    if (!m_enabled_pickup_items[text_id]) {
                        m_debug_echoed_item_hashes.Add(hash);
                    }
                }
                return m_enabled_pickup_items[text_id];
            } catch (Exception e) {
                DDPlugin._warn_log($"* auto_pickup_callback_PickupInstanceItem WARNING - {_item.usePreset} {_item.presetItemID} {DataManager.Instance.GetIntegratedItem(_item.GetItemID())}" + e);
            }
            return false;
        }

        public static void auto_pickup_update() {
            try {
                PlayerCharacter player;
                CharacterController2D character;
                if (!Settings.m_enabled.Value || (player = Singletons.Player) == null || !player.isActiveAndEnabled || (character = Singletons.Character) == null || !character.isActiveAndEnabled) {
                    return;
                }
                Diving.Instance.initialize();
                auto_pickup<FishInteractionBody>(Settings.m_auto_pickup_fish.Value, player, character.transform.position, null);
                auto_pickup<PickupInstanceItem>(Settings.m_auto_pickup_items.Value, player, character.transform.position, auto_pickup_callback_PickupInstanceItem);
                auto_pickup<InstanceItemChest>(Settings.m_auto_pickup_items.Value, player, character.transform.position, null);
                //auto_pickup<BreakableLootObject>(Settings.m_auto_pickup_items.Value, player, character.transform.position);
                auto_pickup<CrabTrapZone>(Settings.m_auto_drop_crab_traps.Value, player, character.transform.position, null);
            } catch (Exception e) {
                DDPlugin._error_log("** Diving.auto_pickup_update ERROR - " + e);
            }
        }

        public static void general_update() {
            try {
                PlayerCharacter player;
                if (!Settings.m_enabled.Value || (player = Singletons.Player) == null || !player.isActiveAndEnabled) {
                    return;
                }
                Diving.Instance.initialize();
                if (Settings.m_infinite_drones.Value) {
                    SROptions.Current.RefillDrone();
                }
                try {
                    if (Settings.m_infinite_bullets.Value) {
                        player?.CurrentInstanceItemInventory?.gunHandler.ForceSetBulletCount(999);
                    }
                } catch { }
            } catch (Exception e) {
                DDPlugin._error_log("** Diving.general_update ERROR - " + e);
            }
        }

        public static void keypress_update ()
		{
			DDPlugin._info_log( "keypress_update" );
			if ( !Diving.Instance.m_is_initialized )
            {
                return;
            }

			DDPlugin._info_log( "keypress_update - Checking Keys" );
			if ( Hotkeys.is_hotkey_down( ( int )Hotkeys.HotKeyIndex.HOTKEY_AURA_ON ) )
            {
                Diving.Instance.m_toxic_aura_enabled_by_hotkey = !Diving.Instance.m_toxic_aura_enabled_by_hotkey;
				DDPlugin._info_log( "keypress_update - HOTKEY_AURA_ON" );
				PluginUpdater.Instance.trigger( "toxic_aura_update" );
            }
            if ( Hotkeys.is_hotkey_down( ( int )Hotkeys.HotKeyIndex.HOTKEY_AURA_TYPE ) )
            {
                Diving.Instance.m_toxic_aura_sleep_by_hotkey = !Diving.Instance.m_toxic_aura_sleep_by_hotkey;
				DDPlugin._info_log( "keypress_update - HOTKEY_AURA_TYPE" );
				PluginUpdater.Instance.trigger( "toxic_aura_update" );
            }
            try
			{
				if ( Hotkeys.is_hotkey_down( ( int )Hotkeys.HotKeyIndex.HOTKEY_HEAL ) )
				{
					DDPlugin._info_log( "keypress_update - HOTKEY_HEAL" );
					Diving.Instance.HealPlayer();
				}
				if ( Hotkeys.is_hotkey_down( ( int )Hotkeys.HotKeyIndex.HOTKEY_HARPOON) )
				{
					DDPlugin._info_log( "keypress_update - HOTKEY_HARPOON" );
					Diving.Instance.GivePlayerHarpoon();
				}
				if ( Hotkeys.is_hotkey_down( ( int )Hotkeys.HotKeyIndex.HOTKEY_TRANQ ) )
				{
					DDPlugin._info_log( "keypress_update - HOTKEY_TRANQ" );
					Diving.Instance.GivePlayerTranq();
				}
				if ( Hotkeys.is_hotkey_down( ( int )Hotkeys.HotKeyIndex.HOTKEY_NET ) )
				{
					DDPlugin._info_log( "keypress_update - HOTKEY_NET" );
					Diving.Instance.GivePlayerNet();
				}
				if ( Hotkeys.is_hotkey_down( ( int )Hotkeys.HotKeyIndex.HOTKEY_SNIPE ) )
				{
					DDPlugin._info_log( "keypress_update - HOTKEY_SNIPE" );
					Diving.Instance.GivePlayerSnipe();
				}
				if ( Hotkeys.is_hotkey_down( ( int )Hotkeys.HotKeyIndex.HOTKEY_WEAP_UP ) )
				{
					DDPlugin._info_log( "keypress_update - HOTKEY_WEAP_UP" );
					Diving.Instance.IncreasePlayerWeapon();
				}
				if ( Hotkeys.is_hotkey_down( ( int )Hotkeys.HotKeyIndex.HOTKEY_WEAP_DOWN ) )
				{
					DDPlugin._info_log( "keypress_update - HOTKEY_WEAP_DOWN" );
					Diving.Instance.DecreasePlayerWeapon();
				}
			}
            catch (Exception e)
            {
				DDPlugin._error_log( "** Diving.keypress_update ERROR - Could not Access player inventory - " + e );
			}
		}

        public static int m_modified_character_hash = 0;

        public static void toxic_aura_update() {
            try {
                CharacterController2D character;
                if (!Settings.m_enabled.Value || (character = Singletons.Character) == null || !character.isActiveAndEnabled || !Diving.Instance.m_toxic_aura_enabled_by_hotkey) {
                    return;
                }
                Diving.Instance.initialize();
                if (m_modified_character_hash != character.GetHashCode()) {
                    m_modified_character_hash = character.GetHashCode();
                    
                }
                foreach (FishInteractionBody fish in Resources.FindObjectsOfTypeAll<FishInteractionBody>()) {
                    if (!Settings.m_auto_pickup_fish.Value && fish.InteractionType == FishInteractionBody.FishInteractionType.Calldrone && Settings.m_large_pickups.Value) {
                        fish.InteractionType = FishInteractionBody.FishInteractionType.Pickup;
                    }
                    if (Vector3.Distance(character.transform.position, fish.transform.position) <= Settings.m_aura_radius.Value) {
                        if (Diving.Instance.m_toxic_aura_sleep_by_hotkey) {
                            if (!m_did_set_sleep_buff_value) {
                                DataManager.Instance.BuffEffectDataDic[SLEEP_BUFF_ID].buffvalue1 = SLEEP_BUFF_VALUE;
                                DataManager.Instance.BuffEffectDataDic[SLEEP_BUFF_ID].buffvalue2 = SLEEP_BUFF_VALUE;
                            }
                            BuffHandler buff_handler = fish.gameObject.GetComponent<BuffHandler>();
                            if (buff_handler != null) {
                                Il2CppSystem.Object buff_dict = ReflectionUtils.il2cpp_get_field(buff_handler, "CJCBPPIBGLB")?.GetValue(buff_handler);
                                bool is_asleep = false;
                                if (buff_dict != null && ReflectionUtils.il2cpp_get_field_value<int>(buff_dict, "count") > 0) {
                                    // TODO: Assuming any buff is sleep is not likely to work.  Should dig into the dict entries.
                                    // debug_log(buff_handler.HasBuffType(BuffType.Sleep));
                                    is_asleep = true;
                                }
                                if (!is_asleep) {
                                    buff_handler.AddBuff(SLEEP_BUFF_ID);
                                } else if (fish.InteractionType == FishInteractionBody.FishInteractionType.Pickup) {

                                }
                            }
                        } else {
                            fish.gameObject.GetComponent<Damageable>().OnDie();
                        }
                    }
                }
            } catch (Exception e) {
                DDPlugin._error_log("** Diving.toxic_aura_update ERROR - " + e);
            }
        }
    }
}
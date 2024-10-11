using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System;

namespace SaveState
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class SaveStatePlugin : BaseUnityPlugin
    {
        public const string pluginGUID = "com.metalted.zeepkist.savestate";
        public const string pluginName = "Save State";
        public const string pluginVersion = "1.3";
        public static CarState carState;

        public ConfigEntry<KeyCode> saveKey;
        public ConfigEntry<KeyCode> removeKey;
        public static SaveStatePlugin Instance;

        public Harmony harmony = new Harmony(pluginGUID);

        public void Awake()
        {
            harmony.PatchAll();
            Logger.LogInfo("Plugin Save State is loaded.");

            Instance = this;

            saveKey = Config.Bind("Controls", "Save State", KeyCode.X, "Save the current state.");
            removeKey = Config.Bind("Controls", "Remove State", KeyCode.Z, "Remove the saved state.");
        }
    }

    public class CarState
    {
        public Vector3 carPosition;
        public Quaternion carRotation;
        public Vector3 carVelocity;
        public Vector3 carAngularVelocity;
        public float time;
    }

    public static class Helper
    {
        public static bool spawnedButNotReleased = false;

        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
    }

    [HarmonyPatch(typeof(GameMaster), "Update")]
    public static class GameMasterPatch
    {
        public static void Postfix(GameMaster __instance)
        {
            if (Input.GetKeyDown(SaveStatePlugin.Instance.saveKey.Value) && !Helper.spawnedButNotReleased)
            {
                LevelScriptableObject GlobalLevel = Helper.GetInstanceField(__instance.GetType(), __instance, "GlobalLevel") as LevelScriptableObject;
                if (GlobalLevel != null && GlobalLevel.IsTestLevel)
                {
                    SetupCar carSetup = __instance.carSetups[0];
                    SaveStatePlugin.carState = new CarState()
                    {
                        carPosition = carSetup.transform.position,
                        carRotation = carSetup.transform.rotation,
                        carVelocity = carSetup.cc.GetRB().velocity,
                        carAngularVelocity = carSetup.cc.GetRB().angularVelocity
                    };

                    __instance.manager.messenger.Log("Saved State", 3f);
                }
            }
            else if (Input.GetKeyDown(SaveStatePlugin.Instance.removeKey.Value) && !Helper.spawnedButNotReleased)
            {
                if (SaveStatePlugin.carState != null)
                {
                    SaveStatePlugin.carState = null;
                    __instance.manager.messenger.Log("Removed Save State", 3f);
                }
            }            
        }
    }

    [HarmonyPatch(typeof(GameMaster), "ReleaseTheZeepkists")]
    public static class SetupGameReleaseTheZeepkistsPatch
    { 
        public static void Postfix(GameMaster __instance)
        {
            LevelScriptableObject GlobalLevel = Helper.GetInstanceField(__instance.GetType(), __instance, "GlobalLevel") as LevelScriptableObject;
            if (GlobalLevel == null || !GlobalLevel.IsTestLevel || SaveStatePlugin.carState == null)
            {
                return;
            }

            __instance.carSetups[0].cc.GetRB().velocity = SaveStatePlugin.carState.carVelocity;
            __instance.carSetups[0].cc.GetRB().angularVelocity = SaveStatePlugin.carState.carAngularVelocity;
            Helper.spawnedButNotReleased = false;
        }
    }

    [HarmonyPatch(typeof(LEV_SaveLoad), "AreYouSure")]
    public class LEV_SaveLoadAreYouSure
    {
        public static void Postfix(LEV_SaveLoad __instance)
        {
            if (((bool) Helper.GetInstanceField(__instance.GetType(), __instance, "isSaving")) || SaveStatePlugin.carState == null)
            {
                return;
            }

            SaveStatePlugin.carState = null;
            __instance.central.manager.messenger.Log("Removed Save State", 3f);
        }
    }

    [HarmonyPatch(typeof(GameMaster), "SpawnPlayers")]
    public class SetupGameSpawnPlayers
    {
        public static bool Prefix(GameMaster __instance)
        {
            LevelScriptableObject GlobalLevel = Helper.GetInstanceField(__instance.GetType(), __instance, "GlobalLevel") as LevelScriptableObject;
            if (GlobalLevel == null || !GlobalLevel.IsTestLevel || SaveStatePlugin.carState == null)
            {
                return true;
            }

            SetupCar setupCar = GameObject.Instantiate<SetupCar>(__instance.soapboxPrefab);
            setupCar.transform.position = SaveStatePlugin.carState.carPosition;
            setupCar.transform.rotation = SaveStatePlugin.carState.carRotation;
            setupCar.DoCarSetupSingleplayer();
           
            __instance.PlayersReady.Add(setupCar.GetComponent<ReadyToReset>());
            __instance.PlayersReady[0].GiveMaster(__instance, 0);
            PlayerScreensUI psu = Helper.GetInstanceField(__instance.GetType(), __instance, "PlayerScreensUI") as PlayerScreensUI;
            __instance.PlayersReady[0].screenPointer = psu.GetScreen(0);
            __instance.PlayersReady[0].WakeScreenPointer();
            __instance.playerResults.Add(new WinCompare.Result(0, 0.0f, 0));
            __instance.carSetups.Add(setupCar);
            setupCar.cc.GetRB().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            setupCar.cc.GetRB().isKinematic = true;
            Helper.spawnedButNotReleased = true;

            return false;
        }
    }
}
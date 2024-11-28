﻿using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Reflection;

namespace ResonitePlatformSpoof
{
    public class ResonitePlatformSpoof : ResoniteMod
    {
        public override string Name => "ResonitePlatformSpoof";
        public override string Author => "isovel, runtime, Lyris";
        public override string Version => "2.0.1";
        public override string Link => "https://github.com/isovel/ResonitePlatformSpoof";

        private static FieldInfo _userInitializingEnabled;
        private static ModConfiguration _config;

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> Enabled = new ModConfigurationKey<bool>("enabled", "Enable platform spoofing in new sessions you join", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Platform> TargetPlatform = new ModConfigurationKey<Platform>("spoofed platform", "This will appear as your platform in new sessions you join", () => Platform.Windows);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<HeadOutputDevice> TargetHeadOutputDevice = new ModConfigurationKey<HeadOutputDevice>("spoofed head output device", "This will appear as your head output device in new sessions you join", () => HeadOutputDevice.SteamVR);

        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            builder
                .Version(new Version(1, 1, 0)) // manually set config version (default is 1.0.0)
                .AutoSave(false); // don't autosave on Resonite shutdown (default is true)
        }

        public override void OnEngineInit()
        {
            _config = GetConfiguration();
            Harmony harmony = new Harmony("ch.isota.ResonitePlatformSpoof");

            // we need write access to this private field later, so we reflect to it here and save the reference
            _userInitializingEnabled = AccessTools.DeclaredField(typeof(User), "InitializingEnabled");
            if (_userInitializingEnabled == null)
            {
                Error("Could not reflect field User.InitializingEnabled!");
                return;
            }

            harmony.PatchAll();
            Msg("Hooks installed successfully!");
        }

        [HarmonyPatch(typeof(SessionOutgoingMessageManager), "EnqueueForTransmission", new Type[] { typeof(SyncMessage), typeof(bool), typeof(bool) })]
        private static class ClientPatch
        {
            private static void Prefix(ref SyncMessage message)
            {
                if (_config.GetValue(Enabled) && message is ControlMessage { ControlMessageType: ControlMessage.Message.JoinRequest } controlMessage)
                {
                    Platform oldPlatform = Platform.Other;
                    if (controlMessage.Data.TryExtract("Platform", ref oldPlatform))
                    {
                        Platform newPlatform = _config.GetValue(TargetPlatform);
                        controlMessage.Data.AddOrUpdate("Platform", newPlatform);
                        Msg($"Spoofed join platform from {oldPlatform} to {newPlatform}");
                    }

                    HeadOutputDevice oldHeadOutputDevice = HeadOutputDevice.UNKNOWN;
                    if (controlMessage.Data.TryExtract("HeadDevice", ref oldHeadOutputDevice))
                    {
                        HeadOutputDevice newHeadOutputDevice = _config.GetValue(TargetHeadOutputDevice);
                        controlMessage.Data.AddOrUpdate("HeadDevice", newHeadOutputDevice);
                        Msg($"Spoofed join head output device from {oldHeadOutputDevice} to {newHeadOutputDevice}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(World), nameof(World.CreateHostUser))]
        private static class HostPatch
        {
            private static void Postfix(ref User __result)
            {
                if (!_config.GetValue(Enabled)) return;
                
                Platform oldPlatform = __result.Platform;
                HeadOutputDevice oldHeadOutputDevice = __result.HeadDevice;
                bool oldInitializingEnabled = (bool)_userInitializingEnabled.GetValue(__result);
                _userInitializingEnabled.SetValue(__result, true);
                __result.Platform = _config.GetValue(TargetPlatform);
                __result.HeadDevice = _config.GetValue(TargetHeadOutputDevice);
                _userInitializingEnabled.SetValue(__result, oldInitializingEnabled);
                Msg($"Spoofed host platform from {oldPlatform} to {__result.Platform}");
                Msg($"Spoofed host head output device from {oldHeadOutputDevice} to {__result.HeadDevice}");
            }
        }
    }
}

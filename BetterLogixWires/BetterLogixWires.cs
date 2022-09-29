using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.LogiX;
using BaseX;

namespace BetterLogixWires
{
	public class BetterLogixWires : NeosMod
	{
		public override string Name => "BetterLogixWires";
		public override string Author => "eia485";
		public override string Version => "2.0.0";
		public override string Link => "https://github.com/EIA485/NeosBetterLogixWires/";
		[AutoRegisterConfigKey]
		readonly static ModConfigurationKey<float> NearSpeed = new ModConfigurationKey<float>("NearSpeed", computeDefault: () => 1f);
		[AutoRegisterConfigKey]
		readonly static ModConfigurationKey<float> FarSpeed = new ModConfigurationKey<float>("FarSpeed", computeDefault: () => 0f);

		static ModConfiguration Config;

		void ConfigUpdate(ConfigurationChangedEvent change)
		{
			Debug($"config update called with config: {change.Config.Owner.Name} key: {change.Key.Name} label: {change.Label}");
			if (change.Config == Config)
			{
				Debug("config validated as loc");
				foreach(World world in Engine.Current.WorldManager.Worlds)
				{
					Debug($"looping through world {world.Name}");
					world.RunSynchronously(()=> {
						foreach (DynamicValueVariableDriver<float2> driver in world.GetGloballyRegisteredComponents<DynamicValueVariableDriver<float2>>())
						{
							Debug(driver.Name);
							if (IsValidVarName(driver.VariableName.Value)) {
								Debug($"found: {driver.Name}");
								var speed = GetSpeedFromVarName(driver.VariableName.Value);
								Debug($"speed for {driver.VariableName.Value} is {speed}");
								driver.Target.Target.Value = speed;
							}
						} 
					});

				}
			}
		}	

		public override void OnEngineInit()
		{
			Config = GetConfiguration();
			GetConfiguration().OnThisConfigurationChanged += ConfigUpdate;
			Harmony harmony = new Harmony("net.eia485.BetterLogixWires");
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(ConnectionWire))]
		class ConnectionWirePatch
		{
			[HarmonyPrefix]
			[HarmonyPatch("DeleteHighlight")]
			static bool DeleteHighlightPatch(SyncRef<FresnelMaterial> ___Material, SyncRef<Slot> ___WireSlot, ConnectionWire __instance)
			{
				Type type = GetWireType(__instance.InputField.Target.GetType());
				___Material.Target = GetWireMaterial(color.Red, type.GetDimensions(), typeof(Impulse) == type, __instance);
				___WireSlot.Target.GetComponent<MeshRenderer>(null, false).Materials[0] = ___Material.Target;
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch("SetupStyle")]
			static bool SetupStylePatch(color color, int dimensions, bool isImpulse, Sync<color> ___TypeColor, SyncRef<FresnelMaterial> ___Material, SyncRef<Slot> ___WireSlot, ConnectionWire __instance)
			{
				___TypeColor.Value = color;
				___Material.Target = GetWireMaterial(color, dimensions, isImpulse, __instance);
				___WireSlot.Target.GetComponent<MeshRenderer>(null, false).Materials.Add(___Material.Target);
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch("SetTypeColor")]
			static bool SetTypeColorPrefix(SyncRef<FresnelMaterial> ___Material, Sync<color> ___TypeColor, SyncRef<Slot> ___WireSlot, ConnectionWire __instance)
			{
				Type type = GetWireType(__instance.InputField.Target.GetType());
				___Material.Target = GetWireMaterial(___TypeColor.Value, type.GetDimensions(), typeof(Impulse) == type, __instance);
				___WireSlot.Target.GetComponent<MeshRenderer>(null, false).Materials[0] = ___Material.Target;
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch("SetupTempWire")]
			static bool SetupTempWirePatch(Slot targetPoint, bool output, Sync<bool> ___TempWire, SyncRef<FresnelMaterial> ___Material, SyncRef<Slot> ___WireSlot, Sync<color> ___TypeColor, ConnectionWire __instance)
			{
				___TempWire.Value = true;
				__instance.TargetSlot.Target = targetPoint;
				___WireSlot.Target.ActiveSelf = true;
				___Material.Target = ___WireSlot.Target.DuplicateComponent<FresnelMaterial>(___Material.Target, false);
				float2 value = new float2(0, 1);
				___Material.Target.FarTextureScale.Value = value;
				___Material.Target.NearTextureScale.Value = value;
				___WireSlot.Target.GetComponent<MeshRenderer>(null, false).Materials[0] = ___Material.Target;
				bool isImpulse = ___TypeColor.Value == color.White;

				float neartexspeed = GetTexSpeed(false);
				if (neartexspeed != 0f)
				{
					Panner2D panner = ___WireSlot.Target.AttachComponent<Panner2D>();
					panner.Speed = HandelImpulseSpeed(neartexspeed, isImpulse);
					panner.Target = ___Material.Target.NearTextureOffset;
				}

				float fartexspeed = GetTexSpeed(true);
				if (fartexspeed != 0f)
				{
					Panner2D panner = ___WireSlot.Target.AttachComponent<Panner2D>();
					panner.Speed = HandelImpulseSpeed(fartexspeed, isImpulse);
					panner.Target = ___Material.Target.FarTextureOffset;
				}


				if (output)
				{
					__instance.SetupAsOutput(true);
				}
				MeshCollider component = ___WireSlot.Target.GetComponent<MeshCollider>(null, false);
				if (!(component == null))
				{
					component.Destroy();
				}
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch("OnAttach")]
			static bool OnAttachPrefix(SyncRef<Slot> ___WireSlot, FieldDrive<float3> ___WirePoint, FieldDrive<float3> ___WireTangent, FieldDrive<floatQ> ___WireOrientation, FieldDrive<float> ___WireWidth, ConnectionWire __instance)
			{
				___WireSlot.Target = __instance.Slot.AddSlot("Wire", true);
				StripeWireMesh stripeWireMesh = ___WireSlot.Target.AttachComponent<StripeWireMesh>(true, null);
				stripeWireMesh.Orientation0.Value = floatQ.Euler(0f, 0f, -90f);
				SyncField<float3> tangent = stripeWireMesh.Tangent0;
				float3 left = float3.Left;
				tangent.Value = (left) * 0.25f;
				stripeWireMesh.Width0.Value = 0.025600001f;
				___WirePoint.Target = stripeWireMesh.Point1;
				___WireTangent.Target = stripeWireMesh.Tangent1;
				___WireOrientation.Target = stripeWireMesh.Orientation1;
				___WireWidth.Target = stripeWireMesh.Width1;
				MeshCollider meshCollider = ___WireSlot.Target.AttachComponent<MeshCollider>(true, null);
				meshCollider.Mesh.Target = stripeWireMesh;
				meshCollider.Sidedness.Value = MeshColliderSidedness.DualSided;
				___WireSlot.Target.AttachComponent<SearchBlock>(true, null);
				___WireSlot.Target.ActiveSelf = false;
				___WireSlot.Target.AttachComponent<MeshRenderer>(true, null).Mesh.Target = stripeWireMesh;
				return false;
			}
		}
		
		[HarmonyPatch(typeof(DynamicVariableHandler<float2>))]
		class DynVarPatch
		{
			[HarmonyPrefix]
			[HarmonyPatch("LastValue", MethodType.Getter)]
			static bool PrefixLastValueGetter(string ____currentName, ref float2 __result)
			{
				if (IsValidVarName(____currentName))
				{
					__result = GetSpeedFromVarName(____currentName);
					return false;
				}
				return true;
			}

			[HarmonyPrefix]
			[HarmonyPatch("HasVariable", MethodType.Getter)]
			static bool HasVariablePrefix(string ____currentName, ref bool __result)
			{
				if (IsValidVarName(____currentName))
				{
					__result = true;
					return false;
				}
				return true;
			}

		}

		public static FresnelMaterial GetWireMaterial(color color, int dimensions, bool isImpulse, ConnectionWire instance)
		{
			World world = instance.World;
			Slot LogixAssets = world.AssetsSlot.FindOrAdd("LogixAssets", true);

			string key = string.Format("Logix_WireMaterial_{0}_{1}_{2}", color, isImpulse ? "Impulse" : "Value", dimensions);
			FresnelMaterial fresnelMaterial = world.KeyOwner(key) as FresnelMaterial;
			if (fresnelMaterial == null)
			{
				fresnelMaterial = LogixAssets.AttachComponent<FresnelMaterial>(true, null);
				fresnelMaterial.AssignKey(key, 1, false);
				fresnelMaterial.BlendMode.Value = BlendMode.Alpha;
				fresnelMaterial.ZWrite.Value = ZWrite.On;
				fresnelMaterial.Sidedness.Value = Sidedness.Double;
				StaticTexture2D wireTexture = LogixHelper.GetWireTexture(world, dimensions, isImpulse);
				fresnelMaterial.NearTexture.Target = wireTexture;
				fresnelMaterial.FarTexture.Target = wireTexture;
				float2 value = new float2(0.5f, 1f);
				fresnelMaterial.NearTextureScale.Value = value;
				fresnelMaterial.FarTextureScale.Value = value;
				fresnelMaterial.NearColor.Value = color.MulA(.8f);
				fresnelMaterial.FarColor.Value = color.MulRGB(.5f).MulA(.8f);
			}

			SetupPanners(fresnelMaterial, isImpulse, world, LogixAssets);

			return fresnelMaterial;
		}
		public static Type GetWireType(Type t)
		{
			try
			{
				return t.GetGenericArguments()[0];
			}
			catch
			{
				return t;
			}
		}

		public static void CleanList(ISyncList list)
		{
			for (int i = list.Count - 1; i >= 0; i--)
			{
				ISyncMember syncMember = list.GetElement(i);
				if (syncMember == null || (syncMember as FieldDrive<float2>)?.Target == null)
				{
					list.RemoveElement(i);
				}
			}
		}

		public static float2 HandelImpulseSpeed(bool isFarTexture, bool isImpulse)
		{
			return HandelImpulseSpeed(GetTexSpeed(isFarTexture), isImpulse);
		}

		public static float2 HandelImpulseSpeed(float speed, bool isImpulse)
		{
			return new float2(isImpulse ? (speed * -1) : speed, 0);
		}

		public static float GetTexSpeed(bool isFarTexture)
		{
			return Config.GetValue(isFarTexture ? FarSpeed : NearSpeed);
		}

		public static void SetupPanner(Sync<float2> Target, bool isFarTexture, bool isImpulse, World world, Slot LogixAssets)
		{
			string PannerKey = string.Format("Logix_WirePanner{0}_{1}", isFarTexture ? "_Far" : "", isImpulse ? "Impulse" : "Value");
			Panner2D panner = world.KeyOwner(PannerKey) as Panner2D;
			if (panner == null)
			{
				panner = LogixAssets.AttachComponent<Panner2D>(true, null);
				panner.AssignKey(PannerKey, 1, false);
				float speed = GetTexSpeed(isFarTexture);

				if ((!world.IsAuthority) & speed == 0f)
				{
					panner.Speed = HandelImpulseSpeed(speed, isImpulse);
				}
				SetupMultiDriver(Target, panner, LogixAssets, isImpulse, isFarTexture);

			}
			else
			{
				ValueMultiDriver<float2> multiDriver = panner?.Target?.Parent as ValueMultiDriver<float2>;
				if (multiDriver == null)
				{
					SetupMultiDriver(Target, panner, LogixAssets, isImpulse, isFarTexture);
				}
				else
				{
					ISyncList listOfDrives = multiDriver.Drives;
					CleanList(listOfDrives);
					if (Target.IsDriven | Target.IsLinked)
					{
						if (!((Target.ActiveLink as SyncElement).Component == multiDriver))
						{
							(listOfDrives.AddElement() as FieldDrive<float2>).ForceLink(Target);
						}
					}
					else
						(listOfDrives.AddElement() as FieldDrive<float2>).Target = Target;

					Sync<float2> speedSync = (Sync<float2>)AccessTools.Field(typeof(Panner2D), "_speed").GetValue(panner);
					if (speedSync.IsDriven | speedSync.IsLinked)
					{
						Component syncComp = (speedSync.ActiveLink as SyncElement).Component;
						if (!((syncComp is DynamicValueVariableDriver<float2>) & ((DynamicValueVariableDriver<float2>)syncComp).VariableName==GetVarName(isFarTexture, isImpulse)))
						{ 
							speedSync.DirectLink.ReleaseLink();
							SetupDynVar(speedSync, LogixAssets, isImpulse, isFarTexture);
						}
					}
					else
						SetupDynVar(speedSync, LogixAssets, isImpulse, isFarTexture);
				}
			}
		}

		public static void SetupPanners(FresnelMaterial Target, bool isImpulse, World world, Slot LogixAssets)
		{
			SetupPanner(Target.NearTextureOffset, false, isImpulse, world, LogixAssets);
			SetupPanner(Target.FarTextureOffset, true, isImpulse, world, LogixAssets);
		}

		public static void SetupDynVar(IField<float2> Target, Slot LogixAssets, bool isImpulse, bool isFarTexture)
		{
			DynamicValueVariableDriver<float2> DynVarDriver = LogixAssets.AttachComponent<DynamicValueVariableDriver<float2>>();
			DynVarDriver.VariableName.Value = GetVarName(isFarTexture, isImpulse);
			DynVarDriver.Target.Target = Target;
			if (Target.World.IsAuthority) 
			{
				DynVarDriver.DefaultValue.Value = HandelImpulseSpeed(isFarTexture, isImpulse);
			}
			else
			{
				DynVarDriver.DefaultValue.Value = Target.Value;
			}
		}

		public static string GetVarName(bool isFarTexture, bool isImpulse)
		{
			return "Wolrd/" + (isImpulse ? "Impulse" : "") + (isFarTexture ? "Far" :"Near") + "WireSpeed";
		}

		public static bool IsValidVarName(string name)
		{
			return (name == "World/NearWireSpeed" || name == "Wolrd/FarWireSpeed" || name == "Wolrd/ImpulseNearWireSpeed" || name == "Wolrd/ImpulseFarWireSpeed");
		}

		public static void SetupMultiDriver(Sync<float2> Target, Panner2D panner, Slot LogixAssets, bool isImpulse, bool isFarTexture)
		{
			ValueMultiDriver<float2> multiDriver = LogixAssets.AttachComponent<ValueMultiDriver<float2>>();
			panner.Target = multiDriver.Value;
			panner.Offset = new float2(0, 0);
			(((ISyncList)multiDriver.Drives).AddElement() as FieldDrive<float2>).Target = Target;
			Sync<float2> speedSync = (Sync<float2>)AccessTools.Field(typeof(Panner2D), "_speed").GetValue(panner);
			SetupDynVar(speedSync, LogixAssets, isImpulse, isFarTexture);
		}

		public static float2 GetSpeedFromVarName(string VarName)
		{
			bool isFarTexture = VarName.Contains("Far");
			bool isImpulse = VarName.Contains("Impulse");
			return HandelImpulseSpeed(isFarTexture, isImpulse);
		}
	}
}
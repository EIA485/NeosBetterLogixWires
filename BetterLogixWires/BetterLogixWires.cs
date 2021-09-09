using HarmonyLib;
using NeosModLoader;
using System;
using FrooxEngine;
using FrooxEngine.LogiX;
using BaseX;

namespace BetterLogixWires
{
	public class BetterLogixWires : NeosMod
	{
		public override string Name => "BetterLogixWires";
		public override string Author => "eia485";
		public override string Version => "1.0.1";
		public override string Link => "https://github.com/EIA485/NeosBetterLogixWires/";
		public override void OnEngineInit()
		{
			Harmony harmony = new Harmony("net.eia485.BetterLogixWires");
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(ConnectionWire))]
		class BetterLogixWiresPatch
		{
			[HarmonyPrefix]
			[HarmonyPatch("DeleteHighlight")]
			public static bool DeleteHighlightPatch(SyncRef<FresnelMaterial> ___Material, SyncRef<Slot> ___WireSlot, ConnectionWire __instance)
			{
				Type type = GetWireType(__instance.InputField.Target.GetType());
				___Material.Target = GetWireMaterial(color.Red, type.GetDimensions(), typeof(Impulse) == type, __instance);
				AccessTools.Method(typeof(ConnectionWire), "SetColor").Invoke(__instance, new object[] { color.Red });
				___WireSlot.Target.GetComponent<MeshRenderer>(null, false).Materials[0] = ___Material.Target;
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch("SetupStyle")]
			public static bool SetupStylePatch(color color, int dimensions, bool isImpulse, Sync<color> ___TypeColor, SyncRef<FresnelMaterial> ___Material, SyncRef<Slot> ___WireSlot,  ConnectionWire __instance)
			{
				___TypeColor.Value = color;
				___Material.Target = GetWireMaterial(color, dimensions, isImpulse, __instance);
				AccessTools.Method(typeof(ConnectionWire), "SetColor").Invoke(__instance, new object[] { color });
				___WireSlot.Target.GetComponent<MeshRenderer>(null, false).Materials.Add(___Material.Target);
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch("SetTypeColor")]
			public static bool SetTypeColorPrefix(SyncRef<FresnelMaterial> ___Material, Sync<color> ___TypeColor, SyncRef<Slot> ___WireSlot, ConnectionWire __instance)
			{
				Type type = GetWireType(__instance.InputField.Target.GetType());
				___Material.Target = GetWireMaterial(___TypeColor, type.GetDimensions(), typeof(Impulse)==type, __instance);
				___WireSlot.Target.GetComponent<MeshRenderer>(null, false).Materials[0] = ___Material.Target;
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch("SetupTempWire")]
			public static bool SetupTempWirePatch(Slot targetPoint, bool output, Sync<bool> ___TempWire, SyncRef<FresnelMaterial> ___Material, SyncRef<Slot> ___WireSlot, ConnectionWire __instance)
			{
				___TempWire.Value = true;
				__instance.TargetSlot.Target = targetPoint;
				___WireSlot.Target.ActiveSelf = true;
				___Material.Target = ___WireSlot.Target.DuplicateComponent<FresnelMaterial>(___Material.Target, false);
				float2 value = new float2(0f, 1f);
				___Material.Target.FarTextureScale.Value = value;
				___Material.Target.NearTextureScale.Value = value;
				___WireSlot.Target.GetComponent<MeshRenderer>(null, false).Materials[0] = ___Material.Target;
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
			public static bool OnAttachPrefix(SyncRef<Slot> ___WireSlot, FieldDrive<float3> ___WirePoint, FieldDrive<float3> ___WireTangent, FieldDrive<floatQ> ___WireOrientation, FieldDrive<float> ___WireWidth, ConnectionWire __instance)
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

		public static FresnelMaterial GetWireMaterial(color color, int dimensions, bool isImpulse, ConnectionWire instance)
		{
			string key = string.Format("Logix_WireMaterial_{0}_{1}_{2}", color, isImpulse ? "Impulse" : "Value", dimensions);
			FresnelMaterial fresnelMaterial = instance.World.KeyOwner(key) as FresnelMaterial;
			if (fresnelMaterial == null)
			{
				fresnelMaterial = instance.World.AssetsSlot.FindOrAdd("LogixAssets", true).AttachComponent<FresnelMaterial>(true, null);
				fresnelMaterial.AssignKey(key, 1, false);
				fresnelMaterial.BlendMode.Value = BlendMode.Alpha;
				fresnelMaterial.ZWrite.Value = ZWrite.On;
				fresnelMaterial.Sidedness.Value = Sidedness.Double;
				StaticTexture2D wireTexture = LogixHelper.GetWireTexture(instance.World, dimensions, isImpulse);
				fresnelMaterial.NearTexture.Target = wireTexture;
				fresnelMaterial.FarTexture.Target = wireTexture;
				float2 value = new float2(0.5f, 1f);
				fresnelMaterial.NearTextureScale.Value = value;
				fresnelMaterial.FarTextureScale.Value = value;
			}
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

	}
}
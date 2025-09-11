using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace UltraFishing;

[HarmonyPatch]
public class FishingRodPatch {
  private static FakeWater currentFakeWater; // awful!!

  [HarmonyPrefix]
  [HarmonyPatch(typeof(FishingRodWeapon), "Update")]
	private static bool TheWorstHackInHistory(FishingRodWeapon __instance) // i'm sorry
	{
		if (GameStateManager.Instance.PlayerInputLocked || MonoSingleton<InputManager>.Instance.PerformingCheatMenuCombo())
		{
			return false;
		}
		if ((float)MonoSingleton<FishingHUD>.Instance.timeSinceFishCaught >= 1f && (MonoSingleton<InputManager>.Instance.InputSource.Punch.WasPerformedThisFrame || MonoSingleton<InputManager>.Instance.InputSource.Fire1.WasPerformedThisFrame))
		{
			MonoSingleton<FishingHUD>.Instance.ShowFishCaught(show: false);
		}
		MonoSingleton<FishingHUD>.Instance.SetState(__instance.state);
		switch (__instance.state)
		{
		case FishingRodState.ReadyToThrow:
			if (MonoSingleton<InputManager>.Instance.InputSource.Fire1.WasPerformedThisFrame && (float)__instance.timeSinceAction > 0.1f)
			{
				MonoSingleton<FishingHUD>.Instance.SetPowerMeter(0f, canFish: false);
				__instance.selectedPower = 0f;
				__instance.climaxed = false;
				__instance.fishHooked = false;
				__instance.baitThrown = false;
				__instance.state = FishingRodState.SelectingPower;
				__instance.targetingCircle = Object.Instantiate(__instance.targetPrefab, __instance.approximateTargetPosition, Quaternion.identity);
				__instance.timeSinceAction = 0f;
			}
			break;
		case FishingRodState.SelectingPower:
		{
			__instance.selectedPower += (Time.deltaTime * 0.4f + __instance.selectedPower * 0.01f) * (__instance.climaxed ? (-0.5f) : 1f);
			if (__instance.selectedPower > 1f)
			{
				__instance.selectedPower = 1f;
				__instance.climaxed = true;
			}
			if (__instance.selectedPower < 0.1f)
			{
				__instance.climaxed = false;
			}
			Vector3 vector = __instance.approximateTargetPosition;
			bool flag = false;
			if (Physics.Raycast(vector + Vector3.up * 3f, Vector3.down, out var hitInfo, 30f))
			{
        //Plugin.logger.LogInfo(GenericHelper.GetFullPath(hitInfo.collider.gameObject));
        vector = hitInfo.point;
        if (hitInfo.collider.TryGetComponent<Water>(out var component) && (bool)component.fishDB) {
          __instance.currentFishPool = component.fishDB;
          __instance.currentWater = component;
          flag = true;
          if ((bool)component.overrideFishingPoint)
          {
            vector = component.overrideFishingPoint.position;
          }
        }
        else if (hitInfo.collider.TryGetComponent<FakeWater>(out var fwcomponent) && (bool)fwcomponent.fishDB) {
          __instance.currentFishPool = fwcomponent.fishDB;
          currentFakeWater = fwcomponent;
          flag = true;
          if ((bool)fwcomponent.overrideFishingPoint)
          {
            vector = fwcomponent.overrideFishingPoint.position;
          }
        }
        else
        {
          __instance.currentFishPool = null;
          __instance.currentWater = null;
          currentFakeWater = null;
          flag = false;
        }
      }
			else
			{
				__instance.currentFishPool = null;
				__instance.currentWater = null;
        currentFakeWater = null;
				flag = false;
			}
			MonoSingleton<FishingHUD>.Instance.SetPowerMeter(__instance.selectedPower, flag);
			if (flag)
			{
				__instance.targetingCircle.transform.position = vector + Vector3.up * 0.5f;
				__instance.targetingCircle.SetState(isGood: true, Vector3.Distance(hitInfo.point, MonoSingleton<NewMovement>.Instance.transform.position));
				__instance.targetingCircle.waterNameText.text = __instance.currentFishPool.fullName;
				__instance.targetingCircle.waterNameText.color = __instance.currentFishPool.symbolColor;
			}
			else
			{
				__instance.targetingCircle.transform.position = vector + Vector3.up * 0.5f;
				__instance.targetingCircle.SetState(isGood: false, Vector3.Distance(hitInfo.point, MonoSingleton<NewMovement>.Instance.transform.position));
				__instance.targetingCircle.waterNameText.text = "";
			}
			__instance.targetingCircle.transform.forward = MonoSingleton<NewMovement>.Instance.transform.forward;
			if (MonoSingleton<InputManager>.Instance.InputSource.Fire1.WasCanceledThisFrame && (float)__instance.timeSinceAction > 0.1f)
			{
				if (flag)
				{
					__instance.targetingCircle.GetComponent<Animator>().SetTrigger(FishingRodWeapon.Set);
					__instance.animator.ResetTrigger(FishingRodWeapon.Throw);
					__instance.state = FishingRodState.Throwing;
					__instance.timeSinceAction = 0f;
				}
				else
				{
					__instance.ResetFishing();
				}
			}
			break;
		}
		case FishingRodState.Throwing:
			__instance.targetingCircle.transform.forward = MonoSingleton<NewMovement>.Instance.transform.forward;
			__instance.fishHooked = false;
			if (!__instance.baitThrown)
			{
				__instance.baitThrown = true;
				__instance.animator.SetTrigger(FishingRodWeapon.Throw);
			}
			if ((bool)__instance.spawnedBaitCon && __instance.spawnedBaitCon.landed)
			{
				__instance.state = FishingRodState.WaitingForFish;
				__instance.timeSinceBaitInWater = 0f;
				__instance.distanceAfterThrow = Vector3.Distance(MonoSingleton<NewMovement>.Instance.transform.position, __instance.spawnedBaitCon.baitPoint.position);
				Object.Destroy(__instance.targetingCircle.gameObject);
			}
			break;
		case FishingRodState.WaitingForFish:
			__instance.baitThrown = false;
			if (Vector3.Distance(MonoSingleton<NewMovement>.Instance.transform.position, __instance.spawnedBaitCon.baitPoint.position) > __instance.distanceAfterThrow + 30f)
			{
				Object.Destroy(__instance.spawnedBaitCon.gameObject);
				MonoSingleton<HudMessageReceiver>.Instance.SendHudMessage("Fishing interrupted");
				__instance.ResetFishing();
				break;
			}
			if (!__instance.fishHooked && Random.value < 0.002f + (float)__instance.timeSinceBaitInWater * 0.01f)
			{
        if (__instance.currentWater == null) {
          __instance.hookedFishe = __instance.currentFishPool.GetRandomFish(currentFakeWater.attractFish);
        }
        else {
          __instance.hookedFishe = __instance.currentFishPool.GetRandomFish(__instance.currentWater.attractFish);
        }
				if (__instance.hookedFishe == null)
				{
					if (!__instance.noFishErrorDisplayed)
					{
						__instance.noFishErrorDisplayed = true;
						MonoSingleton<HudMessageReceiver>.Instance.SendHudMessage("Nothing seems to be biting here...");
					}
					break;
				}
        if (__instance.currentWater == null) {
          currentFakeWater.attractFish = null;
        }
        else {
          __instance.currentWater.attractFish = null;
        }
				__instance.fishHooked = true;
				MonoSingleton<FishingHUD>.Instance.SetFishHooked(hooked: true);
				__instance.spawnedBaitCon.FishHooked();
			}
			if (MonoSingleton<InputManager>.Instance.InputSource.Fire1.IsPressed || MonoSingleton<InputManager>.Instance.InputSource.Fire2.IsPressed)
			{
				__instance.animator.SetTrigger(FishingRodWeapon.Pull);
				if (__instance.fishHooked)
				{
					MonoSingleton<FishingHUD>.Instance.SetFishHooked(hooked: false);
					__instance.state = FishingRodState.FishStruggle;
					__instance.spawnedBaitCon.CatchFish(__instance.hookedFishe.fish);
				}
				else
				{
					Object.Destroy(__instance.spawnedBaitCon.gameObject);
					__instance.animator.SetTrigger(FishingRodWeapon.Idle);
					__instance.animator.ResetTrigger(FishingRodWeapon.Throw);
					__instance.animator.Play(FishingRodWeapon.Idle);
					__instance.ResetFishing();
				}
			}
			break;
		case FishingRodState.FishStruggle:
			__instance.fishDesirePosition = Mathf.PerlinNoise(Time.time * 0.3f, 0f);
			__instance.fishTolerance = 0.1f + 0.4f * Mathf.PerlinNoise(Time.time * 0.4f, 0f);
			if (MonoSingleton<InputManager>.Instance.InputSource.Fire1.IsPressed)
			{
				__instance.playerPositionVelocity += 1.9f * Time.deltaTime;
				__instance.animator.SetTrigger(FishingRodWeapon.Pull);
			}
			else if (MonoSingleton<InputManager>.Instance.InputSource.Fire2.IsPressed)
			{
				__instance.playerPositionVelocity -= 1.9f * Time.deltaTime;
				__instance.animator.SetTrigger(FishingRodWeapon.Pull);
			}
			else
			{
				__instance.playerPositionVelocity *= 1f - 2f * Time.deltaTime;
			}
			__instance.playerProvidedPosition += __instance.playerPositionVelocity * Time.deltaTime;
			if (__instance.playerProvidedPosition > 1f)
			{
				__instance.playerProvidedPosition = 1f;
				__instance.playerPositionVelocity = 0f - __instance.playerPositionVelocity;
			}
			if (__instance.playerProvidedPosition < 0f)
			{
				__instance.playerProvidedPosition = 0f;
				__instance.playerPositionVelocity = 0f - __instance.playerPositionVelocity;
			}
			MonoSingleton<FishingHUD>.Instance.SetPlayerStrugglePosition(__instance.playerProvidedPosition);
			MonoSingleton<FishingHUD>.Instance.SetStruggleSatisfied(__instance.struggleSatisfied);
			MonoSingleton<FishingHUD>.Instance.SetFishDesire(Mathf.Clamp01(__instance.topBound), Mathf.Clamp01(__instance.bottomBound));
			__instance.spawnedBaitCon.allowedToProgress = __instance.struggleSatisfied;
			MonoSingleton<FishingHUD>.Instance.SetStruggleProgress(__instance.spawnedBaitCon.flyProgress, __instance.hookedFishe.fish.blockedIcon, __instance.hookedFishe.fish.icon);
			break;
		}
    return false;
	}
}

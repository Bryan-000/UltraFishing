using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Reflection;
using Object = UnityEngine.Object;
using TMPro;

namespace UltraFishing;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {	
  public const string PLUGIN_GUID = "com.earthlingOnFire.UltraFishing";
  public const string PLUGIN_NAME = "UltraFishing";
  public const string PLUGIN_VERSION = "1.0.0";
  public static AssetBundle bundle;
  public static ManualLogSource logger;
  public static string modDir;

  private void Awake() {
    gameObject.hideFlags = HideFlags.HideAndDontSave;
    Plugin.logger = Logger;
  }

  private void Start() {
    string modPath = Assembly.GetExecutingAssembly().Location.ToString();
    modDir = Path.GetDirectoryName(modPath);
    LoadBundle();
    GlobalFishManager.Start();
    new Harmony(PLUGIN_GUID).PatchAll();
    Plugin.logger.LogInfo($"Plugin {PLUGIN_GUID} is loaded!");
  }

  private void LoadBundle() {
    string bundlePath = Path.Combine(modDir, "fishingstuff.fishbundle");
    bundle = AssetBundle.LoadFromFile(bundlePath);
    if (bundle == null) {
      Plugin.logger.LogError("Bundle could not be loaded");
    }
  }
}

[HarmonyPatch]
public static class Patches {

  [HarmonyPostfix]
  [HarmonyPatch(typeof(GunControl), "Start")]
  private static void GunControl_Start_Postfix() {
    if (Object.FindObjectOfType<FishingHUD>() != null) return;

    GameObject fishManagerObj = new GameObject("FishManager");
    fishManagerObj.SetActive(value: false);
    fishManagerObj.AddComponent<FishManager>().fishDbs = new FishDB[]{};
    fishManagerObj.SetActive(value: true);

    SetupWaters();

    GameObject fishingCanvas = AssetHelper.LoadPrefab("Assets/Prefabs/UI/FishingCanvas.prefab");
    GameObject fishingCanvasClone = Object.Instantiate(fishingCanvas);

    GameObject rodWeapon = AssetHelper.LoadPrefab("Assets/Prefabs/Fishing/Fishing Rod Weapon.prefab");
    WeaponIcon rodIcon = rodWeapon.AddComponent<WeaponIcon>();
    
    rodIcon.weaponDescriptor = Plugin.bundle.LoadAsset<WeaponDescriptor>("assets/bundles/fishingstuff/rod descriptor.asset");
   
    AddWeapon(5, rodWeapon);

    LoadFishTerminal();
  }

  [HarmonyPostfix]
  [HarmonyPatch(typeof(FishManager), "UnlockFish")]
  private static void FishManager_UnlockFish_Postfix(ref FishObject fish) {
    GlobalFishManager.UnlockFish(fish);
  }
  
  [HarmonyPrefix]
  [HarmonyPatch(typeof(FishEncyclopedia), "Start")]
  private static bool FishEncyclopedia_Start_Prefix(FishEncyclopedia __instance) {
    if (__instance is GlobalFishEncyclopedia) {
      GlobalFishEncyclopedia globalFishEncyclopedia = (GlobalFishEncyclopedia)__instance;
      globalFishEncyclopedia.StartEncyclopedia();
      return false;
    }
    FishEncyclopedia enc = __instance;
    GameObject gameObject = enc.gameObject;
    GlobalFishEncyclopedia newEnc = gameObject.AddComponent<GlobalFishEncyclopedia>();

    newEnc.fishPicker = enc.fishPicker;
    newEnc.fishInfoContainer = enc.fishInfoContainer;
    newEnc.fishName = enc.fishName;
    newEnc.fishDescription = enc.fishDescription;
    newEnc.fishGrid = enc.fishGrid;
    newEnc.fishButtonTemplate = enc.fishButtonTemplate;
    newEnc.fish3dRenderContainer = enc.fish3dRenderContainer;
    newEnc.fishButtons  = enc.fishButtons;

    Transform backButton = newEnc.fishInfoContainer.transform.Find("Window/Back Button");

    GameObject previousButton = Object.Instantiate(backButton.gameObject, newEnc.fishPicker.transform.parent);
    previousButton.name = "Previous Button";
    previousButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "<<";
    previousButton.transform.localScale = new Vector3(1.4f, 1.4f, 1);
    previousButton.transform.position += Vector3.down * 0.0425f;  
    previousButton.GetComponent<Button>().onClick.AddListener(delegate {
      newEnc.PreviousPage();
    });

    GameObject nextButton = Object.Instantiate(backButton.gameObject, newEnc.fishPicker.transform.parent);
    previousButton.name = "Next Button";
    nextButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = ">>";
    nextButton.transform.localScale = previousButton.transform.localScale;
    if (SceneHelper.CurrentScene.Contains("construct") || SceneHelper.CurrentScene.Contains("5-S")) {
      nextButton.transform.position = previousButton.transform.position + Vector3.left * 0.8313f;
    }
    else {
      nextButton.transform.position = previousButton.transform.position + Vector3.right * 0.8313f;
    }
    nextButton.GetComponent<Button>().onClick.AddListener(delegate {
      newEnc.NextPage();
    });
    newEnc.fishInfoContainer.transform.SetAsLastSibling();

    backButton.GetComponent<ShopButton>().toActivate = new GameObject[]{ newEnc.fishPicker };

    Object.Destroy(enc);

    return false;
  }

  [HarmonyPostfix]
  [HarmonyPatch(typeof(ItemIdentifier), "PutDown")]
  private static void ItemIdentifier_PutDown_Postfix(ItemIdentifier __instance) {
    FishObjectReference fishRef = __instance.GetComponent<FishObjectReference>();
    if (fishRef == null) return;
    FishObject fish = fishRef.fishObject;
    switch (fish.fishName) {
      case "Coin":
        GameObject coin = __instance.transform.Find("Coin").gameObject;
        Camera cam = CameraController.Instance.GetComponent<Camera>();
        GameObject camObj = cam.gameObject;
        GunControl gc = GunControl.Instance;
        FistControl fc = FistControl.Instance;

        fc.currentPunch.CoinFlip();

        GameObject obj = GameObject.Instantiate(coin, camObj.transform.position + camObj.transform.up * -0.5f, camObj.transform.rotation);
        obj.SetActive(true);
        obj.GetComponent<Coin>().sourceWeapon = gc.currentWeapon;
        MonoSingleton<RumbleManager>.Instance.SetVibration(RumbleProperties.CoinToss);
        obj.GetComponent<Rigidbody>().AddForce(camObj.transform.forward * 20f + Vector3.up * 15f + MonoSingleton<PlayerTracker>.Instance.GetPlayerVelocity(trueVelocity: true), ForceMode.VelocityChange);

       GameObject.Destroy(__instance.gameObject);
        break;
    }
  }

  private static void LoadFishTerminal() {
    string scene = SceneHelper.CurrentScene;

    if (!(scene.Contains("Level") || scene.Contains("construct") || scene.Contains("Museum"))) return;

    GameObject terminal = Plugin.bundle.LoadAsset<GameObject>("assets/bundles/fishingstuff/fishing enc terminal.prefab");
    GameObject terminalClone;

    if (scene.Contains("construct")) {
      terminalClone = Object.Instantiate(terminal);
      terminalClone.transform.position = new Vector3(-37, -10, 335.125f);
      terminalClone.transform.localEulerAngles = new Vector3(0, 0, 180);
      return;
    }

    GameObject firstRoom;
    switch (scene) {
      case "Level 6-1":
        firstRoom = GenericHelper.FindGameObject("Interiors/FirstRoom");
        break;
      default:
        firstRoom = GenericHelper.FindGameObjectContaining("FirstRoom");
        break;
    }

    if (firstRoom == null) {
      Plugin.logger.LogError("No FirstRoom could be found!");
      return;
    }

    terminalClone = Object.Instantiate(terminal, firstRoom.transform.GetChild(0));
    terminalClone.transform.localPosition = new Vector3(-6.5f, 2, 32);
    terminalClone.transform.localEulerAngles = Vector3.zero;
  }

  private static void AddWeapon(int slot, GameObject weapon) {
    GunControl gunControl = GunControl.Instance;

    if (slot >= gunControl.slots.Count) return;

    if (gunControl.slots[slot].Exists(w => w.name == weapon.name + "Clone")) return;

    GameObject weaponClone = Object.Instantiate(weapon, gunControl.transform);
    gunControl.slots[slot].Add(weaponClone);
    gunControl.UpdateWeaponList(false);
    weaponClone.SetActive(value: false);
  }

  private static void SetupWaters() {
    switch (SceneHelper.CurrentScene) {
      case "uk_construct":
        WaterBuilder.SetWater("Water Tri")
          .AddFish("Funny Stupid Fish (Friend)")
          .AddFish("PITR Fish")
          .AddFish("Trout")
          .AddFish("Metal Fish")
          .AddFish("Chomper")
          .AddFish("Bomb Fish")
          .AddFish("Eyeball")
          .AddFish("Frog (?)")
          .AddFish("Dope Fish")
          .AddFish("Stickfish")
          .AddFish("Cooked Fish")
          .AddFish("Shark")
          .SetUp("Garry's Lake", Color.green);
        break;
      case "CreditsMuseum2":
        WaterBuilder.SetWater("__Room_Aquarium/", 8)
          .AddFish("Funny Stupid Fish (Friend)")
          .AddFish("PITR Fish")
          .AddFish("Trout")
          .AddFish("Metal Fish")
          .AddFish("Chomper")
          .AddFish("Bomb Fish")
          .AddFish("Eyeball")
          .AddFish("Frog (?)")
          .AddFish("Dope Fish")
          .AddFish("Stickfish")
          .AddFish("Cooked Fish")
          .AddFish("Shark")
          .SetUp("Aquarium", Color.cyan);
        WaterBuilder.SetWater("__Room_Courtyard/__Level Geo/Water Fountain/Water fountain_water_1")
          .AddFish("Coin")
          .SetUp("Fountain", Color.cyan);
        WaterBuilder.SetWater("__Room_FrontDesk_1/__Level geo/Cube (3)")
          .AddFish("Wise Fish")
          .SetUp("Credits", Color.magenta);
        WaterBuilder.SetWater("__Room_Large_Lower/__Level Geo/water")
          .AddFish("Wise Fish")
          .SetUp("Credits", Color.magenta);
        break;
      case "Level 0-2":
        WaterBuilder.SetWater("3 - Blood Room/3 Nonstuff/Decorations/Mulchflow")
          .AddFish("Filthy Screaming Fish (Filsh)")
          .SetUp("Meat", Color.red);
        WaterBuilder.SetWater("3 - Blood Room/3 Nonstuff/Decorations/Mulchflow/Cube")
          .AddFish("Filthy Screaming Fish (Filsh)")
          .SetUp("Meat", Color.red);
        WaterBuilder.SetWater("3 - Blood Room/3 Nonstuff/Decorations/Mulchflow (1)")
          .AddFish("Filthy Screaming Fish (Filsh)")
          .SetUp("Meat", Color.red);
        WaterBuilder.SetWater("3 - Blood Room/3 Nonstuff/Decorations/Mulchflow (1)/Cube")
          .AddFish("Filthy Screaming Fish (Filsh)")
          .SetUp("Meat", Color.red);
        WaterBuilder.SetWater("6 - Crusher Arena/6 Nonstuff/Floor", 4)
          .AddFish("Filthy Screaming Fish (Filsh)")
          .AddMeshCollider()
          .SetUp("Meat", Color.red);
        WaterBuilder.SetWater("7 - Crusher Hallway/7 Nonstuff/Floor/Blood/")
          .AddFish("Filthy Screaming Fish (Filsh)")
          .SetUp("Meat", Color.red);
        foreach (int childIndex in new int[] {0, 1, 3, 5, 7}) {
          WaterBuilder.SetWater("9-9B Tunnel/BloodRiver/", childIndex)
            .AddFish("Filthy Screaming Fish (Filsh)")
            .SetUp("Meat", Color.red);
        }
        break;
      case "Level 0-5":
        WaterBuilder.SetWater("2 - Lava Foundry/Lava/", 0)
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("2 - Lava Foundry/Lava/", 1)
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
      break;
      case "Level 1-1":
        WaterBuilder.SetWater("6 - Waterfall Arena/6 Nonstuff/Cliff and Waterfall", 0, "GameObject")
          .AddFish("null")
          .SetUp("Waterfall", Color.magenta);
        WaterBuilder.SetWater("6 - Waterfall Arena/6 Nonstuff/Cliff and Waterfall", 2, "GameObject")
          .AddFish("null")
          .SetUp("Waterfall", Color.magenta);
        WaterBuilder.SetWater("1 - First Field/1 Stuff/Fountain/Cylinder") // works but not after coin
          .AddFish("Coin")
          .SetUp("Fountain", Color.cyan);
        break;
      case "Level 1-2":
        WaterBuilder.SetWater("7 - Castle Entrance/7 Nonstuff/Sewer/GreenWater") 
          .AddFish("Cancerous Fish")
          .SetUp("Cancerous Water", Color.green);
        WaterBuilder.SetWater("7B - Lava Room/Floor/Lava/")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
      break;
      case "Level 1-3":
        WaterBuilder.SetWater("R2 - Second Arena/R2 Nonstuff/Lava")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("B1-C Lava Staircase/B1-C Nonstuff/Floor/Cube")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("B1-D Lava Hallway/B1-D Nonstuff/Lava/Cube-clone")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
      break;
      case "Level 2-3":
        WaterBuilder.SetWater("1 - Main Hall/1 Nonstuff/Water/", 3)
          .AddFish("Koi Fish")
          .SetUp("Pond", Color.magenta);
        /*WaterBuilder.SetWater("5 - Final Arena/5 Nonstuff/Water (Controlled)/")*/
        /*  .AddFish("")*/
        /*  .SetUp("", Color.magenta);*/
      break;
      case "Level 3-1":
      WaterBuilder.SetWater("5 - Circular Arena/5 Nonstuff/Water/")
        .AddFish("Eyeball")
        .AddFish("Frog (?)")
        .SetUp("Blood", Color.red);
        WaterBuilder.SetWater("3 - Big Arena/3 Nonstuff/Floor/Acid/")
          .AddFish("Melted Fish")
          .SetUp("Acid", Color.green);
        WaterBuilder.SetWater("9 - Uphill Battle/9 Nonstuff/Floor/Acid/Cube")
          .AddFish("Melted Fish")
          .SetUp("Acid", Color.green);
        WaterBuilder.SetWater("9 - Uphill Battle/9 Nonstuff/Floor/Acid/Cube (1)")
          .AddFish("Melted Fish")
          .SetUp("Acid", Color.green);
        WaterBuilder.SetWater("10 - Structure/10 Stuff/AcidRaiser (1)/AcidRaiser/Acid/")
          .AddFish("Melted Fish")
          .SetUp("Acid", Color.green);
        break;
      case "Level 3-2":
        WaterBuilder.SetWater("3 - Other Room/3 Nonstuff/Water/")
          .AddFish("Melted Fish")
          .SetUp("Acid", Color.green);
        break;
      //do something in 4-1 (pool of water beginning, lava later)
      //do something in 4-3 pool of water
      //4-4 chamber of the feline and the rodent
      case "Level 4-4":
        WaterBuilder.CreateWater("8 - Outro/8 Stuff/Landing (Broken) (1)")
          .SetPosition(1065, 255, 692)
          .SetLocalScale(9, 0, 9)
          .AddFish("Eyeball")
          .SetUp("\"V2\"", Color.red);
        break;
      case "Level 5-1":
        WaterBuilder.SetWater("Underwaters/All Waters/Cube (3)") 
          .AddFish("Funny Stupid Fish (Friend)")
          .AddFish("PITR Fish")
          .SetUp("Cave Lake", Color.cyan);
        WaterBuilder.SetWater("IntroParent/Intro/Intro A - First Cave/Plane/Cube")
          .AddFish("Chomper")
          .SetUp("Cave Pool", Color.gray);
        WaterBuilder.SetWater("IntroParent/Intro/Intro A - First Cave/Plane (1)/Cube")
          .AddFish("Chomper")
          .SetUp("Cave Pool", Color.gray);
        WaterBuilder.SetWater("IntroParent/Intro/Intro C - Second Cave/Plane (2)/Cube")
          .AddFish("Chomper")
          .SetUp("Cave Pool", Color.gray);
        WaterBuilder.SetWater("2B - Arena B/B Nonstuff/Water/Cube")
          .AddFish("Chomper")
          .SetUp("Cave Pool", Color.gray);
        WaterBuilder.SetWater("1 - Main Cave/1 Nonstuff/Drained/Cube")
          .AddFish("Dope Fish")
          .SetUp("Cave Lake", Color.cyan);
        WaterBuilder.SetWater("2A - Arena A/A Nonstuff/Drained (1)/Cube")
          .AddFish("PITR Fish")
          .AddFish("Funny Stupid Fish (Friend)")
          .SetUp("Cave Lake", Color.cyan);
        break;
      case "Level 5-2":
        WaterBuilder.SetWater("Sea/Sea Itself/Filler/WaterTrigger") //sometimes doesn't work
          .AddFish("Nerd Shark", 0)
          .AddBait("3 - Ferryman's Cabin/3 Nonstuff/Interior/Book with Stand/Book", "Nerd Shark")
          .SetUp("The Ocean Styx", Color.blue);
        break;
      case "Level 5-4":
        WaterBuilder.SetWater("Surface/Stuff/Watersurface/Cube")
          .AddFish("Eel (?)")
          .SetUp("The Ocean Styx", Color.blue);
        WaterBuilder.SetWater("Surface/Stuff/Watersurface/Cube (1)")
          .AddFish("Eel (?)")
          .SetUp("The Ocean Styx", Color.blue);
        WaterBuilder.SetWater("Surface/Stuff/Watersurface/Cube (2)")
          .AddFish("Eel (?)")
          .SetUp("The Ocean Styx", Color.blue);
        WaterBuilder.SetWater("Surface/Stuff/Watersurface (Sunken)/NewDeath/Anti-diver Colliders/Cube")
          .AddFish("Eel (?)")
          .SetUp("The Ocean Styx", Color.blue);
        for (int i = 0; i < 8; i++) {
          WaterBuilder.SetWater("Surface/Stuff/Watersurface (Sunken)/NewWater/", i)
            .AddFish("Eel (?)")
            .SetUp("The Ocean Styx", Color.blue);
        }
        break;
      case "Level 6-1":
        WaterBuilder.SetWater("Interiors/6 - Lava Chasm/6 Nonstuff/Lava")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("10 - Chapel/10 Nonstuff/Pit")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("14 - Hall of Sacreligious Remains/14 Nonstuff/Lava Rim/Lava/Cube (9)")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("14 - Hall of Sacreligious Remains/14 Nonstuff/Lava Rim/Lava/Cube (9)/Cube (7)")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("14 - Hall of Sacreligious Remains/14 Nonstuff/Lava Rim/Lava/Cube (6)")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("14 - Hall of Sacreligious Remains/14 Nonstuff/Lava Rim/Lava/Cube (6)/Cube (7)")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        break;
      case "Level 7-2":
        //optionally, Outdoors/12 - Red Skull Trench/12 Nonstuff/Water
        WaterBuilder.SetWater("Outdoors/Decorations/Ground/Blood")
          .AddFish("Bomb Fish")
          .SetUp("The River Phlegethon", Color.black);
        break;
      case "Level 7-4":
        WaterBuilder.SetWater("Main/Interior/InteriorStuff/BoilingBlood")
          .AddFish("Melted Fish")
          .SetUp("Earthmover Insides", Color.black);
        WaterBuilder.SetWater("Main/Interior/InteriorStuff/BoilingBlood (Return)")
          .AddFish("Melted Fish")
          .SetUp("Earthmover Insides", Color.black);
        break;
      case "Level 7-S":
        WaterBuilder.SetWater("Pond/Pond Underwater")
          .AddFish("Koi Fish")
          .SetUp("Pond", Color.white);
        WaterBuilder.SetWater("Pit/PitDestroyer") //find a way to raise safely
          .AddFish("Wise Fish")
          .SetUp("Depths Of The Library", Color.gray);
        WaterBuilder.SetWater("Curved Pit Destroyer")
          .AddFish("Wise Fish")
          .SetUp("Depths Of The Library", Color.gray);
        WaterBuilder.SetWater("Curved Pit Destroyer/GameObject")
          .AddFish("Wise Fish")
          .SetUp("Depths Of The Library", Color.gray);
        WaterBuilder.SetWater("7-S_Unpaintable/Exterior/The Water Ups_Todo/The Water Ups/Water Ups Ocean")
          .AddFish("\"size 2\"", GlobalFishManager.CanCatchSize2())
          .AddMeshCollider(false)
          .SetUp("The Water Ups", Color.blue);
        break;
      case "Level P-2":
        WaterBuilder.SetWater("Shortcut/Deathzones/Deathzone")
          .AddFish("Metal(?) Fish")
          .SetUp("Scrindonguloded Souls", Color.black);
        WaterBuilder.SetWater("Shortcut/Deathzones", 2)
          .AddFish("Metal(?) Fish")
          .SetUp("Scrindonguloded Souls", Color.black);
        WaterBuilder.SetWater("Main Section/Outside/2 - Bridge Street/Floor/Plane (2)/Plane")
          .AddFish("Metal(?) Fish")
          .SetUp("Scrongled Souls", Color.black);
        WaterBuilder.SetWater("Main Section/Outside/2 - Bridge Street/Floor/Plane (3)/Plane (1)")
          .AddFish("Metal(?) Fish")
          .SetUp("Scrongled Souls", Color.black);
        WaterBuilder.SetWater("Main Section/Inside/6 - Soul Tunnel/6 Nonstuff (1)/Soulwalls/Cube(Clone)")
          .AddFish("Metal(?) Fish")
          .SetUp("Damned Souls", Color.black);
        WaterBuilder.SetWater("Main Section/Inside/6 - Soul Tunnel/6 Nonstuff (1)/Soulwalls", 2)
          .AddFish("Metal(?) Fish")
          .SetUp("Damned Souls", Color.black);
        WaterBuilder.SetWater("Main Section/Inside/6 - Soul Tunnel/6 Nonstuff/Soulwalls/Cube(Clone)")
          .AddFish("Metal(?) Fish")
          .SetUp("Damned Souls", Color.black);
        WaterBuilder.SetWater("Main Section/Inside/6 - Soul Tunnel/6 Nonstuff/Soulwalls", 2)
          .AddFish("Metal(?) Fish")
          .SetUp("Damned Souls", Color.black);
        break;
      case "Level 0-E":
        WaterBuilder.SetWater("6 - Crossroads/6 Nonstuff/6 Hot Only/Blood")
          .AddFish("Filthy Screaming Fish (Filsh)")
          .SetUp("Meat", Color.red);
        WaterBuilder.SetWater("8 - Lava Foundry/8 Hot Only/Lava (1)/Cube")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("5-6 Water")
          .AddFish("Frozen Fish")
          .SetUp("Freezing Water", Color.white);
        break;
      case "Level 1-E":
        WaterBuilder.SetWater("2 - Skull Field % Blue Skull Room/2 Nonstuff/Return Trip Nonstuff/Lava/")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        WaterBuilder.SetWater("1 - First Field % Skylight Hallway/1 Nonstuff/1 Lava/Cube")
          .AddFish("Overcooked Fish")
          .SetUp("Lava", Color.red);
        break;
    }
  }
}

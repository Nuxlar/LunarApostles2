using BepInEx;
using HarmonyLib;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.ContentManagement;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace LunarApostles
{
  [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
  [BepInDependency(R2API.ContentManagement.R2APIContentManager.PluginGUID)]
  [BepInDependency(R2API.PrefabAPI.PluginGUID)]
  public class Main : BaseUnityPlugin
  {
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "Nuxlar";
    public const string PluginName = "LunarApostles";
    public const string PluginVersion = "1.3.0";

    internal static Main Instance { get; private set; }
    public static string PluginDirectory { get; private set; }

    public static GameObject apostleMaster;
    public static GameObject apostleMassBody;
    public static GameObject apostleDesignBody;
    public static GameObject apostleBloodBody;
    public static GameObject apostleSoulBody;

    public static SpawnCard scavSpawnCard;
    public static Material moonTerrainMat;
    public static Material bloodMat;

    public void Awake()
    {
      Instance = this;

      Log.Init(Logger);
      LoadAssets();

      PluginDirectory = System.IO.Path.GetDirectoryName(Info.Location);
      LanguageFolderHandler.Register(PluginDirectory);

      //ScavLunarEncounter
      On.RoR2.ScriptedCombatEncounter.Start += LimboCheck;

      /*
        new system for keeping track of the pillars chosen
        possible solutions
        1. have the pillars in limbo, selecting the pillar reloads the scene or begins that specific encounter
        2. repeat previous implementation, swapping between moon2 and limbo
        3. gauntlet system, where you have to fight all 4 in succession
      */
    }

    private void LimboCheck(On.RoR2.ScriptedCombatEncounter.orig_Start orig, ScriptedCombatEncounter self)
    {
      if (self.name == "ScavLunarEncounter")
      {
        Beautify();
        Transform spawnPos = self.spawns[0].explicitSpawnPosition;
        self.spawns = new ScriptedCombatEncounter.SpawnInfo[] { new ScriptedCombatEncounter.SpawnInfo() { explicitSpawnPosition = spawnPos, spawnCard = scavSpawnCard } };
      }
      orig(self);
    }

    public static void Beautify()
    {
      MeshRenderer[] meshList = Object.FindObjectsOfType(typeof(MeshRenderer)) as MeshRenderer[];
      foreach (MeshRenderer renderer in meshList)
      {
        GameObject meshBase = renderer.gameObject;
        if (meshBase.name == "Floor")
          renderer.sharedMaterial = moonTerrainMat;
        if (meshBase.name == "Mesh")
          renderer.sharedMaterial = bloodMat;
      }
    }

    private static void LoadAssets()
    {
      AssetReferenceT<GameObject> bodyRef = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_ScavLunar.ScavLunar1Body_prefab);
      AssetReferenceT<GameObject> masterRef = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_ScavLunar.ScavLunar1Master_prefab);
      AssetReferenceT<SpawnCard> cardRef = new AssetReferenceT<SpawnCard>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_ScavLunar.cscScavLunar_asset);
      AssetReferenceT<Material> terrainMatRef = new AssetReferenceT<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_mysteryspace.matMSTerrain_mat);
      AssetReferenceT<Material> bloodMatRef = new AssetReferenceT<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_moon2.matMoonbatteryBlood_mat);
      AssetAsyncReferenceManager<Material>.LoadAsset(terrainMatRef).Completed += (x) => moonTerrainMat = x.Result;
      AssetAsyncReferenceManager<Material>.LoadAsset(bloodMatRef).Completed += (x) => bloodMat = x.Result;

      AssetAsyncReferenceManager<GameObject>.LoadAsset(bodyRef).Completed += (x) =>
      {
        GameObject bodyPrefab = x.Result;

        List<GameObject> bodies = new();
        List<GameObject> masters = new();
        apostleMassBody = PrefabAPI.InstantiateClone(bodyPrefab, "ApostleMassBody");
        apostleDesignBody = PrefabAPI.InstantiateClone(bodyPrefab, "ApostleDesignBody");
        apostleBloodBody = PrefabAPI.InstantiateClone(bodyPrefab, "ApostleBloodBody");
        apostleSoulBody = PrefabAPI.InstantiateClone(bodyPrefab, "ApostleSoulBody");
        bodies.Add(apostleMassBody);
        bodies.Add(apostleDesignBody);
        bodies.Add(apostleBloodBody);
        bodies.Add(apostleSoulBody);

        for (int i = 0; i < bodies.Count; i++)
        {
          GameObject prefab = bodies[i];
          GameObject masterPrefab = PrefabAPI.InstantiateClone(AssetAsyncReferenceManager<GameObject>.LoadAsset(masterRef).WaitForCompletion(), "ApostleNux" + i.ToString() + "Master");
          masterPrefab.GetComponent<CharacterMaster>().bodyPrefab = prefab;

          foreach (GivePickupsOnStart component in masterPrefab.GetComponents<GivePickupsOnStart>())
            Destroy(component);

          GivePickupsOnStart givePickups = masterPrefab.AddComponent<GivePickupsOnStart>();
          givePickups.overwriteEquipment = true;
          givePickups.equipmentString = "CrippleWard";
          givePickups.itemInfos = new GivePickupsOnStart.ItemInfo[] { new GivePickupsOnStart.ItemInfo() { count = 1, itemString = "AdaptiveArmor" } };

          AISkillDriver aiSkillDriver1 = masterPrefab.GetComponents<AISkillDriver>().Where(x => x.customName == "UseEquipmentAndFireCannon").First();
          aiSkillDriver1.maxDistance = 160f;
          aiSkillDriver1.maxUserHealthFraction = 0.9f;
          masterPrefab.GetComponents<AISkillDriver>().Where(x => x.customName == "FireCannon").First().maxDistance = 160f;
          AISkillDriver aiSkillDriver2 = masterPrefab.GetComponents<AISkillDriver>().Where(x => x.skillSlot == SkillSlot.Secondary).First();
          aiSkillDriver2.maxUserHealthFraction = 0.95f;
          aiSkillDriver2.maxDistance = 160f;
          masterPrefab.GetComponents<AISkillDriver>().Where(x => x.skillSlot == SkillSlot.Utility).First().maxUserHealthFraction = 0.85f;

          CharacterBody body = prefab.GetComponent<CharacterBody>();
          body.baseMaxHealth = 250f;
          body.levelMaxHealth = 75f;
          body.baseDamage = 12f;
          body.levelDamage = 2.4f;
          body.baseNameToken = "Bhelam";
          body.subtitleNameToken = "Apostle of Blood ";

          masters.Add(masterPrefab);
          ContentAddition.AddMaster(masterPrefab);
          ContentAddition.AddBody(prefab);
        }

        MultiCharacterSpawnCard msc = AssetAsyncReferenceManager<SpawnCard>.LoadAsset(cardRef).WaitForCompletion() as MultiCharacterSpawnCard;
        msc.masterPrefabs = masters.ToArray();
        scavSpawnCard = msc;
      };
    }
  }
}
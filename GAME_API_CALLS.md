# HS_Portal — game calls (from Source)

Not extracted from the game DLL. This is `HS_Portal/Source`. Unity / System / Harmony / our types skipped.

3.2 vs 3.3: `7DaysRef/HS_Portal_3.2_vs_3.3.md`

## Inherits
- `ConsoleCmdAbstract`
- `IEquatable`
- `IModApi`
- `ItemAction`
- `ItemActionRanged`
- `NetPackage`
- `XUiController`

## HarmonyPatch / typeof game types
- `Entity`
- `ItemActionRanged`
- `NetPackageManager`
- `ProjectileMoveScript`
- `ReflectionHelpers`
- `string`
- `vp_FPController`

## Type.Method (static / nested)
- `AssetBundle.GetAllLoadedAssetBundles`
- `Block.GetBlockByName`
- `Center.ToString`
- `FieldType.GetGenericArguments`
- `GameIO.GetSaveGameDir`
- `GameManager.ShowTooltip`
- `GameStartDone.RegisterHandler`
- `Instance.IsPaused`
- `Instance.Output`
- `Instance.SpawnParticleEffectServer`
- `ItemClass.GetItem`
- `LayerMask.NameToLayer`
- `Localization.Get`
- `Log.Error`
- `Log.Out`
- `Log.Warning`
- `Manager.BroadcastPlay`
- `Manager.Play`
- `Normal.ToString`
- `ParticleEffect.SpawnParticleEffect`
- `PlayerSpawnedInWorld.RegisterHandler`
- `Versioning.TargetFrameworkAttribute`
- `Voxel.Raycast`
- `WorldShuttingDown.RegisterHandler`

## new
- `new BinaryReader`
- `new BinaryWriter`
- `new BlockChangeInfo`
- `new ItemStack`
- `new ItemValue`
- `new ParticleEffect`
- `new Ray`
- `new Vector3i`

## override (must still exist on 3.3 base)
- `CanExecute`
- `Equals`
- `Execute`
- `ExecuteAction`
- `GetBindingValueInternal`
- `getCommands`
- `getDescription`
- `GetHashCode`
- `getHelp`
- `GetLength`
- `Init`
- `OnHoldingUpdate`
- `OnOpen`
- `ProcessPackage`
- `read`
- `ReadFrom`
- `Update`
- `write`

## By file (line)
### HSGameVersion.cs
- `20: Log.Error`
- `25: Log.Error`
- `31: Log.Out`

### HSPortalCommands.cs
- `5: inherits ConsoleCmdAbstract`
- `7: override getCommands`
- `12: override getDescription`
- `19: override getHelp`
- `32: override Execute`
- `117: Localization.Get`
- `138: Center.ToString`
- `138: Normal.ToString`
- `152: Localization.Get`
- `154: Localization.Get`
- `164: ItemClass.GetItem`
- `166: new ItemStack`
- `166: new ItemValue`
- `169: ItemClass.GetItem`
- `172: new ItemStack`
- `172: new ItemValue`
- `182: new ItemStack`
- `183: new ItemStack`
- `198: Localization.Get`
- `200: Localization.Get`
- `215: new Vector3i`
- `237: new Vector3i`
- `241: new BlockChangeInfo`
- `242: new BlockChangeInfo`
- `251: new Vector3i`
- `260: Block.GetBlockByName`
- `268: Instance.Output`

### HSPortalDebug.cs
- `11: Log.Out`
- `16: Log.Out`
- `21: Log.Warning`
- `26: Log.Error`

### HSPortalGel.cs
- `15: inherits IEquatable`
- `23: new Vector3i`
- `26: override Equals`
- `27: override GetHashCode`
- `67: Localization.Get`
- `77: Localization.Get`
- `79: Voxel.Raycast`
- `165: new Vector3i`
- `187: new Vector3i`
- `203: new Vector3i`
- `229: new BinaryWriter`
- `261: new BinaryReader`
- `269: new Vector3i`
- `286: GameIO.GetSaveGameDir`

### HSPortalMath.cs
- `29: new Vector3i`
- `30: new Vector3i`
- `31: new Vector3i`
- `32: new Vector3i`
- `33: new Vector3i`
- `34: new Vector3i`
- `214: new Vector3i`
- `215: new Vector3i`
- `224: new Vector3i`
- `225: new Vector3i`
- `231: new Vector3i`

### HSPortalMod.cs
- `4: inherits IModApi`
- `15: GameStartDone.RegisterHandler`
- `16: WorldShuttingDown.RegisterHandler`
- `17: PlayerSpawnedInWorld.RegisterHandler`

### HSPortalNet.cs
- `56: FieldType.GetGenericArguments`
- `134: GameManager.ShowTooltip`
- `157: GameManager.ShowTooltip`
- `198: new Ray`
- `204: Localization.Get`
- `228: new Ray`
- `235: Localization.Get`
- `254: inherits NetPackage`
- `337: override read`
- `356: new Vector3i`
- `362: override write`
- `425: new Vector3i`
- `430: override ProcessPackage`
- `468: override GetLength`
- `474: HarmonyPatch NetPackageManager`

### HSPortalPlacement.cs
- `21: Localization.Get`
- `32: Localization.Get`
- `34: new Ray`
- `35: Voxel.Raycast`
- `44: Localization.Get`
- `62: Localization.Get`
- `69: Localization.Get`
- `172: new Vector3i`
- `172: new Vector3i`
- `174: new Vector3i`
- `174: new Vector3i`
- `174: new Vector3i`
- `174: new Vector3i`
- `182: new Vector3i`
- `208: new ParticleEffect`
- `212: Instance.SpawnParticleEffectServer`
- `215: ParticleEffect.SpawnParticleEffect`
- `220: Manager.BroadcastPlay`
- `273: new Vector3i`

### HSPortalShots.cs
- `18: new Ray`
- `22: new Ray`
- `32: new Ray`
- `92: new Ray`
- `92: Voxel.Raycast`
- `111: HarmonyPatch ProjectileMoveScript`
- `121: HarmonyPatch ItemActionRanged`

### HSPortalTeleporter.cs
- `258: HarmonyPatch vp_FPController`

### HSPortalVisual.cs
- `138: LayerMask.NameToLayer`
- `388: AssetBundle.GetAllLoadedAssetBundles`

### HSPortalWorld.cs
- `141: Localization.Get`

### HSPortalWornBoots.cs
- `229: AssetBundle.GetAllLoadedAssetBundles`

### ItemActionHSPortalGelGun.cs
- `6: inherits ItemActionRanged`
- `10: override ReadFrom`
- `16: override OnHoldingUpdate`
- `21: override CanExecute`
- `26: override ExecuteAction`
- `40: Manager.Play`
- `42: Localization.Get`
- `50: Manager.Play`
- `50: Manager.Play`
- `81: GameManager.ShowTooltip`
- `81: Localization.Get`
- `114: ItemClass.GetItem`

### ItemActionHSPortalGun.cs
- `6: inherits ItemAction`
- `10: override ExecuteAction`
- `23: Manager.Play`
- `88: GameManager.ShowTooltip`
- `88: Localization.Get`
- `89: Manager.Play`
- `95: Localization.Get`
- `96: GameManager.ShowTooltip`
- `125: Instance.IsPaused`
- `155: GameManager.ShowTooltip`
- `155: Localization.Get`
- `158: Manager.Play`

### XUiC_HSPortalHud.cs
- `6: inherits XUiController`
- `8: override Init`
- `14: override OnOpen`
- `20: override Update`
- `26: override GetBindingValueInternal`
- `46: Localization.Get`
- `46: Localization.Get`
- `49: Localization.Get`
- `49: Localization.Get`
- `52: Localization.Get`
- `52: Localization.Get`
- `55: Localization.Get`
- `62: HarmonyPatch ReflectionHelpers`

### obj\Release\.NETFramework,Version=v4.8.AssemblyAttributes.cs
- `4: Versioning.TargetFrameworkAttribute`



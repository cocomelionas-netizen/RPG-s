cd "$(dirname "$0")"
R=$(cd "$(dirname "$0")/../.." && pwd)/Assets/_Project/Scripts/Runtime
T=$(cd "$(dirname "$0")/../.." && pwd)/Assets/_Project/Tests/PlayMode
rm -rf src && mkdir -p src
cp $R/Navigation/*.cs $R/Monsters/*.cs $R/Progression/*.cs $R/Items/*.cs $R/Accounts/*.cs $R/Dungeons/*.cs $R/Vendors/*.cs $R/CameraSystem/CameraFollow.cs $R/CameraSystem/RenderResolution.cs $R/Networking/PlayerNetworkMovement.cs $R/Networking/MovementValidator.cs $R/World/*.cs $R/Networking/NetworkBootstrap.cs $R/Networking/NetworkLaunchOptions.cs $R/Networking/DevConnectionPanel.cs $R/Combat/Health.cs $R/Combat/AutoAttack.cs $R/Combat/HealthRegeneration.cs $R/Combat/Faction.cs $R/Combat/CombatEffects.cs $R/Combat/Mana.cs $R/Combat/DamageSchool.cs $R/Combat/StatusEffects.cs $R/Combat/StatusInfo.cs $R/Combat/PlayerRespawn.cs $R/Combat/HealthPresenter.cs $R/Combat/PlayerTargeting.cs $R/Characters/*.cs $R/Networking/MotionState.cs $R/Player/PlayerMovement.cs $R/Player/Cheats.cs $R/UI/*.cs src/
cp $T/MonsterTests.cs $T/NavigationTests.cs src/
cp $T/VendorTests.cs $T/DungeonTests.cs $T/AccountTests.cs $T/InventoryTests.cs $T/ProgressionTests.cs $T/LevelCurveTests.cs $T/AttributeAndManaTests.cs $T/WaystoneTests.cs $T/GemTests.cs $T/IconTests.cs $T/EmpowerTests.cs $T/ChestTests.cs $T/SpellbookTests.cs $T/GossipTests.cs $T/CheatTests.cs $T/RenderResolutionTests.cs $T/StatusInfoTests.cs $T/WorldMapTests.cs $T/VendorRestockTests.cs $T/DayNightTests.cs $T/AudioTests.cs src/
sed -i 's/\.linearVelocity/.velocity/g' src/*.cs
sed -i 's/ContactFilter2D\.noFilter/new ContactFilter2D().NoFilter()/g' src/*.cs
sed -i 's/FindObjectsByType<UnitFrame>(FindObjectsInactive.Include)/FindObjectsOfType<UnitFrame>(true)/; s/FindObjectsByType<Health>()/FindObjectsOfType<Health>()/' src/PlayerTargeting.cs
sed -i 's/Object.FindAnyObjectByType<LootBag>()/Object.FindObjectOfType<LootBag>()/' src/InventoryTests.cs
sed -i 's/FindObjectsByType<LootBag>()/FindObjectsOfType<LootBag>()/' src/DungeonManager.cs src/DungeonTests.cs
sed -i 's/FindAnyObjectByType</FindObjectOfType</g' src/*.cs

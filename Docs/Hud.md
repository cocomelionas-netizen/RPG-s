# HUD (įkvėpta Source išdėstymo)

Išdėstymas pagal Source ekrano nuotrauką, bet visa grafika sugeneruota mūsų pačių (jokių jų failų).

| Vieta | Kas | Klasė |
|-------|-----|-------|
| Viršuje kairėje | Žaidėjo rėmelis: apvalus portretas (dabartinis veikėjo kadras), lygio ženkliukas, vardas ir klasė, HP (raudona) ir mana (mėlyna) su % | `UnitFrame` (LocalPlayer) |
| Viršuje centre | Taikinio rėmelis, veidrodinis (portretas dešinėje), vardas raudonas priešams, mėlynas žaidėjams | `UnitFrame` (Target) |
| Viršuje dešinėje | Zonos pavadinimas (Greenvale / požemio vardas) ir minimapas: žemė, kliūtys, taškai — tu (balta), žaidėjai (mėlyna), monstrai (raudona), prekeiviai (geltona), portalai (violetinė) | `Minimap` |
| Apačioje kairėje | Žinučių langas: loot, auksas, patirtis, lygiai, įspėjimai (`MessageLog.Add`) | `MessageLog` |
| Apačioje centre | Veiksmų juosta rėmelyje: 1 ataka, 2–4 gebėjimai, 5 gydymo ir 6 manos gėrimas su likusiu kiekiu; po ja plona patirties juosta | `AbilityBar`, `ExperienceBar` |
| Virš monstrų | Raudoni vardai virš HP juostos (pasirinkto — su lygiu), kitų žaidėjų vardai mėlyni; monstrų HP juosta matoma visada | `UnitNameplates`, `HealthPresenter` |

Minimapo vaizdas piešiamas vieną kartą kiekvienam žemėlapiui iš `WorldNavigation` (vaikščiojama / užblokuota),
zonos vardas — `WorldNavigation.ZoneName`.

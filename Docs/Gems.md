# Brangakmeniai ir lizdai

| Brangakmenis | Atributas | Chipped | Paprastas | Flawless |
|---|---|---|---|---|
| Ruby | Strength | +2 | +4 | +7 |
| Topaz | Agility | +2 | +4 | +7 |
| Sapphire | Intelligence | +2 | +4 | +7 |
| Emerald | Vitality | +2 | +4 | +7 |

- **Lizdai:** 25% monstrų numestų ginklų ir šarvų turi lizdą (`ItemStack.Socket`: 0 — nėra, -1 — tuščias,
  >0 — įstatyto brangakmenio daikto id). Lizdas rodomas aprašyme („Socket: empty“ / „Socket: Ruby (+4 Strength)“).
- **Įstatymas:** paspaudus brangakmenį krepšyje jis įstatomas į pirmą dėvimą daiktą (ginklas, galva, krūtinė, kojos)
  su tuščiu lizdu. Brangakmenis lieka visam laikui, jo atributai skaičiuojami kaip daikto.
- **Juvelyras** Gemma Brightstone (miestelio pietuose) parduoda Chipped brangakmenius ir mygtuku
  „Combine gems“ sujungia kiekvienus 3 vienodus į geresnį (Chipped → paprastas → Flawless).
- **Kritimas:** Chipped — iš visų lauko monstrų (2–5%), paprasti — iš orkų ir Goblin King, Flawless — iš Goblin King.
- Daiktas su brangakmeniu parduodamas brangiau (pridedama brangakmenio kaina). Lizdai saugomi paskyroje.

Testai: `Tests/PlayMode/GemTests.cs`.

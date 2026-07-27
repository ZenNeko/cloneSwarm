# Pitch Deck Outline: Swarm Survivors
**5-Minute Pitch Presentation Template (depa Digital Startup Fund)**
*Language: English (As strictly required by depa)*

---

## Slide 1: Title & Introduction
### "Co-op Raid Survival in a Bullet Heaven"
* **Project Name:** Swarm Survivors (Online Co-op Bullet Heaven Game)
* **Presenter:** Shanawat Wanniran (Solo Developer & Game Design Lead)
* **Tagline:** A 1-4 player cooperative horde survival game combining bullet heaven speed with tactical MMO raid mechanics.
* **Core Technology:** Unity 6 + Netcode for GameObjects (NGO)
* **Visual Concept:** Sleek, neon-accented 3D characters facing massive dark fantasy horde swarms.

---

## Slide 2: The Problem
### "Why Bullet Heavens Feel Lonely and Repetitive"
* **No Real Online Co-op:** The most popular bullet heaven games (Vampire Survivors, Brotato) are strictly single-player. Players cannot share the thrill of survival with friends online.
* **Network Latency & Desync Bottlenecks:** Coordinating fast-paced gameplay with hundreds of active entities on-screen causes heavy lag, packet loss, and position desync in multiplayer environments.
* **Repetitive Scaling & Mechanics:** Standard games in the genre scale difficulty by simply multiplying enemy HP and damage, leading to monotonous endgame phases that lack tactical gameplay.

---

## Slide 3: The Solution
### "Lag-Free Server-Authoritative Architecture"
* **Optimized Server-Authoritative Netcode:** Built using Unity Netcode for GameObjects (NGO) to enforce server-side validation of player positions, hit registration, and projectile trajectories.
* **Overcoming the Desync Bottleneck:** Solves the multiplayer lag issue common in the horde survival genre, ensuring smooth and reliable gameplay for 1-4 players online.
* **Proof of Concept (PoC) Status:** The core network lobby, player synchronization, and logic-handling modules are fully functional and validated, eliminating technical execution risks.

---

## Slide 4: Unique Raid Boss Mechanics
### "Timeline-Based MMO Raids, Streamlined"
* **Timeline-Based Choreography:** Boss fights follow a choreographed timeline of phases and mechanics rather than random actions—drawing direct inspiration from FFXIV raids and *Rabbit and Steel*.
* **Simplified Co-op Telegraphs:** Streamlines complex MMO indicators into clean, high-speed visual telegraphs, allowing players to coordinate instantly without voice chat:
  * **Stack:** Grouping up with teammates to share split damage.
  * **Gaze:** Facing characters away from the boss to prevent being stunned.
  * **Spread:** Moving apart to avoid overlapping AoE damage.
* **High-Level Coordination:** Advanced difficulties (Nightmare) force players to resolve overlapping threats (e.g., executing a **Stack** while avoiding a **Gaze** marker simultaneously), creating an intense, skill-based co-op experience.

---

## Slide 5: Unique Character Abilities & Progression
### "Diverse Playstyles and Strategic Power Growth"
* **Unique Character Abilities (Active & Passive):**
  * **Active Abilities (Q & E Skills):** Hero-specific manual skills (e.g., tactical dashes, combat stance switches, ultimate AoE attacks) providing essential utility during intense boss battles.
  * **Passive Skills:** Auto-triggering combat modifiers (e.g., charge systems, kill-streaks, hit-count thresholds) that uniquely define each hero's gameplay style and weapon synergies.
* **Endgame Weapon Synthesis (Fusions):** Upgrade normal weapons to Super tiers by meeting specific stat/weapon levels, then fuse them into ultimate Fusion Weapons using recipes.
* **Out-of-Game Progression (Talents):** Accumulate gold during runs to unlock permanent stat upgrades in the **Talent Shop** (e.g., Attack Power, Max HP, Ability Haste, Magnet, and Second Chance Revives).

---

## Slide 6: Dynamic Stage Difficulty System
### "Adaptable Challenge for Casual & Hardcore Gamers"
Host can select from 4 difficulty levels in the lobby, which automatically scales server multipliers:

| Difficulty | Enemy HP | Enemy DMG | Gold & EXP | Unique Mechanics / Systems Impact |
| :--- | :---: | :---: | :---: | :--- |
| **Easy** | 0.7x | 0.7x | 0.7x | Perfect for beginners or testing out weapon builds. |
| **Normal** | 1.0x | 1.0x | 1.0x | Standard baseline stats and enemy spawn speeds. |
| **Hard** | 1.5x | 1.5x | 2.0x | +20% Elite mob spawn rate with special mods (Shields, Fire Auras). |
| **Nightmare** | 2.5x | 2.5x | 4.0x | +15% enemy move speed, -20% boss cast times (2.0s telegraph alert windows), and **overlapping raid mechanics** (e.g., Stack + Gaze simultaneously). |

---

## Slide 7: Market Opportunity & Target Audience
### "Sustained Growth and Valve's Official Recognition"
* **Global Market Scaling:** The global roguelike/roguelite market has grown steadily from **$2.3 Billion USD in 2021** to **$3.8 Billion USD in 2025**, and is projected to reach **$9.2 Billion USD by 2034** (growing at a CAGR of 10.3%).
* **Valve's Official 'Bullet Heaven' Tag:** In May 2026, Steam officially recognized "Bullet Heaven" as a formal store tag, validating the massive, permanent player demand for the genre.
* **Proven Genre Hits (Peak Player Milestones):**
  * *Vampire Survivors:* 8M+ copies sold, 77k Peak CCU on Steam.
  * *Brotato:* 2.5M+ copies sold, 38k Peak CCU on Steam.
  * *HoloCure:* Free fan-game showing massive viral demand with 45k Peak CCU and a 99% positive review rating.
  * *Deep Rock Galactic: Survivor:* 1M+ copies sold in its first month.
* **The Paradigm Shift (3D Co-op):** The market is shifting from minimalist single-player games to 3D cooperative multiplayer experiences, creating a massive supply gap that *Swarm Survivors* is built to fill.

---

## Slide 8: Competitive & Risk Analysis
### "Addressing Competitors and the LoL Swarm Risk"
* **Feature Comparison Matrix:**

| Feature | Vampire Survivors | Rabbit & Steel | LoL Swarm (Event) | Swarm Survivors |
| :--- | :---: | :---: | :---: | :---: |
| **High Horde Density** | ✔ (2D) | ✖ (Arena) | ✔ (3D) | **✔ (3D Swarm)** |
| **Online Co-op & Netcode** | ✖ (Local) | ✔ (P2P) | ✔ (Dedicated) | **✔ (Server-Auth NGO)** |
| **MMO Raid Telegraphs** | ✖ | ✔ | ✖ | **✔ (Timeline-based)** |
| **Permanent Standalone** | ✔ | ✔ | ✖ (Event only) | **✔ (Steam Page)** |

* **In-Depth Competitor Gap Analysis (Market Gaps):**
  * **Vampire Survivors & Brotato (Single-player Giants):** Built on single-player engines. Adding true online multiplayer requires a massive architecture rebuild, leaving online co-op players underserved.
  * **HoloCure (Free Fan-Game Giant):** Achieved 45k Peak CCU and a 99% review score, proving massive player interest. However, it is strictly single-player, 2D, and restricted to Hololive IP—making it legally non-commercial and leaving the premium monetized market wide open.
  * **The Spell Brigade (Casual Co-op Bullet Heaven):** Features online co-op but relies on casual chaotic physics and friendly-fire. It lacks the tactical depth, organized phase timelines, and structured boss raid mechanics that core gamers crave.
  * **Rabbit and Steel (Tactical Co-op MMO Arena):** Highly successful FFXIV-style boss mechanics but limited to flat 2D arena environments. It completely misses the "horde power fantasy" of defeating thousands of mobs and farming XP gems.
  * **LoL Swarm (Riot Games Event):** Validated massive global appetite for 3D co-op bullet heavens, but was only a temporary event. It is locked inside the bloated LoL client and requires kernel-level anti-cheat (Riot Vanguard), raising privacy concerns.


* **LoL Swarm Risk Mitigation (If Riot Games returns with Swarm):**
  * **Standalone & Lightweight:** *Swarm Survivors* runs directly on Steam without requiring heavy game launchers or kernel-level anti-cheat drivers (Riot Vanguard), which many PC gamers avoid.
  * **Indie Value & Longevity:** Offered as a premium indie title ($4.99 - $9.99) with deep talent tree progression and custom weapon fusions that exceed LoL's temporary arcade format.
  * **Low-Cost Infrastructure:** Powered by optimized peer-to-peer and lobby servers, allowing us to maintain multiplayer services indefinitely at negligible costs.

---

## Slide 9: Business Model & Financial Projection
### "Premium Indie Game Model with Global Scalability"
* **Business Model:** Buy-to-Play premium game on Steam priced at **$4.99 - $9.99 USD** (targeting global markets: North America, Europe, East Asia).
* **Financial Projection (Year 1):**
  * **Sales Volume Target:** 5,000 units sold on Steam.
  * **Projected Revenue:** $25,000 - $50,000 (~900,000 to 1,800,000 THB).
  * **Profit Margin:** >50% net margin. Low overhead cost due to digital distribution and peer-to-peer lobby server architecture.

---

## Slide 10: Timeline & Milestones
### "A Focused 6-Month Road to Steam Demo"
* **Month 1-2: Advanced Systems & Raid Bosses**
  * Develop 4-tier difficulty scaling logic, profile save systems, and the permanent Talent Shop.
  * Implement advanced MMO raid boss mechanics (Stack, Gaze, Spread).
* **Month 1-5: Complete Art Assets Overhaul**
  * **Month 1-3:** Design and model new 3D characters, monsters, and boss assets to replace placeholders.
  * **Month 2-5:** Re-design environmental scene assets, lobby/in-game UI HUD, and adapt existing VFX.
* **Month 4-6: QA, Closed Beta, & Steam Release**
  * **Month 4-5:** Perform high-latency network stress testing and reconnect logic validation.
  * **Month 5:** Run a Closed Beta phase to fine-tune difficulty multipliers with core players.
  * **Month 6:** Draft developer docs, setup the Steam Store page, and launch the playable Demo.

---

## Slide 11: Team Profile & Funding Request
### "Proven Track Record to Deliver"
* **Lead Developer:** Shanawat Wanniran (4th-year Games & Interactive Media Student, Bangkok University)
  * **AdaBrain Game Jams 2023 (1st Runner-Up):** Project Manager & Lead Designer.
  * **Game Projects:** Lead Gameplay Programmer for *TiniTale* (2024); Multiplayer Interaction Designer for *In The Hollow* (2025).
* **Funding Request: 200,000 THB (depa Digital Startup Fund)**
  * **50% (100,000 THB):** Professional 3D Art & Assets Overhaul
  * **20% (40,000 THB):** QA Network Simulation & Virtual Server Hosting
  * **15% (30,000 THB):** Steam Store Registration & Marketing Trailer/Materials
  * **15% (30,000 THB):** Custom Mechanics C# Scripting & Development Fee

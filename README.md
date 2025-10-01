# Samurai Reflex Duel – Core Loop

## 🎯 Game Concept
*Samurai Reflex Duel* is a minimalist reflex-based duel game.  
Two opponents (the player and an AI) face each other. The goal is to **react faster** to a signal while avoiding **false starts**.  
Each duel lasts only a few seconds, encouraging replayability and mastery.

---

## 🔄 Core Loop
The game flow follows a **5-step cycle**:

1. **Ready**  
   - Both fighters stand idle.  
   - The duel is about to begin.  

2. **Waiting**  
   - A random delay is triggered (e.g., 2–5 seconds).  
   - No one must attack during this phase.  
   - Attacking early results in a **false start** and immediate loss.  

3. **Signal**  
   - A clear visual/audio signal is displayed.  
   - This is the player’s cue to attack as fast as possible.  

4. **Resolve**  
   - The system checks which fighter attacked first.  
   - Reaction times are measured in milliseconds.  
   - If both react at the exact same time, the duel ends in a **draw**.  

5. **Results**  
   - The winner is displayed (Player / AI / Draw).  
   - Reaction times are shown.  
   - Player can retry or change difficulty.  

➡️ After results, the cycle restarts at **Ready** for the next duel.

---

## 🧠 Key Mechanics
- **False Start**: attacking before the signal = automatic loss.  
- **Reaction Window**: AI reacts within a configurable time range (e.g., 100–200 ms).  
- **Difficulty Presets**: Easy / Normal / Hard adjust AI reaction speed, wait range, and false start probability.  
- **Juice / Feedback**: camera shake, flash, and sound effects emphasize the impact of winning or losing.

---

## 📊 Flow Diagram
```mermaid
flowchart TD
    A[Ready] --> B[Waiting]
    B -->|Random delay| C[Signal]
    B -->|Player/AI attacks too early| E[Results: False Start]
    C --> D[Resolve]
    D --> E[Results]
    E --> A

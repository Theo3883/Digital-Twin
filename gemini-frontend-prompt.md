# Gemini AI Studio Prompt — Digital Twin Health App Mock Frontend (iOS Liquid Glass)

---

## SYSTEM CONTEXT

You are an elite UI/UX engineer and iOS design specialist. You will generate a complete, pixel-perfect **mock frontend** for a medical-grade health monitoring iOS app called **"Digital Twin"**, using Apple's **iOS 26 Liquid Glass** design language introduced at WWDC 2025.

---

## WHAT IS iOS LIQUID GLASS (Design Reference)

iOS Liquid Glass is Apple's 2025 design paradigm for iOS 26. It supersedes the traditional frosted-glass (vibrancy) look with a physically-based, fluid glass metaphor. Key visual rules:

### Material & Surface
- **Translucent glass surfaces** — cards, modals, bottom sheets, and navigation bars appear as curved thick glass panes sitting above a blurred version of whatever is behind them.
- **Specular edge highlights** — a thin white/light gradient runs along the top and left edges of every glass panel, simulating refracted light.
- **Backdrop refraction** — the content behind glass panels appears slightly distorted (not just blurred), as if seen through real curved glass. Simulate with `backdrop-filter: blur(24px) saturate(180%)` plus a subtle displacement/warp effect.
- **Adaptive tinting** — glass panels inherit a tint from the dominant color of the background wallpaper/gradient. For this medical app: deep navy/teal environment.
- **Layered glass depth** — elements stack with increasing opacity: background (10% white), cards (18% white), modals (28% white), popovers (38% white).
- **No hard shadows** — depth is conveyed through glass opacity layers, NOT drop shadows. Use `box-shadow: inset 0 1px 1px rgba(255,255,255,0.35)` for the inner highlight.

### Color System
- **Background wallpaper gradient**: Deep space mesh gradient — `#0A0E1A` → `#071B2E` → `#0D2240` with subtle animated aurora-like color shifts (teal, deep purple, midnight blue).
- **Accent / Primary**: Bioluminescent Teal — `#00D4C8` (adapts to `#009688` for alert/active states)
- **Cardiac Red** (critical alerts): `#FF2D55` (Apple's system red)
- **Ambient Orange** (warnings): `#FF9500`
- **Positive Green**: `#30D158`
- **Text Primary**: `rgba(255,255,255,0.92)` (SF Pro Display, weight 600+)
- **Text Secondary**: `rgba(255,255,255,0.52)` (SF Pro Text, weight 400)
- **Text Tertiary**: `rgba(255,255,255,0.28)`

### Typography
- **SF Pro Display** (headings, large numbers): `font-family: -apple-system, "SF Pro Display"`. Titles: 34px/700. Section headers: 22px/600.
- **SF Pro Text** (body, labels): 17px/400 regular, 15px/500 medium.
- **SF Pro Rounded** (metric values, badges): large numeric values (48–64px), weight 300 (thin/light feels medical-grade).
- **SF Mono** (ECG axis values, technical readings): monospaced.
- **Letter spacing**: Tighten headings at `letter-spacing: -0.5px`. Loosen small caps at `0.8px`.

### Motion & Animation
- **Spring physics**: All transitions use spring curves (`cubic-bezier(0.34, 1.56, 0.64, 1)`) — elements overshoot slightly then settle.
- **Liquid fill animations**: Charts and progress rings fill with a fluid, wave-like animation.
- **Pulse glow**: Critical metrics (HR, SpO2) emit a faint radial pulse glow timed to the heart rate value.
- **Parallax depth**: Cards shift slightly (3–5px) on scroll/tilt, reinforcing Z-depth.
- **Page transitions**: Slide up with scale-from-center and blur-in (`transform: scale(0.94) translateY(20px)` → identity).

### Shape Language
- **Extra-large corner radii**: Cards `border-radius: 28px`, inner containers `20px`, small chips `14px`, true pills `9999px`.
- **Floating bottom bars**: Tab bars float 12px above the safe area, fully glass, pill-shaped with 24px radius.
- **SF Symbols**: All icons are SF Symbols style (single-weight, variable optical size).
- **No borders** except the inner specular highlight. No dividers — use spacing instead.

---

## THE APP — DIGITAL TWIN HEALTH ECOSYSTEM

**Purpose**: A medical-grade iOS app that acts as a patient's "digital twin" — continuously monitoring cardiac health (via ECG/SpO2 IoT hardware + Apple HealthKit wearables), contextualizing with environment data (air quality, weather), checking medication safety, and providing AI-powered coaching and anomaly detection.

**Users**:
1. **Patient** — uses the iOS app daily for vitals dashboard, coaching, medication checks, document upload.
2. **Doctor** — views patient data on a web portal (doctor-facing views optional but note the tab).

---

## SCREENS TO GENERATE

Generate **all 8 screens** below as complete, self-contained HTML/CSS (single file per screen, or one combined SPA with tab navigation). Use **real mock data** (not placeholder text). Every number, label, timestamp, chart, and badge must look medically plausible.

Screens:
1. **Home / Wearables Dashboard**
2. **ECG Live Monitor**
3. **Environment & Air Quality Widget**
4. **Medications Safety**
5. **Medical Assistant (AI Chat)**
6. **Digital Twin Profile**
7. **Documents & OCR Upload**
8. **Coaching & Behavioral Analytics**

---

## SCREEN SPECIFICATIONS

---

### SCREEN 1 — Home / Wearables Dashboard

**Purpose**: Primary glanceable view of the patient's live vitals and digital twin status.

**Layout**: Single vertical scroll. Sticky glass top bar. 4 metric cards in a 2×2 grid. Digital Twin mannequin. Environment strip. Coaching snippet.

**Top Bar (Liquid Glass)**:
- Left: Avatar chip — glass pill with patient photo placeholder (initials "A.M."), patient name "Alexandru Munteanu", subtitle "Cardiac Monitoring · Active"
- Center: App logo / wordmark "DigitalTwin" in SF Pro Display
- Right: Notification bell (SF Symbol `bell.badge.fill`) with a red dot badge, and a shield icon

**Hero Section**:
- Full-width glass card (28px radius, 2px specular top border)
- A stylized animated SVG heart rate line (ECG-like blip, repeating loop at ~72 BPM cadence)
- Large metric: "72" in 64px SF Pro Rounded thin, label "BPM" in teal
- Sub-label: "Heart Rate · Normal Range" in secondary text
- Small pill badge: "● Live" with a pulsing green dot

**Metric Cards Grid (2×2)**:

Card 1 — Heart Rate:
- Icon: `heart.fill` (SF Symbol, teal)
- Value: `72` BPM
- Trend: +2 BPM vs last hour (green ↑ arrow chip)
- Sparkline: 20-point sine-wave SVG sparkline (teal line, no fill)
- Last updated: "Updated 3s ago"
- Glass card, 28px radius

Card 2 — SpO2:
- Icon: `lungs.fill` (purple-blue)
- Value: `98.2` %
- Trend: Stable (grey — symbol)
- Sparkline: Tight high-value curve (97.8–98.5 range)
- Sub: "Oxygen Saturation · Excellent"

Card 3 — Steps:
- Icon: `figure.walk` (orange)
- Value: `8,431`
- Trend: +1,200 vs yesterday (green ↑)
- Sparkline: Bar-style micro chart (daily steps this week)
- Progress arc: 84% of 10,000 daily goal

Card 4 — Calories:
- Icon: `flame.fill` (red-orange)
- Value: `1,847` kcal
- Trend: On track
- Sparkline: Gradual rise curve
- Sub: "Active + Resting"

**Digital Twin Mannequin Section**:
- Glass card full-width
- Header: "Digital Twin · Body View"
- SVG body outline (front-facing human silhouette, clean line art)
- Highlighted regions with glass glow overlays:
  - Heart region: pulsing red/pink glow, radius expands/contracts at 72 BPM
  - Lungs region: subtle blue breathing glow
  - Legs/activity: faint orange step-motion trail
- Overlay chips floating on body: "❤ 72 BPM", "🫁 98.2% SpO2", "⚡ Active"

**Environment Strip**:
- Horizontal glass scroll of 3 environment chips:
  - 🌡 "21.4°C" — Temperature
  - 💧 "58% Humidity"
  - 🌬 "PM2.5: 12 μg/m³ · Good" — green badge
- Sourced from "OpenWeatherMap · Bucharest, RO · 3 min ago"

**Coaching Snippet**:
- Glass card, left-side accent bar in teal
- AI avatar icon (sparkle symbol)
- Text: "Your cardiac recovery after yesterday's run was 18% faster than last week's average. Consider a light 20-min recovery walk this afternoon."
- "Full Report →" link chip

---

### SCREEN 2 — ECG Live Monitor

**Purpose**: Real-time ECG visualization with triage status, SkiaSharp-style rendering simulated in SVG/Canvas.

**Layout**: Top status bar → full-screen ECG canvas → metrics strip → triage panel → history tab.

**Top Bar**:
- "ECG Monitor" title
- Recording status pill: "● Recording · ESP32 Connected" (green pulsing dot)
- Settings gear icon

**ECG Canvas (Full Width, ~280px height)**:
- Dark glass background (`rgba(8, 14, 30, 0.95)`)
- Green grid lines (like hospital monitor): `rgba(0, 255, 128, 0.08)`
- Horizontal axis: time labels in SF Mono (0s, 1s, 2s, 3s, 4s, 5s)
- Vertical axis: mV labels (-1.5, -0.5, 0, 0.5, 1.0, 1.5 mV)
- ECG waveform: A realistic-looking SVG path simulating normal sinus rhythm (P-wave, QRS complex, T-wave repeating). Color: `#00FF88` with a trailing glow shadow (`filter: drop-shadow(0 0 4px #00FF88)`)
- Sweep cursor: vertical line in white moving right, clearing old data with a dark trail (classic hospital monitor sweep effect)
- Speed label: "25 mm/s · 10 mm/mV"

**Metrics Strip (below ECG)**:
- 3 glass chips in horizontal row:
  - Heart Rate: "72 BPM" with mini sparkline
  - SpO2: "98.2%" with color dot
  - Signal Quality: "98% Good" with green bar

**Triage Panel**:
- Glass card with header "AI Triage Engine · Status"
- Status badge (large, centered): "✓ NORMAL SINUS RHYTHM" — green glass badge, 28px radius, checkmark SF Symbol
- 3 rule result rows:
  - "Signal Quality Rule" → "Pass" (green checkmark)
  - "Heart Rate Activity Rule" → "Pass ·  72 BPM, Resting" (green)
  - "SpO₂ Rule" → "Pass · 98.2%, Above Threshold" (green)
- Footer: "CNN Anomaly Detection · Last analysis: 4s ago · Class: N (Normal) · Confidence: 97.3%"

**Critical Alert State (show as separate variant/footnote)**:
- Same layout but:
  - Background flashes red glass tint
  - Status badge: "⚠ CRITICAL — POSSIBLE AFIB DETECTED" in `#FF2D55`
  - Red pulse animation radiates from badge
  - Banner slides from top: "Doctor notified · Push sent to Dr. Ionescu"

---

### SCREEN 3 — Environment & Air Quality

**Purpose**: Environmental health contextualizer — correlates patient's cardiac events with pollution, weather.

**Layout**: Glass top card (map placeholder) → AQI breakdown → correlation chart → recommendations.

**Top Card**:
- Blurred city skyline / map mockup as background
- Overlay glass panel: "Bucharest, Romania · 22 Feb 2026"
- Dominant badge: "AQI 42 · GOOD" — large, centered, green glass badge
- Subtext: "Air quality is satisfactory and poses little or no risk."

**Metric Grid (2×2)**:
- PM2.5: `12 μg/m³` · "Good" green badge
- PM10: `18 μg/m³` · "Good"
- Temperature: `21.4°C` · "Comfortable"
- Humidity: `58%` · "Optimal"
- O₃ (Ozone): `38 μg/m³` · "Good"
- NO₂: `22 μg/m³` · "Good"

**Correlation Chart**:
- Glass card full-width
- Header: "Heart Rate vs Air Quality — Last 24h"
- Dual-axis line chart:
  - Left Y-axis: HR (BPM) — teal line
  - Right Y-axis: PM2.5 (μg/m³) — amber line
  - X-axis: time (00:00 → 23:00)
  - Note annotation at 14:30: "PM2.5 spike · HR +8 BPM correlation"
- Footnote: "Moderate correlation detected (r = 0.62). Domain risk event threshold: PM2.5 > 35 μg/m³."

**Recommendations Glass Card**:
- AI icon + teel accent bar
- "Low pollution levels detected. Outdoor activity recommended between 06:00–09:00 for optimal cardiac benefit."
- Source chips: "OpenWeatherMap", "Google Air Quality API"

---

### SCREEN 4 — Medications Safety

**Purpose**: Drug-drug interaction checker with RxNav integration.

**Layout**: Medication list → Interaction matrix → Add medication FAB.

**Medication List**:
- Header: "Active Medications · 4 drugs"
- Each medication as a glass card row:
  - `Metoprolol 50mg` · "Twice daily" · Small tag: "Beta Blocker"
  - `Warfarin 5mg` · "Once daily" · Small tag: "Anticoagulant"
  - `Atorvastatin 20mg` · "Once at night" · Small tag: "Statin"
  - `Aspirin 100mg` · "Once daily" · Small tag: "Antiplatelet"

**Safety Badge (Prominent)**:
- Full-width glass card
- `⚠ 2 Interactions Detected` — amber badge, large
- Sub: "1 Moderate · 1 Low Severity"

**Interaction Cards**:

Interaction 1 (Moderate — Amber):
- Drug A: Warfarin | Drug B: Aspirin
- Severity badge: "MODERATE" — amber glass badge
- Description: "Concurrent use increases risk of bleeding. Monitor INR closely. Physician-supervised combination."
- Risk icon: shield with exclamation

Interaction 2 (Low — Yellow):
- Drug A: Metoprolol | Drug B: Atorvastatin
- Severity badge: "LOW" — yellow glass badge
- Description: "Minor interaction. Standard monitoring sufficient."

**Add Medication FAB**:
- Bottom-center floating pill button: "+ Add Medication"
- Glass material, teal accent

---

### SCREEN 5 — Medical AI Assistant (RAG Chat)

**Purpose**: GPT-4/Llama-based medical assistant with RAG over clinical guidelines.

**Layout**: Full-screen chat interface. Message bubbles. Input bar at bottom.

**Top Bar**:
- Avatar: AI brain/sparkle icon in glass circle
- Name: "MedAssist · Digital Twin AI"
- Subtitle: "Powered by GPT-4 · RAG: ESC Guidelines, PubMed 2024"
- Status: "● Online"

**Chat Messages (mock conversation)**:

User bubble (right, solid teal glass):
"Why is my heart occasionally skipping a beat? I noticed it in last night's ECG."

AI bubble (left, frosted glass):
"Based on your ECG recording from 23 Feb at 02:14, I detected 3 isolated **Premature Ventricular Contractions (PVCs)** — these are the 'skipped beat' sensations you're feeling.

**What are PVCs?**
PVCs occur when an electrical signal originates from the ventricles instead of the SA node, causing early contraction. They appear as wide QRS complexes on your ECG.

**Is it serious?**
In an otherwise healthy heart, isolated PVCs (< 5% burden) are typically benign. Your 24h burden is estimated at 0.8%.

**References sourced from:** ESC 2022 Ventricular Arrhythmias Guidelines (Section 4.2), AHA 2020 Premature Beat Management.

Would you like me to generate a clinical report for Dr. Ionescu?"

User bubble:
"Yes, generate the clinical report."

AI bubble (with document preview card):
"Generating clinical report... ✓

**Clinical ECG Summary — Alexandru Munteanu**
Date: 22 Feb 2026 | Duration: 24h Holter
- Dominant rhythm: Normal sinus, 68–78 BPM
- PVC count: 847 events (burden: 0.8%)
- Morphology: LBBB pattern, inferior axis → likely RVOT origin
- QTc: 412ms (Normal)
- No sustained VT, no AF detected

[Download PDF Report ↓]  [Share with Doctor ↓]"

**Bottom Input Bar**:
- Glass pill input field: "Ask a medical question..."
- Microphone icon (voice input)
- Send arrow button (teal)
- Disclaimer chip: "Not a substitute for professional medical advice"

---

### SCREEN 6 — Digital Twin Profile

**Purpose**: Comprehensive patient health profile, profile completion, and doctor assignments.

**Layout**: Profile header → completion ring → vital statistics → doctor assignments → medical history timeline.

**Profile Header**:
- Large glass card
- Avatar circle (initials "A.M.", teal gradient background)
- Name: "Alexandru Munteanu" — SF Pro Display 28px
- DOB: "14 March 1988 (37 y/o)" | Blood type: "A+"
- Patient ID: "PT-62894-RO"
- Completion ring: "Profile 78% Complete" — circular progress ring in teal

**Profile Completion Checklist**:
- ✓ Personal info
- ✓ Medical history
- ✓ Current medications (4)
- ✓ ECG device connected
- ○ Upload discharge letters (0/2)
- ○ Family history (pending)

**Vital Statistics Grid**:
- Weight: 78 kg
- Height: 181 cm
- BMI: 23.8 (Normal)
- Resting HR: 68 BPM
- Blood Pressure: 118/76 mmHg
- Cholesterol: 4.2 mmol/L

**Assigned Doctors**:
- Glass card
- Dr. Elena Ionescu · Cardiologist · "Spitalul Universitar, Bucharest" (avatar, online dot)
- Dr. Mihai Popa · General Practitioner (avatar)
- "+ Connect New Doctor" button

**Medical History Timeline (vertical)**:
- Mar 2024: "Paroxysmal SVT episode — ED visit, cardioverted"
- Oct 2023: "Started Metoprolol 50mg"
- Jun 2022: "Echocardiogram — Normal LV function, EF 62%"
- Jan 2021: "Holter Monitor — Isolated PVCs, benign"

---

### SCREEN 7 — Documents & OCR Upload

**Purpose**: Upload medical documents (PDFs, photos). OCR + NLP extracts structured data.

**Layout**: Upload zone → processing state → extracted data review → confirm & save.

**Upload Zone** (State 1):
- Large centered glass card (dashed teal border, 28px radius)
- Upload icon: `doc.badge.plus` (SF Symbol, large)
- Text: "Drop discharge letters, lab results, or prescriptions"
- Sub: "PDF, JPG, PNG supported · Up to 25MB"
- Two CTA buttons: "Choose File" (solid teal pill) | "Take Photo" (glass pill)

**Processing State** (State 2 — animated):
- Glass card with spinning progress ring (teal)
- Steps shown: "1. OCR Extraction ✓ | 2. NLP Classification ✓ | 3. FHIR Mapping..."
- Document thumbnail on left
- "Analyzing: Scrisoare de externare — Spitalul Floreasca, 12 Jan 2024"

**Extracted Data Review** (State 3):
- Glass card header: "Extracted Data — Please Review"
- Confidence badge: "94% Confidence"
- Table of extracted fields (editable inline):
  - Patient Name: "Alexandru Munteanu" ✓
  - Date: "12 Jan 2024" ✓
  - Diagnosis (ICD-10): "I47.1 — Supraventricular Tachycardia" ✓
  - Medications Prescribed: "Metoprolol 50mg 2x/day" ✓
  - Next appointment: "Follow-up in 3 months" ✓
  - Physician: "Dr. Elena Ionescu" ✓
- Warning row: "Unrecognized field: 'Analize anexate'" — amber tag, manual input
- CTAs: "Confirm & Save to Profile" (teal) | "Edit" | "Discard"

**Document History**:
- Small list at bottom: previously uploaded documents with date chips

---

### SCREEN 8 — Coaching & Behavioral Analytics

**Purpose**: Gemini Pro-powered behavioral insights and personalized health coaching.

**Layout**: Weekly performance summary → coaching card → trend charts → NLP query interface.

**Weekly Performance Summary**:
- Glass card header: "Week of 17–22 Feb 2026"
- 3 large KPI tiles (horizontal scroll):
  - Activity Score: `82/100` — progress arc, teal. "Top 25% this week"
  - Sleep Score: `74/100` — progress arc, purple. "Avg 6h 48m"
  - Recovery Score: `88/100` — progress arc, green. "+15% vs last week"

**Coaching Card (Gemini)**:
- AI sparkle avatar, "Gemini Pro · Behavioral Coach" label
- Teal left accent bar
- Large coaching insight:
  "🏃 Your cardiac recovery after Thursday's 8km run was 18% faster than your weekly average, reaching baseline HR within 4 minutes. 

  💤 Sleep quality dipped Tuesday–Wednesday (< 6h deep sleep). This correlates with elevated resting HR (+6 BPM) on Wednesday morning.

  📋 Recommendation: Prioritize 7+ hours of sleep tonight. A 25-min low-intensity walk tomorrow at 07:00 (air quality: Good) would compound your cardiovascular adaptations without overloading your recovery."
- Refresh button | "Share with Doctor" button

**Trend Charts**:

Chart 1 — Resting HR Trend (7 days):
- Line chart, teal, gentle curve
- Average annotation line: "68 BPM avg"
- Wednesday annotated: "Elevated after poor sleep"

Chart 2 — Steps vs Target (bar):
- Daily bars this week vs 10,000 goal line
- Colors: green (met), amber (close), red (missed)

Chart 3 — Sleep Architecture:
- Stacked bar per night: Deep / REM / Light / Awake
- Color coded: deep blue / purple / light blue / grey

**NLP Query Interface**:
- Glass search/chat bar at bottom
- Pre-filled suggestions: "How did I progress this month?" | "Best time to exercise today?" | "Explain my sleep score"
- Results appear inline as AI coaching cards

---

## TECHNICAL IMPLEMENTATION REQUIREMENTS

### Stack
- Pure **HTML5 + CSS3 + Vanilla JS** (single file or modular)
- No React/Vue — use native web components or simple JS classes
- **CSS Custom Properties** for the entire design token system
- `backdrop-filter` for glass effect (with `-webkit-` prefix)
- SVG for ECG waveforms, sparklines, body mannequin, and icons
- CSS animations (no GSAP) for pulse, sweep, and fill effects

### CSS Architecture
```css
:root {
  /* Liquid Glass tokens */
  --glass-bg-1: rgba(255, 255, 255, 0.08);      /* Level 1 — background panels */
  --glass-bg-2: rgba(255, 255, 255, 0.14);      /* Level 2 — cards */
  --glass-bg-3: rgba(255, 255, 255, 0.22);      /* Level 3 — modals */
  --glass-border: rgba(255, 255, 255, 0.18);
  --glass-highlight: rgba(255, 255, 255, 0.32); /* specular top edge */
  --glass-blur: blur(28px) saturate(180%);
  
  /* Medical colors */
  --teal-primary: #00D4C8;
  --teal-active: #009688;
  --red-critical: #FF2D55;
  --amber-warning: #FF9500;
  --green-positive: #30D158;
  
  /* Background */
  --bg-deep: #0A0E1A;
  --bg-mesh: radial-gradient(ellipse at 20% 50%, #071B2E 0%, transparent 60%),
             radial-gradient(ellipse at 80% 20%, #0D2240 0%, transparent 50%),
             radial-gradient(ellipse at 50% 80%, #0A1628 0%, transparent 50%);
  
  /* Typography */
  --font-display: -apple-system, "SF Pro Display", "Helvetica Neue", sans-serif;
  --font-text: -apple-system, "SF Pro Text", "Helvetica Neue", sans-serif;
  --font-rounded: -apple-system, "SF Pro Rounded", "Helvetica Neue", sans-serif;
  --font-mono: "SF Mono", "Menlo", "Monaco", monospace;
  
  /* Spacing scale (4px base) */
  --space-xs: 4px; --space-s: 8px; --space-m: 16px; 
  --space-l: 24px; --space-xl: 32px; --space-2xl: 48px;
  
  /* Radius */
  --radius-chip: 10px; --radius-card: 28px; --radius-modal: 36px; --radius-pill: 9999px;
  
  /* Transitions */
  --spring: cubic-bezier(0.34, 1.56, 0.64, 1);
  --ease-smooth: cubic-bezier(0.25, 0.46, 0.45, 0.94);
}
```

### Glass Component Mixin Pattern
```css
.glass-card {
  background: var(--glass-bg-2);
  backdrop-filter: var(--glass-blur);
  -webkit-backdrop-filter: var(--glass-blur);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-card);
  box-shadow: 
    inset 0 1px 0 var(--glass-highlight), /* specular top edge */
    0 8px 32px rgba(0, 0, 0, 0.4);        /* depth shadow */
}
```

### ECG Animation (Screen 2)
- Use `<svg>` with a `<polyline>` element
- Animate via JS: push new sample points every 2ms (simulated), shift array left, redraw
- OR use CSS `stroke-dashoffset` animation on a pre-drawn SVG path of a full PQRST complex

### Heartbeat Pulse Animation
```css
@keyframes heartbeat-pulse {
  0%   { transform: scale(1);   opacity: 0.6; }
  14%  { transform: scale(1.3); opacity: 0.8; }
  28%  { transform: scale(1.1); opacity: 0.7; }
  42%  { transform: scale(1.25); opacity: 0.8; }
  70%  { transform: scale(1);   opacity: 0.6; }
  100% { transform: scale(1);   opacity: 0.6; }
}
```

### Tab Navigation Bar
- Floating glass pill, 12px above safe area
- 5 tabs with SF Symbol-style icons: Home (house), ECG (waveform.path.ecg), Environment (leaf), Meds (pills), Profile (person.crop.circle)
- Active tab: solid teal background pill inside the glass bar
- Tab labels: 10px SF Pro Text

### Responsive Consideration
- Design for iPhone 15 Pro viewport: 393 × 852 pt
- Include iPhone 15 Pro Dynamic Island simulation at top (optional but impressive)
- Max content width: 393px, centered on wider screens

---

## DATA CONTRACTS (Use These Exact Mock Values)

```json
{
  "patient": {
    "name": "Alexandru Munteanu",
    "dob": "1988-03-14",
    "age": 37,
    "bloodType": "A+",
    "bmi": 23.8
  },
  "vitals": {
    "heartRate": 72,
    "spo2": 98.2,
    "steps": 8431,
    "calories": 1847,
    "heartRateTrend": [68, 70, 71, 73, 72, 74, 72, 71, 72, 70, 69, 71, 72, 73, 74, 72, 71, 70, 72, 72],
    "spo2Trend": [98.1, 98.0, 98.3, 98.2, 98.4, 98.2, 98.1, 98.3, 98.2, 98.2, 98.5, 98.3, 98.1, 98.2, 98.0, 98.3, 98.4, 98.2, 98.1, 98.2]
  },
  "environment": {
    "city": "Bucharest, Romania",
    "temperature": 21.4,
    "humidity": 58,
    "pm25": 12,
    "pm10": 18,
    "aqi": 42,
    "aqiLevel": "Good",
    "sourcedAt": "2026-02-22T15:00:00Z"
  },
  "medications": [
    { "name": "Metoprolol", "dose": "50mg", "frequency": "Twice daily", "class": "Beta Blocker" },
    { "name": "Warfarin", "dose": "5mg", "frequency": "Once daily", "class": "Anticoagulant" },
    { "name": "Atorvastatin", "dose": "20mg", "frequency": "Once at night", "class": "Statin" },
    { "name": "Aspirin", "dose": "100mg", "frequency": "Once daily", "class": "Antiplatelet" }
  ],
  "interactions": [
    { "drugA": "Warfarin", "drugB": "Aspirin", "severity": "Moderate", "description": "Increased bleeding risk. Monitor INR." },
    { "drugA": "Metoprolol", "drugB": "Atorvastatin", "severity": "Low", "description": "Minor interaction, standard monitoring sufficient." }
  ],
  "coaching": {
    "activityScore": 82,
    "sleepScore": 74,
    "recoveryScore": 88,
    "advice": "Your cardiac recovery after Thursday's run was 18% faster. Prioritize sleep tonight and consider a 25-min low-intensity walk tomorrow morning."
  }
}
```

---

## OUTPUT FORMAT

1. Generate **8 complete HTML screens** — one per section above.
2. All in a **single HTML file** with tab-based navigation (show/hide screens via JS).
3. **No placeholder content** — all text, numbers, charts, and colors must be from the specs above.
4. CSS must implement the full Liquid Glass token system defined in the Technical section.
5. ECG waveform must animate (sweeping or looping SVG path animation).
6. Heart rate metric card must have a CSS heartbeat pulse effect.
7. Include a floating iPhone-style pill at the top (Dynamic Island simulation) on screen 2 (ECG).
8. The tab bar must be a floating glass pill with 5 icons.
9. Include CSS `@media (prefers-color-scheme: light)` overrides that adapt glass to a light wallpaper variant — but default is dark.
10. Include `@media (prefers-reduced-motion: reduce)` that disables all animations gracefully.

---

## QUALITY BAR

This is a **medical-grade application** — clarity, hierarchy, and readability are paramount.

- No decorative elements that obscure data.
- Critical values (HR, SpO2) must be legible at a glance.
- Color alone never conveys state — always pair color with an icon or label.
- All interactive elements must have `:focus-visible` outlines for accessibility.
- Contrast ratio for text on glass surfaces must meet **WCAG AA** (4.5:1 minimum).
- The design should look like it belongs between **Apple Health, Dexcom G7, and an ICU monitor**.

---

*End of prompt. Generate all 8 screens in a single HTML output file.*
